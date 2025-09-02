# Data Protection Keys Configuration

## Overview

This document describes the implementation of persistent data protection keys to ensure **ASP.NET Core Identity** continues to function correctly when containers are restarted or replaced.

## Problem Solved

By default, ASP.NET Core stores data protection keys in memory, which means:
- Authentication cookies become invalid when containers restart
- Users are automatically logged out when containers are replaced
- Tokens and encrypted data cannot be decrypted after container recreation

## Solution Implemented

### 1. Code Changes

**UI Application (`AiStockTradeApp/Program.cs`)**:
```csharp
// Configure Data Protection for container persistence
var dataProtectionKeysPath = Environment.GetEnvironmentVariable("DATA_PROTECTION_KEYS_PATH") 
    ?? builder.Configuration["DataProtection:KeysPath"] 
    ?? "/app/keys"; // Default container path

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("AiStockTradeApp") // Must be same across all instances
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90)); // Keys valid for 90 days
```

**API Application (`AiStockTradeApp.Api/Program.cs`)**:
- Same data protection configuration as UI application
- Ensures consistency across all application components

### 2. Docker Compose Configuration

**Persistent Volumes**:
```yaml
volumes:
  ui_data_protection_keys: # Persistent volume for UI data protection keys
  api_data_protection_keys: # Persistent volume for API data protection keys
```

**Service Configuration**:
```yaml
services:
  aistocktradeapp:
    environment:
      DATA_PROTECTION_KEYS_PATH: "/app/keys"
    volumes:
      - ui_data_protection_keys:/app/keys

  AiStockTradeAppApi:
    environment:
      DATA_PROTECTION_KEYS_PATH: "/app/keys"
    volumes:
      - api_data_protection_keys:/app/keys
```

### 3. Azure App Service Configuration

**Bicep Infrastructure**:
```bicep
{
  name: 'DataProtection__KeysPath'
  value: '/home/data-protection-keys'
}
```

Azure App Service provides persistent storage at `/home/` which survives container restarts.

### 4. Local Development Configuration

**appsettings.json**:
```json
{
  "DataProtection": {
    "KeysPath": "./keys"
  }
}
```

For local development, keys are stored in a `./keys` directory relative to the application.

## Configuration Hierarchy

The application checks for data protection keys path in this order:

1. **Environment Variable**: `DATA_PROTECTION_KEYS_PATH`
2. **Configuration**: `DataProtection:KeysPath` from appsettings.json
3. **Default**: `/app/keys` (container default)

## Security Considerations

### ✅ What's Protected
- **Authentication cookies remain valid** across container restarts
- **Identity tokens can be decrypted** after container recreation
- **User sessions persist** through deployments
- **Encrypted application data** remains accessible

### 🔒 Security Best Practices
- **Keys directory excluded from version control** (`.gitignore`)
- **File system permissions** restrict access to application user only
- **Key rotation** occurs automatically every 90 days
- **Application name consistency** prevents cross-application key usage

### 🏗️ Production Recommendations

For production environments, consider these enhanced approaches:

#### Azure Key Vault (Recommended for Azure)
```csharp
builder.Services.AddDataProtection()
    .PersistKeysToAzureBlobStorage(blobClient, containerName, blobName)
    .ProtectKeysWithAzureKeyVault(keyVaultClient, keyVaultKeyId)
    .SetApplicationName("AiStockTradeApp");
```

#### Redis (Recommended for Multi-Instance)
```csharp
builder.Services.AddDataProtection()
    .PersistKeysToStackExchangeRedis(redisConnection, "DataProtection-Keys")
    .SetApplicationName("AiStockTradeApp");
```

## Testing the Implementation

### 1. Container Restart Test
```bash
# Start the application
docker-compose up -d

# Log in to the application
# Note: Create a user account and sign in

# Restart containers
docker-compose restart

# Verify: User should still be logged in
# Authentication cookies should remain valid
```

### 2. Volume Persistence Test
```bash
# Remove containers but keep volumes
docker-compose down

# Restart with same volumes
docker-compose up -d

# Verify: Previous authentication state preserved
```

### 3. Key Generation Verification
```bash
# Check that keys are being generated
docker-compose exec aistocktradeapp ls -la /app/keys
docker-compose exec AiStockTradeAppApi ls -la /app/keys

# Should see XML files containing encryption keys
```

## Troubleshooting

### Common Issues

**Problem**: Users still logged out after restart
- **Check**: Verify volumes are mounted correctly
- **Check**: Ensure `SetApplicationName` is identical across instances
- **Check**: Verify keys directory has proper permissions

**Problem**: Keys directory not created
- **Check**: Container has write permissions to the target directory
- **Check**: Path configuration is correct
- **Solution**: Add manual directory creation in Dockerfile if needed

**Problem**: Different keys between UI and API
- **Cause**: Different application names or separate key stores
- **Solution**: Ensure both applications use same `SetApplicationName`

### Monitoring

**Log Messages to Watch For**:
```
"Data protection configured with persistent keys at: /app/keys"
"Created data protection keys directory: /app/keys"
"Warning: Failed to configure persistent data protection keys"
```

## Maintenance

### Key Rotation
- **Automatic**: Keys rotate every 90 days by default
- **Manual**: Delete key files to force immediate rotation
- **Monitoring**: Monitor application logs for key generation events

### Backup Considerations
- **Docker Volumes**: Include data protection volumes in backup strategy
- **Azure**: Keys in `/home/data-protection-keys` are included in App Service backups
- **Recovery**: Restore volumes to maintain user authentication continuity

## Development vs Production

| Environment | Key Storage | Persistence | Security |
|-------------|-------------|-------------|----------|
| **Local Development** | `./keys` folder | Survives app restart | Basic file permissions |
| **Docker Compose** | Named volumes | Survives container recreation | Container isolation |
| **Azure App Service** | `/home/data-protection-keys` | Survives app restarts/deployments | Azure platform security |
| **Production (Enhanced)** | Azure Key Vault / Redis | High availability + encryption | Enterprise-grade security |

This implementation ensures that your ASP.NET Core Identity system remains functional and user-friendly even in containerized environments where instances may be frequently recreated.
