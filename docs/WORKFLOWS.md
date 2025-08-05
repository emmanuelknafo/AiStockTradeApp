# GitHub Actions Workflow Guide

This document explains when each workflow runs and what triggers them.

## Workflows Overview

### 1. CI/CD Pipeline (`ci-cd.yml`)

**Purpose**: Main build, test, and deployment pipeline

**Triggers**:

- ✅ **Runs on**: Code changes to application files
- ❌ **Skips on**: Documentation, README, and configuration-only changes

**Excluded Paths** (won't trigger):

- `**.md` - All Markdown files
- `**/README.md` - README files
- `docs/**` - Documentation folder
- `*.txt` - Text files
- `.gitignore` - Git ignore file
- `.vscode/**` - VS Code settings
- `.vs/**` - Visual Studio settings
- `AGENTS.md` - Agent documentation
- `LICENSE` - License file

**Manual Trigger**: Available with environment selection (dev/prod)

### 2. Documentation Updates (`docs.yml`)

**Purpose**: Validate documentation changes

**Triggers**:

- ✅ **Runs on**: Documentation file changes only

**Included Paths**:

- `**.md` - All Markdown files
- `**/README.md` - README files
- `docs/**` - Documentation folder
- `AGENTS.md` - Agent documentation

**Features**:

- Link validation in Markdown files
- README structure validation
- Documentation folder structure check

### 3. Configuration Validation (`config-validation.yml`)

**Purpose**: Validate configuration and infrastructure files

**Triggers**:

- ✅ **Runs on**: Configuration file changes

**Included Paths**:

- `.github/workflows/**` - Workflow files
- `**/appsettings*.json` - Application settings
- `**/*.config` - Configuration files
- `Dockerfile*` - Docker files
- `.dockerignore` - Docker ignore file
- `**/*.bicep` - Bicep templates
- `**/parameters*.json` - Parameter files

**Features**:

- JSON syntax validation
- Bicep template validation
- Dockerfile validation
- GitHub Actions workflow syntax validation

### 4. Infrastructure Deployment (`infrastructure.yml`)

**Purpose**: Deploy infrastructure independently

**Triggers**:

- 🔧 **Manual only**: Workflow dispatch with environment and location selection

**Features**:

- Infrastructure deployment
- Resource group creation
- Infrastructure destruction option

## Workflow Strategy

This setup optimizes CI/CD resource usage by:

1. **Separating Concerns**: Different workflows for different types of changes
2. **Resource Efficiency**: Main CI/CD only runs for actual code changes
3. **Fast Feedback**: Documentation and config validation runs quickly
4. **Flexibility**: Manual triggers for infrastructure operations

## Examples

| Change Type | Workflows That Run |
|-------------|-------------------|
| Code change in `Controllers/` | ✅ CI/CD Pipeline |
| Update `README.md` | ✅ Documentation Updates |
| Modify `appsettings.json` | ✅ Configuration Validation |
| Change `main.bicep` | ✅ Configuration Validation |
| Update both code and docs | ✅ CI/CD Pipeline + Documentation Updates |
| Manual infrastructure deploy | ✅ Infrastructure Deployment |

## Configuration Files

- **markdown-link-check-config.json**: Configuration for link checking in documentation
- Excludes localhost and local development URLs
- Includes retry logic and timeout settings
