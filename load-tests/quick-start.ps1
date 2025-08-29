# Quick Start Scripts for Load Testing
# Collection of pre-configured test scenarios

# Smoke Test - Quick validation (5 users, 30 seconds)
function Start-SmokeTest {
    Write-Host "Running Smoke Test..." -ForegroundColor Green
    .\run-load-test.ps1 -TestType locust -Users 5 -Duration 30 -OutputDir "smoke-test-results"
}

# Normal Load Test - Typical usage (50 users, 5 minutes)
function Start-NormalLoadTest {
    Write-Host "Running Normal Load Test..." -ForegroundColor Green
    .\run-load-test.ps1 -TestType both -Users 50 -Duration 300 -Html -OutputDir "normal-load-results"
}

# Stress Test - High load (100 users, 10 minutes)
function Start-StressTest {
    Write-Host "Running Stress Test..." -ForegroundColor Yellow
    Write-Warning "This will generate high load on the target system!"
    .\run-load-test.ps1 -TestType locust -Users 100 -Duration 600 -OutputDir "stress-test-results"
}

# Spike Test - Sudden load increase (200 users, 3 minutes)
function Start-SpikeTest {
    Write-Host "Running Spike Test..." -ForegroundColor Red
    Write-Warning "This will generate very high load - ensure target can handle it!"
    .\run-load-test.ps1 -TestType locust -Users 200 -Duration 180 -OutputDir "spike-test-results"
}

# Endurance Test - Long duration (25 users, 30 minutes)
function Start-EnduranceTest {
    Write-Host "Running Endurance Test..." -ForegroundColor Cyan
    Write-Host "This test will run for 30 minutes to check stability"
    .\run-load-test.ps1 -TestType locust -Users 25 -Duration 1800 -OutputDir "endurance-test-results"
}

# Development Environment Test
function Start-DevEnvironmentTest {
    Write-Host "Running Development Environment Test..." -ForegroundColor Blue
    .\run-load-test.ps1 -TestType both -Environment development -Users 30 -Duration 300 -Html
}

# Show menu for test selection
function Show-TestMenu {
    Write-Host ""
    Write-Host "=== AI Stock Trading API Load Test Menu ===" -ForegroundColor Green
    Write-Host ""
    Write-Host "1. Smoke Test (5 users, 30s)" -ForegroundColor Cyan
    Write-Host "2. Normal Load Test (50 users, 5min)" -ForegroundColor Cyan  
    Write-Host "3. Stress Test (100 users, 10min)" -ForegroundColor Yellow
    Write-Host "4. Spike Test (200 users, 3min)" -ForegroundColor Red
    Write-Host "5. Endurance Test (25 users, 30min)" -ForegroundColor Cyan
    Write-Host "6. Development Environment Test" -ForegroundColor Blue
    Write-Host "7. Custom Test (manual parameters)" -ForegroundColor Magenta
    Write-Host "0. Exit" -ForegroundColor Gray
    Write-Host ""
    
    $choice = Read-Host "Select test scenario (0-7)"
    
    switch ($choice) {
        "1" { Start-SmokeTest }
        "2" { Start-NormalLoadTest }
        "3" { 
            $confirm = Read-Host "Stress test will generate high load. Continue? (y/N)"
            if ($confirm -eq 'y' -or $confirm -eq 'Y') { Start-StressTest }
        }
        "4" { 
            $confirm = Read-Host "Spike test will generate very high load. Continue? (y/N)"
            if ($confirm -eq 'y' -or $confirm -eq 'Y') { Start-SpikeTest }
        }
        "5" { 
            $confirm = Read-Host "Endurance test will run for 30 minutes. Continue? (y/N)"
            if ($confirm -eq 'y' -or $confirm -eq 'Y') { Start-EnduranceTest }
        }
        "6" { Start-DevEnvironmentTest }
        "7" { 
            Write-Host "Use .\run-load-test.ps1 with custom parameters" -ForegroundColor Magenta
            Write-Host "Example: .\run-load-test.ps1 -TestType locust -Users 75 -Duration 600 -TargetHost myapi.com"
        }
        "0" { Write-Host "Goodbye!" -ForegroundColor Gray }
        default { 
            Write-Host "Invalid selection. Please try again." -ForegroundColor Red
            Show-TestMenu
        }
    }
}

# Export functions for module usage
Export-ModuleMember -Function Start-SmokeTest, Start-NormalLoadTest, Start-StressTest, Start-SpikeTest, Start-EnduranceTest, Start-DevEnvironmentTest, Show-TestMenu

# If script is run directly, show menu
if ($MyInvocation.InvocationName -eq '&') {
    Show-TestMenu
}
