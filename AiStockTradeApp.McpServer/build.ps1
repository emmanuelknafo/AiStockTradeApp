# Build and Package Script for AI Stock Trade MCP Server

param(
    [string]$Configuration = "Release",
    [string]$OutputPath = "./bin/packages",
    [switch]$BuildDocker,
    [switch]$PublishToNuGet,
    [string]$NuGetApiKey = $env:NUGET_API_KEY
)

Write-Host "🚀 Building AI Stock Trade MCP Server" -ForegroundColor Green
Write-Host "Configuration: $Configuration"

try {
    # Clean previous builds
    Write-Host "🧹 Cleaning previous builds..." -ForegroundColor Yellow
    dotnet clean -c $Configuration

    # Restore dependencies
    Write-Host "📦 Restoring dependencies..." -ForegroundColor Yellow
    dotnet restore

    # Build the project
    Write-Host "🔨 Building project..." -ForegroundColor Yellow
    dotnet build -c $Configuration --no-restore

    # Run tests if available
    Write-Host "🧪 Running tests..." -ForegroundColor Yellow
    if (Test-Path "*.Tests.*") {
        dotnet test -c $Configuration --no-build
    } else {
        Write-Host "No tests found, skipping..." -ForegroundColor Gray
    }

    # Create package
    Write-Host "📦 Creating NuGet package..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null
    dotnet pack -c $Configuration --no-build -o $OutputPath

    $packageFile = Get-ChildItem "$OutputPath/*.nupkg" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

    if ($packageFile) {
        Write-Host "✅ Package created: $($packageFile.Name)" -ForegroundColor Green
        Write-Host "📍 Location: $($packageFile.FullName)" -ForegroundColor Cyan
    }

    # Build Docker image if requested
    if ($BuildDocker) {
        Write-Host "🐳 Building Docker image..." -ForegroundColor Yellow
        $imageName = "aistocktrade-mcp-server"
        $imageTag = "latest"
        
        docker build -t "${imageName}:${imageTag}" .
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Docker image built: ${imageName}:${imageTag}" -ForegroundColor Green
        } else {
            throw "Docker build failed"
        }
    }

    # Publish to NuGet if requested
    if ($PublishToNuGet) {
        if (-not $NuGetApiKey) {
            throw "NuGet API key is required for publishing. Set NUGET_API_KEY environment variable or use -NuGetApiKey parameter."
        }

        Write-Host "🚀 Publishing to NuGet.org..." -ForegroundColor Yellow
        dotnet nuget push "$($packageFile.FullName)" --api-key $NuGetApiKey --source https://api.nuget.org/v3/index.json

        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Successfully published to NuGet.org!" -ForegroundColor Green
        } else {
            throw "NuGet publish failed"
        }
    }

    Write-Host ""
    Write-Host "🎉 Build completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "📋 Next steps:" -ForegroundColor Yellow
    Write-Host "1. Test the MCP server locally with: dotnet run" -ForegroundColor White
    Write-Host "2. Configure Claude Desktop using the examples in claude-config-example.md" -ForegroundColor White
    if ($BuildDocker) {
        Write-Host "3. Deploy to Azure using: ./deploy-azure.ps1" -ForegroundColor White
    }
    if ($PublishToNuGet) {
        Write-Host "3. The package is now available on NuGet.org" -ForegroundColor White
    }
}
catch {
    Write-Error "❌ Build failed: $($_.Exception.Message)"
    exit 1
}
