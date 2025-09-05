using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AiStockTradeApp.Api;

internal static class ApiKeyDiagnostics
{
    private static readonly ConcurrentDictionary<string, SecretClient> _clients = new();

    private static string Mask(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        if (raw.Contains("@Microsoft.KeyVault", StringComparison.OrdinalIgnoreCase)) return "[unresolved-keyvault]"; // Explicit marker for unresolved references
        if (raw.Length <= 4) return "***";
        return raw[..2] + new string('*', Math.Max(0, raw.Length - 4)) + raw[^2..];
    }

    private static SecretClient GetClient(string vaultName, TokenCredential credential)
        => _clients.GetOrAdd(vaultName, vn => new SecretClient(new Uri($"https://{vn}.vault.azure.net/"), credential));

    private static async Task<string?> ResolveIfKeyVaultReferenceAsync(string? raw, ILogger logger, TokenCredential credential)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(raw) || !raw.Contains("@Microsoft.KeyVault", StringComparison.OrdinalIgnoreCase))
                return raw;

            var token = raw.Trim();
            string? vaultName = null;
            string? secretName = null;

            var inside = token[(token.IndexOf('(') + 1)..].TrimEnd(')');
            var parts = inside.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                var kv = part.Split('=', 2, StringSplitOptions.TrimEntries);
                if (kv.Length != 2) continue;
                var key = kv[0];
                var value = kv[1];
                if (key.Equals("VaultName", StringComparison.OrdinalIgnoreCase)) vaultName = value;
                else if (key.Equals("SecretName", StringComparison.OrdinalIgnoreCase)) secretName = value;
                else if (key.Equals("SecretUri", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var uri = new Uri(value);
                        vaultName = uri.Host.Split('.')[0];
                        var segs = uri.Segments.Select(s => s.Trim('/')).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                        var idx = Array.FindIndex(segs, s => s.Equals("secrets", StringComparison.OrdinalIgnoreCase));
                        if (idx >= 0 && idx + 1 < segs.Length) secretName = segs[idx + 1];
                    }
                    catch { }
                }
            }

            if (string.IsNullOrWhiteSpace(vaultName) || string.IsNullOrWhiteSpace(secretName))
            {
                logger.LogWarning("Failed to parse Key Vault reference token: {Token}", token);
                return raw;
            }

            var client = GetClient(vaultName!, credential);
            var secret = await client.GetSecretAsync(secretName);
            return secret?.Value?.Value ?? raw;
        }
        catch (Azure.RequestFailedException ex)
        {
            logger.LogWarning(ex, "Key Vault request failed when resolving API key reference.");
            return raw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unexpected error resolving Key Vault reference.");
            return raw;
        }
    }

    private static string Classify(string? rawOriginal, string? resolved, string placeholder, bool treatDemo = false)
    {
        var rawHadKv = !string.IsNullOrWhiteSpace(rawOriginal) && rawOriginal.Contains("@Microsoft.KeyVault", StringComparison.OrdinalIgnoreCase);
        var resolvedStillKv = !string.IsNullOrWhiteSpace(resolved) && resolved.Contains("@Microsoft.KeyVault", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(resolved))
        {
            if (string.IsNullOrWhiteSpace(rawOriginal)) return "missing";
            return rawHadKv ? "unresolved-keyvault" : "missing";
        }

        if (resolvedStillKv)
        {
            // Failed to resolve (we got the same token back)
            return "unresolved-keyvault";
        }

        if (resolved == placeholder) return "placeholder";
        if (treatDemo && string.Equals(resolved, "demo", StringComparison.OrdinalIgnoreCase)) return "demo";
        return "configured";
    }

    public static async Task<object> GetStatusAsync(IConfiguration config, ILogger logger, TokenCredential? sharedCredential = null)
    {
        var alphaRawOriginal = config["AlphaVantage:ApiKey"];
        var twelveRawOriginal = config["TwelveData:ApiKey"];

        var credential = sharedCredential ?? new DefaultAzureCredential();
        var alphaResolved = alphaRawOriginal?.Contains("@Microsoft.KeyVault", StringComparison.OrdinalIgnoreCase) == true
            ? await ResolveIfKeyVaultReferenceAsync(alphaRawOriginal, logger, credential)
            : alphaRawOriginal;
        var twelveResolved = twelveRawOriginal?.Contains("@Microsoft.KeyVault", StringComparison.OrdinalIgnoreCase) == true
            ? await ResolveIfKeyVaultReferenceAsync(twelveRawOriginal, logger, credential)
            : twelveRawOriginal;

        return new
        {
            alphaVantage = new
            {
                status = Classify(alphaRawOriginal, alphaResolved, "YOUR_ALPHA_VANTAGE_API_KEY"),
                masked = Mask(alphaResolved)
            },
            twelveData = new
            {
                status = Classify(twelveRawOriginal, twelveResolved, "YOUR_TWELVE_DATA_API_KEY", treatDemo: true),
                masked = Mask(twelveResolved)
            }
        };
    }
}