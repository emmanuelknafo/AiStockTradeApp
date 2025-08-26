param(
  [Parameter(Mandatory=$true)]
  [string]$Pat,                                # Azure DevOps PAT with Wiki (Read & Write)
  [string]$OrgUrl = "https://dev.azure.com/MngEnvMCAP675646",
  [string]$Project = "AiStockTradeApp",
  [string]$ProjectId = "",                   # optional: project GUID
  [string]$WikiIdentifier = "59aa6550-3563-42ed-a4e7-8088fb517b93", # GUID or Project.wiki
  [string]$WikiBranch = "wikiMaster",
  [string]$PageRoot = "/Releases",           # base path for releases
  [Alias("Tag","Version")]
  [string]$ReleaseVersion,                     # e.g. v0.2.3; if set and PagePath not provided, path becomes /Releases/<version>
  [string]$PagePath,                           # optional explicit path; overrides PageRoot/ReleaseVersion
  [string]$FilePath,                          # optional; if provided, content is read from file
  [string]$Content                            # optional; if not provided, read FilePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function New-BasicAuthHeader {
  param([Parameter(Mandatory)][string]$Token)
  $basic = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":" + $Token))
  return @{ Authorization = "Basic $basic"; 'Content-Type' = 'application/json' }
}

function Invoke-Req {
  param(
    [ValidateSet('GET','PUT')] [string]$Method,
    [Parameter(Mandatory)][string]$Uri,
    [Parameter(Mandatory)][hashtable]$Headers,
    [string]$Body
  )
  try {
    if ($Method -eq 'GET') {
      $resp = Invoke-WebRequest -Uri $Uri -Headers $Headers -Method GET -SkipHttpErrorCheck
    } else {
      $resp = Invoke-WebRequest -Uri $Uri -Headers $Headers -Method PUT -Body $Body -SkipHttpErrorCheck
    }
    [pscustomobject]@{
      Status  = [int]$resp.StatusCode
      Content = $resp.Content
      Headers = $resp.Headers
    }
  } catch {
    $ex = $_.Exception
    [pscustomobject]@{ Status = 0; Content = $ex.Message; Headers = @{} }
  }
}

if (-not $Content) {
  if (-not $FilePath) { throw "Provide -FilePath or -Content" }
  if (-not (Test-Path -LiteralPath $FilePath)) { throw "File not found: $FilePath" }
  $Content = Get-Content -LiteralPath $FilePath -Raw -Encoding UTF8
}
$bodyJson = @{ content = $Content } | ConvertTo-Json -Compress

# Compute PagePath: prefer explicit PagePath; else use PageRoot + ReleaseVersion; else default to /Releases/wikiMaster
if (-not $PagePath -or [string]::IsNullOrWhiteSpace($PagePath)) {
  if ($ReleaseVersion) {
    $root = $PageRoot
    if (-not $root.StartsWith('/')) { $root = '/' + $root }
    $ver = $ReleaseVersion.Trim().TrimStart('/')
    $PagePath = "$root/$ver"
  } else {
    $PagePath = "/Releases/wikiMaster"
  }
}

# URL-encode path but keep slashes
$encodedPath = [Uri]::EscapeDataString($PagePath) -replace '%2F','/'
$qs = "versionDescriptor.version=$WikiBranch&versionDescriptor.versionType=branch&api-version=7.0"

$headers = New-BasicAuthHeader -Token $Pat

function Publish-WikiPageAttempt {
  param(
    [Parameter(Mandatory)][string]$ProjectSegment,  # project name or projectId
    [Parameter(Mandatory)][string]$WikiIdOrName
  )
  $baseApi = "$OrgUrl/$ProjectSegment/_apis/wiki/wikis/$WikiIdOrName"
  $getUrl  = "$baseApi/pages?path=$encodedPath&includeContent=true&$qs"
  $putUrl  = "$baseApi/pages?path=$encodedPath&$qs"

  Write-Host "GET => $getUrl"
  $get = Invoke-Req -Method GET -Uri $getUrl -Headers $headers
  Write-Host "GET status: $($get.Status)"

  $putHeaders = $headers.Clone()
  if ($get.Status -eq 200) {
    $etag = $get.Headers['ETag']
    if ($etag) {
      $etagFirst = if ($etag -is [System.Array]) { $etag[0] } else { $etag }
      $putHeaders['If-Match'] = [string]$etagFirst
    }
  }

  Write-Host "PUT => $putUrl"
  $put = Invoke-Req -Method PUT -Uri $putUrl -Headers $putHeaders -Body $bodyJson
  Write-Host "PUT status: $($put.Status)"
  if ($put.Content) { ($put.Content | Out-String) | Select-Object -First 1 | ForEach-Object { Write-Host $_ } }
  return $put.Status
}

# 1) Attempt as provided
$usedId = $WikiIdentifier
$status = Publish-WikiPageAttempt -ProjectSegment $Project -WikiIdOrName $usedId

# 2) If GUID and 404, retry with Project.wiki
if ($status -eq 404 -and ($WikiIdentifier -match '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$')) {
  $usedId = "$Project.wiki"
  Write-Host "Retrying with wiki name: $usedId"
  $status = Publish-WikiPageAttempt -ProjectSegment $Project -WikiIdOrName $usedId
}

# 3) If still failing and ProjectId provided, retry with projectId path
if ($status -ne 200 -and $status -ne 201 -and $ProjectId) {
  Write-Host "Retrying with projectId path: $ProjectId"
  $status = Publish-WikiPageAttempt -ProjectSegment $ProjectId -WikiIdOrName $usedId
}

if ($status -ne 200 -and $status -ne 201) {
  throw "Wiki publish failed with status $status. Ensure the Project Wiki exists (and is initialized), PAT has Wiki (Read & Write), and the branch label is '$WikiBranch'."
}

Write-Host "Wiki publish succeeded."
