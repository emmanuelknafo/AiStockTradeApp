# GitHub Service Connection Verification Guide

## 1. Check Service Connection in Azure DevOps Portal

### Steps:
1. **Navigate to Azure DevOps**: Go to `https://dev.azure.com/[your-organization]/[your-project]`
2. **Open Project Settings**: Click the gear icon (⚙️) in the bottom left corner
3. **Find Service Connections**: Under "Pipelines" section, click "Service connections"
4. **Locate GitHub Connection**: Find your `github.com_emmanuelknafo` service connection
5. **View Details**: Click on the service connection to see its configuration

## 2. Required Permissions for GitHub Releases

Your GitHub service connection needs these permissions:

### Required GitHub App/Token Permissions:
- ✅ **Contents**: Read and Write (for creating releases)
- ✅ **Metadata**: Read (for repository access)
- ✅ **Pull requests**: Read (for changelog generation)
- ✅ **Issues**: Read (for changelog generation)
- ⚠️ **Actions**: Read (optional, for workflow integration)

### Not Required (but may cause confusion):
- ❌ **Workflows**: Write (this was causing the original tag push error)

## 3. Service Connection Types

### GitHub App (Recommended)
- More secure and granular permissions
- Better for organization-wide access
- Can be configured with specific repository permissions

### Personal Access Token (PAT)
- Simpler setup but less secure
- Uses your personal GitHub permissions
- Requires manual token renewal

## 4. Verification Commands

### Check Current Service Connection
```bash
# In Azure DevOps CLI (if installed)
az devops service-endpoint list --organization https://dev.azure.com/[your-org] --project [your-project]
```

### Test GitHub API Access
```bash
# Test with your GitHub token (if using PAT)
curl -H "Authorization: token YOUR_GITHUB_TOKEN" \
     -H "Accept: application/vnd.github.v3+json" \
     https://api.github.com/repos/emmanuelknafo/AiStockTradeApp
```

## 5. Common Issues and Solutions

### Issue 1: "Permission denied" when creating releases
**Solution**: 
- Ensure the GitHub App has "Contents: Write" permission
- For PAT: Ensure token has `public_repo` or `repo` scope

### Issue 2: "Resource not found" error
**Solution**:
- Verify repository name in service connection matches exactly
- Check that the GitHub user/app has access to the repository

### Issue 3: Service connection test fails
**Solution**:
- Re-authorize the GitHub connection
- Check if GitHub token has expired (PAT)
- Verify GitHub App installation

## 6. Recreate Service Connection (If Needed)

If your current service connection has issues:

1. **Delete existing connection**:
   - Go to Service Connections
   - Select `github.com_emmanuelknafo`
   - Click "Delete"

2. **Create new GitHub service connection**:
   - Click "New service connection"
   - Select "GitHub"
   - Choose authentication method:
     - **GitHub App** (recommended)
     - **Personal Access Token**
   - Follow the authorization flow

3. **Update pipeline reference**:
   - Update the `endpoint` and `gitHubConnection` names in your pipeline if they change

## 7. Test the Fixed Pipeline

After verifying/fixing the service connection, test with a simple pipeline run:

```yaml
# Test task to verify GitHub connection
- task: GitHubRelease@1
  displayName: "Test GitHub Connection"
  condition: false  # Set to true to test
  inputs:
    gitHubConnection: 'github.com_emmanuelknafo'
    repositoryName: 'emmanuelknafo/AiStockTradeApp'
    action: 'create'
    target: '$(Build.SourceVersion)'
    tagSource: 'gitTag'
    tag: 'test-connection'
    title: 'Test Release'
    isDraft: true
```

## 8. Alternative: Use Azure DevOps GitHub Integration

If service connection issues persist, you can use Azure DevOps's built-in GitHub integration:

1. **Link Azure DevOps to GitHub**:
   - Install Azure Pipelines GitHub App
   - Configure repository access

2. **Use GitHub marketplace integration**:
   - More seamless permission handling
   - Automatic token management

## 9. Verify Current Setup

Run this PowerShell script to check your current configuration:

```powershell
# Check if Azure DevOps CLI is available
if (Get-Command az -ErrorAction SilentlyContinue) {
    Write-Host "Azure CLI available - checking service connections..." -ForegroundColor Green
    az devops configure --defaults organization=https://dev.azure.com/[your-org] project=[your-project]
    az devops service-endpoint list --query "[?name=='github.com_emmanuelknafo']"
} else {
    Write-Host "Install Azure CLI for automated checking: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli" -ForegroundColor Yellow
}

# Check GitHub connectivity
Write-Host "`nTesting GitHub API connectivity..." -ForegroundColor Green
try {
    $response = Invoke-RestMethod -Uri "https://api.github.com/repos/emmanuelknafo/AiStockTradeApp" -Method Get
    Write-Host "✅ Repository accessible: $($response.full_name)" -ForegroundColor Green
} catch {
    Write-Host "❌ GitHub API test failed: $($_.Exception.Message)" -ForegroundColor Red
}
```

## Next Steps

1. ✅ Verify service connection permissions in Azure DevOps portal
2. ✅ Test GitHub API access
3. ✅ Run the updated pipeline
4. ✅ Monitor for successful release creation
5. ✅ Check GitHub releases page for new releases

If issues persist, consider recreating the service connection or switching to GitHub App authentication for better permission management.
