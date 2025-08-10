param(
    [Parameter(Mandatory = $true)]
    [string] $ServicePrincipalId,  # Can be AppId (client ID) or ObjectId
    [switch] $Quiet
)

$ErrorActionPreference = 'Stop'

# Required Graph scopes (minimal set):
$scopes = @(
    'Directory.Read.All',
    'RoleManagement.ReadWrite.Directory'
)

# Ensure Graph module
if (-not (Get-Module -ListAvailable -Name Microsoft.Graph)) {
    Write-Host 'Installing Microsoft.Graph module...'
    Install-Module Microsoft.Graph -Scope CurrentUser -Force -AllowClobber | Out-Null
}
Import-Module Microsoft.Graph -ErrorAction Stop

if (-not $Quiet) { Write-Host 'Connecting to Microsoft Graph...' }
Connect-MgGraph -Scopes $scopes | Out-Null
Select-MgProfile -Name v1.0 -ErrorAction SilentlyContinue | Out-Null

function Resolve-ServicePrincipal {
    param([string] $Id)
    $sp = Get-MgServicePrincipal -Filter "appId eq '$Id'" -ConsistencyLevel eventual -CountVariable _c -ErrorAction SilentlyContinue
    if (-not $sp) {
        $sp = Get-MgServicePrincipal -ServicePrincipalId $Id -ErrorAction SilentlyContinue
    }
    if (-not $sp) { throw "Service principal not found by AppId or ObjectId: $Id" }
    return $sp
}

$sp = Resolve-ServicePrincipal -Id $ServicePrincipalId
if (-not $Quiet) {
    Write-Host "Service Principal DisplayName: $($sp.DisplayName)"
    Write-Host "Service Principal ObjectId:   $($sp.Id)"
}

# Get or activate Directory Readers directory role
$role = Get-MgDirectoryRole -Filter "displayName eq 'Directory Readers'" -ErrorAction SilentlyContinue
if (-not $role) {
    if (-not $Quiet) { Write-Host 'Activating Directory Readers role...' }
    $template = Get-MgDirectoryRoleTemplate -Filter "displayName eq 'Directory Readers'"
    if (-not $template) { throw 'Directory Readers role template not found.' }
    Invoke-MgGraphRequest -Method POST -Uri 'https://graph.microsoft.com/v1.0/directoryRoles' -Body (@{ roleTemplateId = $template.Id } | ConvertTo-Json) | Out-Null
    Start-Sleep -Seconds 4
    $role = Get-MgDirectoryRole -Filter "displayName eq 'Directory Readers'"
}
if (-not $role) { throw 'Failed to obtain Directory Readers role after activation.' }
if (-not $Quiet) { Write-Host "Directory Readers Role Id: $($role.Id)" }

# Idempotency check
function Test-Membership {
    param($RoleId, $SpId)
    return Get-MgDirectoryRoleMember -DirectoryRoleId $RoleId -All -ErrorAction SilentlyContinue | Where-Object { $_.Id -eq $SpId }
}

if (Test-Membership -RoleId $role.Id -SpId $sp.Id) {
    if (-not $Quiet) { Write-Host 'Service principal already has Directory Readers role. No action taken.' }
    return
}

if (-not $Quiet) { Write-Host 'Adding service principal to Directory Readers role...' }

$body = @{ '@odata.id' = "https://graph.microsoft.com/v1.0/directoryObjects/$($sp.Id)" }

$added = $false
try {
    # Primary method
    New-MgDirectoryRoleMemberByRef -DirectoryRoleId $role.Id -BodyParameter $body | Out-Null
    $added = $true
}
catch {
    $msg = $_.Exception.Message
    if ($msg -match 'same key has already been added' -or $msg -match 'One or more added object references already exist') {
        if (-not $Quiet) { Write-Host 'Graph SDK reported duplicate / existing reference. Verifying membership...' }
    }
    elseif ($msg -match 'Request_ResourceNotFound') {
        if (-not $Quiet) { Write-Host 'Role not fully propagated yet; retrying after short delay...' }
        Start-Sleep -Seconds 5
        try {
            New-MgDirectoryRoleMemberByRef -DirectoryRoleId $role.Id -BodyParameter $body | Out-Null
            $added = $true
        }
        catch {
            $msg2 = $_.Exception.Message
            if (-not $Quiet) { Write-Warning "Retry via SDK failed: $msg2. Falling back to raw REST." }
        }
    }
    else {
        if (-not $Quiet) { Write-Warning "SDK add failed: $msg. Falling back to raw REST call." }
    }
}

if (-not (Test-Membership -RoleId $role.Id -SpId $sp.Id)) {
    # Fallback raw REST POST (idempotent; duplicate returns 400 with specific error we ignore)
    try {
        Invoke-MgGraphRequest -Method POST -Uri "https://graph.microsoft.com/v1.0/directoryRoles/$($role.Id)/members/\$ref" -Body ($body | ConvertTo-Json) | Out-Null
        $added = $true
    }
    catch {
        $msg = $_.Exception.Message
        if ($msg -match 'One or more added object references already exist') {
            if (-not $Quiet) { Write-Host 'Membership already existed per REST response.' }
        }
        else { throw }
    }
}

if (Test-Membership -RoleId $role.Id -SpId $sp.Id) {
    if (-not $Quiet) { Write-Host 'Role assignment complete (confirmed).' }
}
else {
    throw 'Failed to assign Directory Readers role after all attempts.'
}