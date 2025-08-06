# Cleanup Script for Misplaced Azure Resources
# Resources were deployed to rg-ai-stock-tracker-prod instead of rg-aistock-prod-002

Write-Host "=== Cleaning up misplaced resources ===" -ForegroundColor Yellow
Write-Host "Resources in wrong RG: rg-ai-stock-tracker-prod"
Write-Host "Should be in: rg-aistock-prod-002"

# Set variables
$OLD_RG = "rg-ai-stock-tracker-prod"
$NEW_RG = "rg-aistock-prod-002"
$INSTANCE_NUM = "002"

Write-Host "=== Step 1: List resources in old resource group ===" -ForegroundColor Cyan
az resource list --resource-group $OLD_RG --output table

Write-Host "=== Step 2: Delete specific misplaced resources ===" -ForegroundColor Cyan
Write-Host "Deleting resources one by one to avoid dependency issues..."

Write-Host "Deleting Web App..." -ForegroundColor Green
az webapp delete --name "app-aistock-prod-$INSTANCE_NUM" --resource-group $OLD_RG

Write-Host "Deleting App Service Plan..." -ForegroundColor Green
az appservice plan delete --name "asp-aistock-prod-$INSTANCE_NUM" --resource-group $OLD_RG --yes

Write-Host "Deleting Application Insights..." -ForegroundColor Green
az monitor app-insights component delete --app "appi-aistock-prod-$INSTANCE_NUM" --resource-group $OLD_RG

Write-Host "Deleting Key Vault..." -ForegroundColor Green
az keyvault delete --name "kv-aistock-prod-$INSTANCE_NUM" --resource-group $OLD_RG

Write-Host "Deleting Log Analytics Workspace..." -ForegroundColor Green
az monitor log-analytics workspace delete --workspace-name "log-aistock-prod-$INSTANCE_NUM" --resource-group $OLD_RG --yes

Write-Host "=== Step 3: Verify cleanup ===" -ForegroundColor Cyan
az resource list --resource-group $OLD_RG --output table

Write-Host "=== Step 4: Check if resource group is empty ===" -ForegroundColor Cyan
$resourceCount = (az resource list --resource-group $OLD_RG --query 'length(@)' --output tsv)
if ($resourceCount -eq 0) {
    Write-Host "Resource group is empty, you can delete it manually if needed:" -ForegroundColor Green
    Write-Host "az group delete --name $OLD_RG --yes --no-wait" -ForegroundColor Yellow
} else {
    Write-Host "Resource group still contains $resourceCount resources. Manual review needed." -ForegroundColor Yellow
}

Write-Host "=== Cleanup completed ===" -ForegroundColor Green
Write-Host "Now you can redeploy to the correct resource group: $NEW_RG" -ForegroundColor Green
