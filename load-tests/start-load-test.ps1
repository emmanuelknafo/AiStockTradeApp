# Simple Load Test Launcher
# Quick shortcuts for common load testing scenarios

param(
    [string]$Scenario = "menu"    # menu, quick, smoke, normal, stress
)

$ErrorActionPreference = "Stop"

function Show-QuickMenu {
    Clear-Host
    Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║               🚀 AI Stock Trading API Load Test              ║" -ForegroundColor Cyan
    Write-Host "╠══════════════════════════════════════════════════════════════╣" -ForegroundColor Cyan
    Write-Host "║                        Quick Launcher                       ║" -ForegroundColor Cyan
    Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Select a test scenario:" -ForegroundColor White
    Write-Host ""
    Write-Host "  1️⃣  Quick Test      (10 users, 1 min)   - Fast validation" -ForegroundColor Green
    Write-Host "  2️⃣  Smoke Test      (5 users, 30 sec)  - Basic functionality" -ForegroundColor Cyan
    Write-Host "  3️⃣  Normal Test     (50 users, 5 min)  - Typical load" -ForegroundColor Yellow
    Write-Host "  4️⃣  Stress Test     (100 users, 10 min) - High load" -ForegroundColor Magenta
    Write-Host "  5️⃣  Custom Test     (specify params)    - Your settings" -ForegroundColor Blue
    Write-Host ""
    Write-Host "  0️⃣  Exit" -ForegroundColor Gray
    Write-Host ""
    
    $choice = Read-Host "Enter your choice (0-5)"
    
    switch ($choice) {
        "1" { 
            Write-Host "🚀 Starting Quick Test..." -ForegroundColor Green
            .\run-load-test.ps1 -TestType locust -Users 10 -Duration 60 -Html
        }
        "2" { 
            Write-Host "💨 Starting Smoke Test..." -ForegroundColor Cyan
            .\run-load-test.ps1 -TestType locust -Users 5 -Duration 30 -Html
        }
        "3" { 
            Write-Host "⚖️ Starting Normal Test..." -ForegroundColor Yellow
            .\run-load-test.ps1 -TestType both -Users 50 -Duration 300 -Html
        }
        "4" { 
            Write-Host "💪 Starting Stress Test..." -ForegroundColor Magenta
            Write-Warning "This will generate high load!"
            $confirm = Read-Host "Continue? (y/N)"
            if ($confirm -eq 'y' -or $confirm -eq 'Y') {
                .\run-load-test.ps1 -TestType locust -Users 100 -Duration 600 -Html
            }
        }
        "5" { 
            Write-Host "🎛️ Custom Test Configuration..." -ForegroundColor Blue
            $users = Read-Host "Number of users (default: 50)"
            $duration = Read-Host "Duration in seconds (default: 300)"
            $testType = Read-Host "Test type [locust/jmeter/both] (default: locust)"
            
            $users = if ($users) { [int]$users } else { 50 }
            $duration = if ($duration) { [int]$duration } else { 300 }
            $testType = if ($testType) { $testType } else { "locust" }
            
            .\run-load-test.ps1 -TestType $testType -Users $users -Duration $duration -Html
        }
        "0" { 
            Write-Host "Goodbye! 👋" -ForegroundColor Gray
            exit 0
        }
        default { 
            Write-Host "❌ Invalid choice. Please try again." -ForegroundColor Red
            Start-Sleep 2
            Show-QuickMenu
        }
    }
}

function Start-QuickTest {
    Write-Host "🚀 Running Quick Load Test (10 users, 1 minute)..." -ForegroundColor Green
    .\run-load-test.ps1 -TestType locust -Users 10 -Duration 60 -Html
}

function Start-SmokeTest {
    Write-Host "💨 Running Smoke Test (5 users, 30 seconds)..." -ForegroundColor Cyan
    .\run-load-test.ps1 -TestType locust -Users 5 -Duration 30 -Html
}

function Start-NormalTest {
    Write-Host "⚖️ Running Normal Load Test (50 users, 5 minutes)..." -ForegroundColor Yellow
    .\run-load-test.ps1 -TestType both -Users 50 -Duration 300 -Html
}

function Start-StressTest {
    Write-Host "💪 Running Stress Test (100 users, 10 minutes)..." -ForegroundColor Magenta
    Write-Warning "This will generate high load on the target system!"
    .\run-load-test.ps1 -TestType locust -Users 100 -Duration 600 -Html
}

# Main execution
try {
    switch ($Scenario.ToLower()) {
        "menu" { Show-QuickMenu }
        "quick" { Start-QuickTest }
        "smoke" { Start-SmokeTest }
        "normal" { Start-NormalTest }
        "stress" { Start-StressTest }
        default { 
            Write-Host "Unknown scenario: $Scenario" -ForegroundColor Red
            Write-Host "Available scenarios: menu, quick, smoke, normal, stress" -ForegroundColor Yellow
            Show-QuickMenu
        }
    }
}
catch {
    Write-Error "Failed to run load test: $($_.Exception.Message)"
    Write-Host ""
    Write-Host "💡 Troubleshooting tips:" -ForegroundColor Cyan
    Write-Host "• Make sure you're in the load-tests directory" -ForegroundColor Gray
    Write-Host "• Ensure the API is running (use -StartApi if needed)" -ForegroundColor Gray
    Write-Host "• Check that Python and Locust are installed" -ForegroundColor Gray
    exit 1
}
