using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace AiStockTradeApp.McpServer.Middleware;

/// <summary>
/// Telemetry initializer to add custom properties to all Application Insights telemetry.
/// </summary>
public class McpTelemetryInitializer : ITelemetryInitializer
{
    public void Initialize(ITelemetry telemetry)
    {
        // Add common properties to all telemetry
        telemetry.Context.GlobalProperties.TryAdd("ServiceName", "AiStockTradeApp.McpServer");
        telemetry.Context.GlobalProperties.TryAdd("ServiceVersion", GetAssemblyVersion());
        telemetry.Context.GlobalProperties.TryAdd("Protocol", "MCP");
        telemetry.Context.GlobalProperties.TryAdd("Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development");
        
        // Add deployment information if available
        var deploymentId = Environment.GetEnvironmentVariable("WEBSITE_DEPLOYMENT_ID") ?? 
                          Environment.GetEnvironmentVariable("CONTAINER_APP_REVISION");
        if (!string.IsNullOrEmpty(deploymentId))
        {
            telemetry.Context.GlobalProperties.TryAdd("DeploymentId", deploymentId);
        }

        // Add container/instance information
        var instanceId = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") ?? 
                        Environment.GetEnvironmentVariable("CONTAINER_APP_REPLICA_NAME") ??
                        Environment.MachineName;
        if (!string.IsNullOrEmpty(instanceId))
        {
            telemetry.Context.GlobalProperties.TryAdd("InstanceId", instanceId);
        }

        // Add Azure region if available
        var region = Environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP") ?? 
                    Environment.GetEnvironmentVariable("AZURE_REGION");
        if (!string.IsNullOrEmpty(region))
        {
            telemetry.Context.GlobalProperties.TryAdd("Region", region);
        }
    }

    private static string GetAssemblyVersion()
    {
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }
}
