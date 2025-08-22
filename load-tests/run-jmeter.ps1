param(
    # Use TargetHost to avoid clashing with PowerShell's built-in $Host variable.
    [Alias('Host')][string]$TargetHost = "localhost",
    [int]$Port = 5000,
    [int]$Threads = 10,
    [int]$Ramp = 10,
    [int]$Loop = 1,
    [string]$Protocol = "http",
    [string]$Path = "/health",
    [string]$JMeterImage = "justb4/jmeter:5.5",
    [switch]$GenerateReport
)

# Resolve tests folder next to this script
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$testsDir = Join-Path $scriptDir "jmeter"
$absPath = (Resolve-Path $testsDir).Path
# Convert to forward slashes for Docker volume mount on Windows
$dockerPath = ($absPath -replace '\\','/').Trim()

# Generate concrete JMX from template (replace placeholders)
$template = Join-Path $testsDir "test-plan.jmx"
$generated = Join-Path $testsDir "test-plan.generated.jmx"
if (-Not (Test-Path $template)) {
    Write-Error "Template not found: $template"
    exit 1
}

$content = Get-Content $template -Raw
$content = $content -replace '@@THREADS@@', $Threads.ToString()
$content = $content -replace '@@RAMP@@', $Ramp.ToString()
$content = $content -replace '@@LOOP@@', $Loop.ToString()
Set-Content -Path $generated -Value $content -Encoding UTF8

${null} = $LASTEXITCODE
# Remove previous results file to avoid mixing prior runs
$resultsFile = Join-Path $testsDir 'results.jtl'
if (Test-Path $resultsFile) {
    try { Remove-Item -LiteralPath $resultsFile -Force -ErrorAction Stop } catch { Write-Warning "Failed to remove existing results file: $resultsFile. $_" }
}
$runArgs = @(
    'run','--rm',
    '-v',"${dockerPath}:/tests",
    '-w','/tests',
    $JMeterImage,
    '-n','-t','test-plan.generated.jmx','-l','results.jtl',
    "-Jhost=$TargetHost","-Jport=$Port","-Jprotocol=$Protocol","-Jpath=$Path"
)
Write-Host "Running JMeter with: docker $($runArgs -join ' ')"
& docker @runArgs
if ($LASTEXITCODE -ne 0) { Write-Error "JMeter run failed with exit code $LASTEXITCODE"; exit $LASTEXITCODE }

if ($GenerateReport.IsPresent) {
    ${null} = $LASTEXITCODE
    # Ensure previous report directory does not exist (JMeter requires empty/non-existing output dir)
    $reportDir = Join-Path $testsDir 'report'
    if (Test-Path $reportDir) {
        try {
            Remove-Item -Recurse -Force -LiteralPath $reportDir -ErrorAction Stop
        } catch {
            Write-Warning "Failed to remove existing report directory at '$reportDir'. Attempting to proceed may fail. Error: $($_.Exception.Message)"
        }
    }

    # Validate results file exists before generating report
    $resultsFile = Join-Path $testsDir 'results.jtl'
    if (-not (Test-Path $resultsFile)) {
        Write-Error "Results file not found: $resultsFile. Run did not produce results or wrong path."
        exit 1
    }

    $reportArgs = @(
        'run','--rm',
        '-v',"${dockerPath}:/tests",
        '-w','/tests',
        $JMeterImage,
        '-g','results.jtl','-o','report'
    )
    Write-Host "Generating HTML report with: docker $($reportArgs -join ' ')"
    & docker @reportArgs
    if ($LASTEXITCODE -ne 0) { Write-Error "Report generation failed with exit code $LASTEXITCODE"; exit $LASTEXITCODE }
    Write-Host "Report available at: $testsDir\report"
}

Write-Host "Results written to: $(Join-Path $testsDir 'results.jtl')"
