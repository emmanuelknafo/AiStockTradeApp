# Auto-Versioning Enhancement for MCP Server

## 🎯 Problem Solved

The GitHub Actions workflow and Azure DevOps pipeline were not automatically incrementing versions when the MCP Server project was modified. Both pipelines would use the same version (`1.0.0-beta`) repeatedly, causing conflicts and making it difficult to track changes.

## 🚀 Solution Implemented

### **Intelligent Change Detection**

Both pipelines now automatically detect changes in the MCP Server project since the last release:

1. **Change Detection Logic**: 
   - Compares current code with the last tagged release (`mcpserver-v*`)
   - Analyzes file types to determine impact level
   - Skips publishing when no changes are detected

2. **Change Classification**:
   - **Code Changes** (`.cs` files) → Minor version increment
   - **Configuration Changes** (`.csproj`, `.json`) → Patch version increment
   - **Documentation/Other** → Patch version increment

### **Smart Version Incrementing**

#### **Version Format Support**:
- **Pre-release with build**: `1.0.0-beta.1` → `1.0.0-beta.2`
- **Pre-release without build**: `1.0.0-beta` → `1.0.0-beta.1`
- **Release version**: `1.0.0` → `1.0.1` (patch) or `1.1.0` (minor)

#### **Increment Rules**:
```
Code Changes (.cs files):     1.0.0 → 1.1.0
Config Changes (.csproj):     1.0.0 → 1.0.1
Beta Versions:                1.0.0-beta → 1.0.0-beta.1
Beta with Build:              1.0.0-beta.1 → 1.0.0-beta.2
```

## 🔧 Implementation Details

### **GitHub Actions Workflow**

**Enhanced Steps**:
1. **Check for MCP Server changes** - Compares with last release tag
2. **Classify change severity** - Determines increment type based on file types
3. **Auto-increment version** - Updates version based on detected changes
4. **Commit version changes** - Pushes updated versions back to repo
5. **Skip if no changes** - Avoids unnecessary publishes

**Key Features**:
```yaml
- name: Check for MCP Server changes
  id: changes
  run: |
    # Find last release tag
    LAST_TAG=$(git describe --tags --abbrev=0 --match="mcpserver-v*" 2>/dev/null || echo "")
    
    # Check for changes since last tag
    CHANGES=$(git diff --name-only $LAST_TAG HEAD -- AiStockTradeApp.McpServer/)
    
    # Determine increment type based on file types
    if echo "$CHANGES" | grep -q "\.cs$"; then
      echo "increment_type=minor" >> $GITHUB_OUTPUT
    else
      echo "increment_type=patch" >> $GITHUB_OUTPUT
    fi
```

### **Azure DevOps Pipeline**

**Enhanced PowerShell Tasks**:
1. **Change Detection Task** - Analyzes git history for MCP Server changes
2. **Version Management Task** - Auto-increments based on change analysis
3. **Conditional Publishing** - Only publishes when changes are detected

**Key Features**:
```powershell
# Check for changes in MCP Server directory since last tag
$changes = git diff --name-only $lastTag HEAD -- AiStockTradeApp.McpServer/

# Determine change severity
$hasCodeChanges = $changes | Where-Object { $_ -match '\.cs$' }
if ($hasCodeChanges) {
    Write-Host "##vso[task.setvariable variable=IncrementType]minor"
} else {
    Write-Host "##vso[task.setvariable variable=IncrementType]patch"
}
```

## 📊 Version Management

### **File Updates**

When versions are auto-incremented, the following files are updated:

1. **Project File**: `AiStockTradeApp.McpServer.csproj`
   ```xml
   <PackageVersion>1.0.0-beta.1</PackageVersion>
   ```

2. **MCP Configuration Files**:
   - `AiStockTradeApp.McpServer/.mcp/server-nuget.json` (GitHub)
   - `AiStockTradeApp.McpServer/.mcp/server-azuredevops.json` (Azure DevOps)
   - `AiStockTradeApp.McpServer/.mcp/server.json` (if exists)

3. **Git Commits**: Automatic commits with `[skip ci]` to prevent recursive builds

### **Dual Publication Strategy**

| Pipeline | Target | Package Name | Version Source |
|----------|--------|--------------|----------------|
| **GitHub Actions** | NuGet.org | `devopsabcs.AiStockTradeMcpServer` | `server-nuget.json` |
| **Azure DevOps** | Private Feed | `MngEnvMCAP675646.AiStockTradeApp.McpServer` | `server-azuredevops.json` |

## 🛡️ Safety Features

### **Skip Conditions**
- **No Changes**: Pipeline skips when no MCP Server files modified
- **Duplicate Prevention**: Checks existing packages before publishing
- **Force Override**: Manual parameter to force publish regardless

### **Error Handling**
- **Git History Failures**: Falls back to "changed" assumption for safety
- **Version Parse Errors**: Clear error messages and build failures
- **Commit Failures**: Warnings instead of build failures

### **Skip CI Prevention**
- **Commit Messages**: Include `[skip ci]` to prevent recursive builds
- **Change Detection**: Only triggers on actual code/config changes

## 🎉 Benefits

### **For Developers**
✅ **No Manual Version Management** - Versions increment automatically
✅ **Change-Based Releases** - Only publishes when there are actual changes
✅ **Clear Version History** - Easy to track what changed in each version
✅ **Semantic Versioning** - Follows logical increment patterns

### **For CI/CD**
✅ **Reduced Build Load** - Skips unnecessary publishes
✅ **Conflict Prevention** - No more duplicate version errors
✅ **Reliable Automation** - Consistent versioning across environments
✅ **Audit Trail** - Git commits track all version changes

## 🔄 Workflow Example

### **Scenario**: Developer adds `GetRandomListedStock` tool

1. **Code Change**: New `.cs` file added to MCP Server
2. **GitHub Workflow**:
   - Detects code changes → increment type = `minor`
   - Current version: `1.0.0-beta` → New version: `1.0.0-beta.1`
   - Updates files and commits changes
   - Publishes to NuGet.org as `devopsabcs.AiStockTradeMcpServer`
3. **Azure DevOps Pipeline**:
   - Detects same changes → increment type = `minor`  
   - Auto-increments to `1.0.0-beta.2` (next available)
   - Publishes to private feed as `MngEnvMCAP675646.AiStockTradeApp.McpServer`

### **Result**: 
- ✅ Two separate packages with unique versions
- ✅ No conflicts or duplicate errors
- ✅ Automatic versioning based on actual changes
- ✅ Clear audit trail in git history

## 🚀 Future Enhancements

### **Potential Improvements**
- **Semantic Analysis**: Parse commit messages for breaking changes
- **API Change Detection**: Analyze code changes for API compatibility
- **Release Notes**: Auto-generate based on changes detected
- **Version Synchronization**: Option to sync versions across pipelines

---

## ✅ Status: **FULLY IMPLEMENTED** ✅

Both GitHub Actions and Azure DevOps pipelines now feature intelligent auto-versioning that eliminates manual version management while ensuring reliable, conflict-free dual publication.
