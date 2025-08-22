param(
    [string]$Host = "localhost",
    [int]$Port = 5000,
    [int]$Threads = 10,
    [int]$Ramp = 10,
    [int]$Loop = 1
)

# Resolve tests folder next to this script
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$testsDir = Join-Path $scriptDir "jmeter"
$absPath = (Resolve-Path $testsDir).Path
# Convert to forward slashes for Docker volume mount on Windows
$dockerPath = $absPath -replace '\\','/'

$cmd = "docker run --rm -v `"$dockerPath`":/tests -w /tests justb4/jmeter:5.5 -n -t test-plan.jmx -l results.jtl -Jhost=$Host -Jport=$Port -Jthreads=$Threads -Jramp=$Ramp -Jloop=$Loop"
Write-Host "Running JMeter with: $cmd"
Invoke-Expression $cmd

Write-Host "Results written to: $(Join-Path $testsDir 'results.jtl')"
