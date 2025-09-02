# GitHub Service Connection Verification Script
# This script helps verify your Azure DevOps GitHub service connection

param(
    [string]$Organization = "",
    [string]$Project = "",
    [string]$ServiceConnectionName = "github.com_emmanuelknafo"
)

Write-Host "=== GitHub Service Connection Verification ===" -ForegroundColor Green
Write-Host ""

# Function to test GitHub API connectivity
function Test-GitHubConnectivity {
    Write-Host "Testing GitHub API connectivity..." -ForegroundColor Yellow
    try {
        $response = Invoke-RestMethod -Uri "https://api.github.com/repos/emmanuelknafo/AiStockTradeApp" -Method Get
        Write-Host "✅ Repository accessible: $($response.full_name)" -ForegroundColor Green
        Write-Host "   - Default branch: $($response.default_branch)" -ForegroundColor Cyan
        Write-Host "   - Visibility: $($response.visibility)" -ForegroundColor Cyan
        return $true
    } catch {
        Write-Host "❌ GitHub API test failed: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Function to check Azure CLI availability
function Test-AzureCLI {
    Write-Host "Checking Azure CLI availability..." -ForegroundColor Yellow
    if (Get-Command az -ErrorAction SilentlyContinue) {
        Write-Host "✅ Azure CLI is available" -ForegroundColor Green
        $version = az version --query '"azure-cli"' --output tsv 2>$null
        Write-Host "   Version: $version" -ForegroundColor Cyan
        return $true
    } else {
        Write-Host "❌ Azure CLI not found" -ForegroundColor Red
        Write-Host "   Install from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli" -ForegroundColor Yellow
        return $false
    }
}

# Function to check DevOps extension
function Test-DevOpsExtension {
    Write-Host "Checking Azure DevOps CLI extension..." -ForegroundColor Yellow
    try {
        $extensions = az extension list --query "[?name=='azure-devops'].name" --output tsv 2>$null
        if ($extensions -contains "azure-devops") {
            Write-Host "✅ Azure DevOps extension is installed" -ForegroundColor Green
            return $true
        } else {
            Write-Host "❌ Azure DevOps extension not installed" -ForegroundColor Red
            Write-Host "   Install with: az extension add --name azure-devops" -ForegroundColor Yellow
            return $false
        }
    } catch {
        Write-Host "❌ Could not check DevOps extension: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Main verification steps
Write-Host "1. Testing basic connectivity..." -ForegroundColor Cyan
$githubOk = Test-GitHubConnectivity

Write-Host "`n2. Checking Azure CLI..." -ForegroundColor Cyan
$azureCliOk = Test-AzureCLI

if ($azureCliOk) {
    Write-Host "`n3. Checking Azure DevOps extension..." -ForegroundColor Cyan
    $devopsExtOk = Test-DevOpsExtension
    
    if ($devopsExtOk) {
        Write-Host "`n4. Manual steps to verify service connection:" -ForegroundColor Cyan
        Write-Host "   Run these commands to check your service connection:" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "   # Login to Azure DevOps" -ForegroundColor White
        Write-Host "   az login" -ForegroundColor Gray
        Write-Host ""
        Write-Host "   # Set default organization and project" -ForegroundColor White
        Write-Host "   az devops configure --defaults organization=https://dev.azure.com/[your-org] project=[your-project]" -ForegroundColor Gray
        Write-Host ""
        Write-Host "   # List service connections" -ForegroundColor White
        Write-Host "   az devops service-endpoint list --query `"[?name=='$ServiceConnectionName']`"" -ForegroundColor Gray
        Write-Host ""
    }
}

Write-Host "`n5. Manual verification steps in Azure DevOps portal:" -ForegroundColor Cyan
Write-Host "   1. Go to: https://dev.azure.com/[your-organization]/[your-project]" -ForegroundColor Yellow
Write-Host "   2. Click Project Settings (gear icon, bottom left)" -ForegroundColor Yellow
Write-Host "   3. Under Pipelines, click 'Service connections'" -ForegroundColor Yellow
Write-Host "   4. Find '$ServiceConnectionName' and click on it" -ForegroundColor Yellow
Write-Host "   5. Click 'Verify' or 'Test connection'" -ForegroundColor Yellow
Write-Host "   6. Check permissions in GitHub:" -ForegroundColor Yellow
Write-Host "      - Contents: Read and Write" -ForegroundColor Gray
Write-Host "      - Metadata: Read" -ForegroundColor Gray
Write-Host "      - Pull requests: Read" -ForegroundColor Gray

Write-Host "`n6. Test the pipeline change:" -ForegroundColor Cyan
Write-Host "   The updated pipeline now uses GitHubRelease@1 task instead of manual git push" -ForegroundColor Yellow
Write-Host "   This should resolve the 'workflows permission' error you encountered" -ForegroundColor Yellow

Write-Host "`n=== Summary ===" -ForegroundColor Green
Write-Host "GitHub API accessible: $(if($githubOk){'✅'}else{'❌'})" -ForegroundColor $(if($githubOk){'Green'}else{'Red'})
Write-Host "Azure CLI available: $(if($azureCliOk){'✅'}else{'❌'})" -ForegroundColor $(if($azureCliOk){'Green'}else{'Red'})

if ($azureCliOk -and $devopsExtOk) {
    Write-Host "`nNext: Use Azure CLI commands above to check service connection details" -ForegroundColor Cyan
} else {
    Write-Host "`nNext: Use Azure DevOps portal steps above to verify service connection" -ForegroundColor Cyan
}

Write-Host "`nThe pipeline has been updated to use GitHubRelease@1 task which should work better!" -ForegroundColor Green
