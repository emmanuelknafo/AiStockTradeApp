# Azure Resource Naming Convention Updates

## Summary

Updated the Bicep infrastructure template to follow Microsoft Cloud Adoption Framework (CAF) best practices for Azure resource naming conventions.

## Changes Made

### 1. Resource Name Abbreviations (Prefixes instead of Suffixes)

Following the [official Azure resource abbreviations guide](https://learn.microsoft.com/en-us/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations), updated all resource names to use standard prefixes:

| Resource Type | Old Suffix | New Prefix | Example |
|---------------|------------|------------|---------|
| App Service Plan | `-asp` | `asp-` | `asp-ai-stock-tracker-dev-001` |
| Web App | `-webapp` | `app-` | `app-ai-stock-tracker-dev-001` |
| Container Registry | (suffix) | `cr` | `craistocktrackrdev001{uniqueString}` |
| Key Vault | `-kv` | `kv-` | `kv-ai-stock-tracker-dev-001` |
| Application Insights | `-ai` | `appi-` | `appi-ai-stock-tracker-dev-001` |
| Log Analytics Workspace | `-law` | `log-` | `log-ai-stock-tracker-dev-001` |

### 2. Instance Number Parameter

Added a new parameter `instanceNumber` with default value `"001"` to support multiple instances of the same resource type:

```bicep
@description('Instance number for resource differentiation')
param instanceNumber string = '001'
```

### 3. Updated Naming Pattern

The new naming pattern follows CAF recommendations:
```
{resource-abbreviation}-{app-name}-{environment}-{instance-number}
```

For container registry (special case due to naming restrictions):
```
cr{containerregistryname}{environment}{instance}{uniquestring}
```

### 4. Parameter Files Updated

Both `parameters.dev.json` and `parameters.prod.json` have been updated to include the new `instanceNumber` parameter with value `"001"`.

## Benefits

1. **Compliance**: Follows Microsoft Cloud Adoption Framework standards
2. **Consistency**: Standardized naming across all Azure resources
3. **Scalability**: Instance numbers allow for multiple deployments
4. **Clarity**: Resource type is immediately identifiable from the prefix
5. **Automation-Friendly**: Consistent patterns enable better automation scripts

## Example Resource Names

With parameters:
- `appName`: "ai-stock-tracker"
- `environment`: "dev"
- `instanceNumber`: "001"

Generated resource names:
- App Service Plan: `asp-ai-stock-tracker-dev-001`
- Web App: `app-ai-stock-tracker-dev-001`
- Key Vault: `kv-ai-stock-tracker-dev-001`
- Application Insights: `appi-ai-stock-tracker-dev-001`
- Log Analytics: `log-ai-stock-tracker-dev-001`
- Container Registry: `craistocktrackrdev001abc123def` (includes unique string)

## Validation

- ✅ Bicep template compiles without errors
- ✅ All resource naming constraints satisfied
- ✅ Parameter files updated and validated
- ✅ Follows Microsoft CAF guidelines
