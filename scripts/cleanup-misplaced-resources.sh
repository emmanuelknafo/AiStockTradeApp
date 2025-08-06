# Cleanup Script for Misplaced Azure Resources
# Resources were deployed to rg-ai-stock-tracker-prod instead of rg-aistock-prod-002

# Clean up resources in the wrong resource group
echo "=== Cleaning up misplaced resources ==="
echo "Resources in wrong RG: rg-ai-stock-tracker-prod"
echo "Should be in: rg-aistock-prod-002"

# Set variables
OLD_RG="rg-ai-stock-tracker-prod"
NEW_RG="rg-aistock-prod-002"
INSTANCE_NUM="002"

echo "=== Step 1: List resources in old resource group ==="
az resource list --resource-group $OLD_RG --output table

echo "=== Step 2: Delete specific misplaced resources ==="
echo "Deleting resources one by one to avoid dependency issues..."

echo "Deleting Web App..."
az webapp delete --name "app-aistock-prod-${INSTANCE_NUM}" --resource-group $OLD_RG

echo "Deleting App Service Plan..."
az appservice plan delete --name "asp-aistock-prod-${INSTANCE_NUM}" --resource-group $OLD_RG --yes

echo "Deleting Application Insights..."
az monitor app-insights component delete --app "appi-aistock-prod-${INSTANCE_NUM}" --resource-group $OLD_RG

echo "Deleting Key Vault..."
az keyvault delete --name "kv-aistock-prod-${INSTANCE_NUM}" --resource-group $OLD_RG

echo "Deleting Log Analytics Workspace..."
az monitor log-analytics workspace delete --workspace-name "log-aistock-prod-${INSTANCE_NUM}" --resource-group $OLD_RG --yes

echo "=== Step 3: Verify cleanup ==="
az resource list --resource-group $OLD_RG --output table

echo "=== Step 4: Delete empty resource group (if empty) ==="
RESOURCE_COUNT=$(az resource list --resource-group $OLD_RG --query 'length(@)')
if [ "$RESOURCE_COUNT" -eq 0 ]; then
    echo "Resource group is empty, deleting..."
    az group delete --name $OLD_RG --yes --no-wait
else
    echo "Resource group still contains $RESOURCE_COUNT resources. Manual review needed."
fi

echo "=== Cleanup completed ==="
echo "Now you can redeploy to the correct resource group: $NEW_RG"
