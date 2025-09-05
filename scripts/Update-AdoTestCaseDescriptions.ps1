<#!
.SYNOPSIS
Adds or overwrites one-line descriptions for all Test Cases in an Azure DevOps Test Plan.

.DESCRIPTION
Traverses all suites in the specified test plan, gathers test case work item IDs, derives a concise one-line description
from each test case title, and updates the System.Description field (HTML) via the Work Item REST API.

.PARAMETER Organization
Azure DevOps organization name (the segment after https://dev.azure.com/).

.PARAMETER Project
Azure DevOps project name.

.PARAMETER PlanId
Test Plan ID to process.

.PARAMETER PatEnvVar
Environment variable name containing a Personal Access Token with Work Item (Read/Write) + Test Plan (Read) scopes. Default: AZDO_PAT

.PARAMETER Overwrite
If set, existing non-empty descriptions will be replaced. If not set, only blank descriptions are filled.

.PARAMETER DryRun
If set, no updates are sent; proposed changes are printed.

.EXAMPLE
PS> ./Update-AdoTestCaseDescriptions.ps1 -Organization myorg -Project aistocktradeapp -PlanId 1401 -Overwrite

.NOTES
Requires: PowerShell 7+, Internet access to Azure DevOps REST API.
#>
param(
    [Parameter(Mandatory)] [string]$Organization,
    [Parameter(Mandatory)] [string]$Project,
    [Parameter(Mandatory)] [int]$PlanId,
    [string]$PatEnvVar = 'AZDO_PAT',
    [switch]$Overwrite,
    [switch]$DryRun,
    [switch]$DumpFirstCase,   # dumps first raw test case object per suite for diagnostics
    [switch]$DumpOnUnknown,   # dumps any unknown structure encountered
    [switch]$ShowPatch        # show patch JSON per work item
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-AuthHeader {
    param([string]$Pat)
    $bytes = [System.Text.Encoding]::ASCII.GetBytes(":$Pat")
    return "Basic " + [Convert]::ToBase64String($bytes)
}

function Invoke-AdoGet {
    param(
        [string]$Uri
    )
    try {
        $resp = Invoke-RestMethod -Method GET -Uri $Uri -Headers $script:Headers -ErrorAction Stop
        return $resp
    }
    catch {
        $status = $_.Exception.Response.StatusCode.value__ 2>$null
        $msg = $_.Exception.Message
        Write-Warning "GET failed ($status): $Uri"
        if ($_.ErrorDetails.Message) { Write-Verbose $_.ErrorDetails.Message }
        throw
    }
}

function Invoke-AdoPatchWorkItem {
    param(
        [int]$Id,
        [object[]]$Operations
    )
    $uri = "https://dev.azure.com/$Organization/$Project/_apis/wit/workitems/${Id}"
    # Ensure array form for JSON Patch
    if ($Operations -isnot [System.Array]) { $Operations = @($Operations) }
    $json = $Operations | ConvertTo-Json -Depth 5 -Compress
    if (-not ($json.Trim().StartsWith('['))) {
        $json = '[' + $json + ']'
    }
    if ($DryRun) {
        Write-Host "[DRY-RUN] PATCH $Id => $json" -ForegroundColor DarkGray
    } else {
    # Supply api-version via Accept header to avoid query parameter
    $patchHeaders = @{}
    foreach ($k in $script:Headers.Keys) { $patchHeaders[$k] = $script:Headers[$k] }
    $patchHeaders['Accept'] = 'application/json;api-version=7.1-preview.3'
        if ($ShowPatch) { Write-Host "PATCH $Id JSON: $json" -ForegroundColor DarkGray }
        try {
            Invoke-RestMethod -Method PATCH -Uri $uri -Headers $patchHeaders -ContentType 'application/json-patch+json' -Body $json -ErrorAction Stop | Out-Null
        }
        catch {
            Write-Warning "Primary PATCH failed for #${Id}: $($_.Exception.Message). Retrying with query api-version."
            $retryUri = "$uri?api-version=7.1-preview.3"
            try {
                Invoke-RestMethod -Method PATCH -Uri $retryUri -Headers $patchHeaders -ContentType 'application/json-patch+json' -Body $json -ErrorAction Stop | Out-Null
            }
            catch {
                Write-Error "Retry PATCH failed for #${Id}: $($_.Exception.Message)"; throw
            }
        }
    }
}

function Get-TestSuites {
    $uri = "https://dev.azure.com/$Organization/$Project/_apis/test/plans/${PlanId}/suites"
    (Invoke-AdoGet -Uri $uri).value
}

function Get-TestPlanMeta {
    $uri = "https://dev.azure.com/$Organization/$Project/_apis/test/plans/${PlanId}"
    Invoke-AdoGet -Uri $uri
}

function Get-TestCasesInSuite {
    param([int]$SuiteId)
    # Re-introduce explicit API version here only; this endpoint is more finicky without versioning
    $uri = "https://dev.azure.com/$Organization/$Project/_apis/test/Plans/${PlanId}/suites/$SuiteId/testcases?api-version=7.1-preview.2"
    $resp = Invoke-AdoGet -Uri $uri
    if (-not $resp) { return @() }
    return $resp.value
}

function Get-WorkItemBasic {
    param([int]$Id)
    $uri = "https://dev.azure.com/$Organization/$Project/_apis/wit/workitems/${Id}?api-version=7.1-preview.3&fields=System.Title,System.Description"
    Invoke-AdoGet -Uri $uri
}

function New-OneLineDescription {
    param([string]$Title)
    if (-not $Title) { return "" }
    $t = $Title.Trim()
    # Remove common prefixes like TC123:, IDs, or bracketed tags
    $t = $t -replace '^TC\d+[:\-]\s*',''
    $t = $t -replace '^\[[^\]]+\]\s*',''
    # Convert imperative or dash-separated phrases into a sentence
    $t = $t -replace '\s*-\s*',' – '
    # Ensure ends with a period
    if ($t.Length -gt 180) { $t = $t.Substring(0,177) + '…' }
    if ($t -notmatch '\.$') { $t += '.' }
    # Minimal HTML wrapper (ADO expects HTML in Description)
    return "<p>$t</p>"
}

# ---- MAIN ----
$pat = [Environment]::GetEnvironmentVariable($PatEnvVar)
if (-not $pat) { throw "Personal Access Token env var '$PatEnvVar' not set." }
$script:Headers = @{ Authorization = Get-AuthHeader -Pat $pat }

Write-Host "Retrieving suites for plan $PlanId..." -ForegroundColor Cyan
try {
    $planMeta = Get-TestPlanMeta
    Write-Host " Plan: $($planMeta.name) (State: $($planMeta.state))" -ForegroundColor DarkCyan
}
catch {
    Write-Error "Unable to retrieve Test Plan $PlanId. Verify: 1) Plan ID exists 2) PAT has 'Test Plans (Read)' scope 3) Your license includes Test Plans 4) Organization/Project names are correct. Raw: $($_.Exception.Message)"
    return
}

try {
    $suites = Get-TestSuites
}
catch {
    Write-Error "Failed fetching suites collection. See previous warnings."
    return
}

if (-not $suites) {
    Write-Warning "No suites returned. If Plan has only root suite, add a suite or manually supply test case IDs."
    return
}

$allCaseIds = [System.Collections.Generic.HashSet[int]]::new()
foreach ($suite in $suites) {
    Write-Host " Suite $($suite.id): $($suite.name)" -ForegroundColor Yellow
    $cases = Get-TestCasesInSuite -SuiteId $suite.id
    # Force array semantics even if a single object comes back
    if ($null -ne $cases -and $cases -isnot [System.Array]) { $cases = @($cases) }
    if ($DumpFirstCase -and $cases -and (@($cases).Count -gt 0)) {
        Write-Host "  Raw first case object:" -ForegroundColor DarkCyan
        (@($cases)[0] | ConvertTo-Json -Depth 6) | Write-Host
    }
    foreach ($c in @($cases)) {
        if (-not $c) { continue }
        $caseId = $null
        if ($c.testCase -and $c.testCase.id) { $caseId = $c.testCase.id }
        elseif ($c.id) { $caseId = $c.id }
        if (-not $caseId) {
            Write-Warning "  Could not find test case id in entry.";
            if ($DumpOnUnknown) { ($c | ConvertTo-Json -Depth 6) | Write-Host }
            continue
        }
        if ($caseId -notmatch '^\d+$') {
            Write-Warning "  Skipping non-numeric id: $caseId"; continue
        }
        [void]$allCaseIds.Add([int]$caseId)
    }
}

Write-Host "Total distinct test cases: $($allCaseIds.Count)" -ForegroundColor Cyan

$updated = 0
$skipped = 0

foreach ($id in $allCaseIds) {
    $wi = Get-WorkItemBasic -Id $id
    $title = $wi.fields.'System.Title'
    # Handle absence of System.Description property gracefully
    $existing = $null
    $hasExistingField = $false
    if ($wi.fields -and ($wi.fields.PSObject.Properties.Name -contains 'System.Description')) {
        $existing = $wi.fields.'System.Description'
        $hasExistingField = $true
    }
    $newDesc = New-OneLineDescription -Title $title
    $hasExistingContent = ($existing -and ($existing -is [string]) -and $existing.Trim() -ne '')
    if ($hasExistingContent -and -not $Overwrite) {
        $skipped++
        Write-Host " Skip (has description) #$id $title" -ForegroundColor DarkGray
        continue
    }
    if ($hasExistingField -and $Overwrite) { $op = 'replace' } else { $op = 'add' }
    if ($existing -and $existing -eq $newDesc) {
        Write-Host " No change #$id $title" -ForegroundColor DarkGray
        continue
    }
    Invoke-AdoPatchWorkItem -Id $id -Operations @(@{ op = $op; path = '/fields/System.Description'; value = $newDesc })
    $updated++
    Write-Host " Updated #$id -> one-line description" -ForegroundColor Green
}

Write-Host "Done. Updated=$updated Skipped=$skipped" -ForegroundColor Cyan
if ($DryRun) { Write-Host "(Dry-run mode: no changes were persisted)" -ForegroundColor Magenta }
