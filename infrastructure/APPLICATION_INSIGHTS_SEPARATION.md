# Application Insights Separation Changes

## Overview
Updated the infrastructure to deploy **separate Application Insights instances** for the UI and API applications instead of sharing a single instance.

## Changes Made

### 1. Bicep Template (`main.bicep`)

#### Variables Updated
- Changed from single `applicationInsightsName` to separate naming:
  ```bicep
  var applicationInsightsUiName = 'appi-ui-${resourceNamePrefix}-${instanceNumber}'
  var applicationInsightsApiName = 'appi-api-${resourceNamePrefix}-${instanceNumber}'
  ```

#### Resources Created
- **Application Insights UI**: `applicationInsightsUI` resource for the web application
- **Application Insights API**: `applicationInsightsAPI` resource for the API application
- Both instances share the same Log Analytics Workspace for unified logging

#### Application Configuration
- **UI Web App** (`webApp`): Uses `applicationInsightsUI` connection string and instrumentation key
- **API Web App** (`webApi`): Uses `applicationInsightsAPI` connection string and instrumentation key

#### Outputs Added
- `applicationInsightsUiName`: Name of the UI Application Insights instance
- `applicationInsightsApiName`: Name of the API Application Insights instance  
- `applicationInsightsUiConnectionString`: Connection string for UI telemetry
- `applicationInsightsApiConnectionString`: Connection string for API telemetry

### 2. Benefits of Separation

#### Improved Monitoring
- **Isolated telemetry**: UI and API metrics are separated for clearer analysis
- **Component-specific dashboards**: Create focused dashboards for each application layer
- **Independent alerting**: Set up different alert thresholds for UI vs API performance

#### Security & Compliance
- **Principle of least privilege**: Each application only accesses its own telemetry data
- **Data segregation**: Separate billing and access control for different application tiers
- **Audit compliance**: Easier to track which component generated specific telemetry

#### Operational Excellence
- **Debugging efficiency**: Quickly identify if issues are UI-related or API-related
- **Performance tuning**: Independent performance baselines for each component
- **Resource planning**: Separate usage patterns for UI vs API workloads

### 3. Resource Naming Convention

| Component | Resource Name Pattern | Example |
|-----------|----------------------|---------|
| UI Application Insights | `appi-ui-{appName}-{environment}-{instanceNumber}` | `appi-ui-aistock-dev-002` |
| API Application Insights | `appi-api-{appName}-{environment}-{instanceNumber}` | `appi-api-aistock-dev-002` |
| Shared Log Analytics | `log-{appName}-{environment}-{instanceNumber}` | `log-aistock-dev-002` |

### 4. Deployment Impact

#### No Breaking Changes
- Existing deployments will seamlessly upgrade to the new structure
- Environment variables remain the same (`APPLICATIONINSIGHTS_CONNECTION_STRING`)
- Application code requires no modifications

#### New Deployments
- Two Application Insights instances will be created instead of one
- Each application receives its dedicated connection string
- Shared Log Analytics Workspace for unified log correlation

### 5. Monitoring & Dashboards

#### Recommended Dashboard Setup
1. **Unified Dashboard**: Combine metrics from both instances for overall application health
2. **UI-Specific Dashboard**: Focus on page load times, user interactions, client-side errors
3. **API-Specific Dashboard**: Monitor request rates, response times, dependency calls, server errors

#### Query Examples
```kusto
// Cross-component correlation using shared Log Analytics
union 
    app('appi-ui-aistock-dev-002').requests,
    app('appi-api-aistock-dev-002').requests
| where timestamp > ago(1h)
| project timestamp, appName, name, duration, resultCode
```

### 6. Migration Notes

#### For Existing Environments
- Old single Application Insights instance can be safely removed after new deployment
- Historical data in the old instance remains accessible
- Consider data export if long-term historical analysis is needed

#### For Development Teams
- Update any hardcoded Application Insights references in documentation
- Review monitoring alerts and update them to use appropriate instance
- Update any custom dashboards to reference the new resource names

## Conclusion

This separation provides better observability, security, and operational clarity while maintaining backward compatibility with existing application code and deployment processes.
