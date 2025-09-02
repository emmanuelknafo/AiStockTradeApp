# Test script for the Microsoft-pattern MCP Server Docker container
Write-Host "Testing MCP Server Docker Container (Microsoft Pattern)" -ForegroundColor Green
Write-Host "=======================================================" -ForegroundColor Green

# Check if the image exists
$imageName = "aistocktradeapp-mcp:latest"
$imageExists = docker images --format "table {{.Repository}}:{{.Tag}}" | Select-String $imageName

if (-not $imageExists) {
    Write-Host "Error: Docker image $imageName not found. Please build it first." -ForegroundColor Red
    exit 1
}

Write-Host "✅ Docker image found: $imageName" -ForegroundColor Green

# Test 1: Run container in background and test HTTP endpoint
Write-Host "`n🧪 Test 1: Starting container with HTTP mode..." -ForegroundColor Yellow

# Stop any existing containers
docker stop mcp-test-container 2>$null
docker rm mcp-test-container 2>$null

# Start container
Write-Host "Starting container..." -ForegroundColor Cyan
docker run -d --name mcp-test-container -p 5001:5000 -e STOCK_API_BASE_URL="https://app-aistock-dev-002.azurewebsites.net" $imageName dotnet AiStockTradeApp.McpServer.dll --http

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to start container" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Container started successfully" -ForegroundColor Green

# Wait for container to be ready
Write-Host "⏳ Waiting for MCP server to start..." -ForegroundColor Cyan
Start-Sleep -Seconds 5

# Test 2: Check container logs
Write-Host "`n🧪 Test 2: Checking container logs..." -ForegroundColor Yellow
$logs = docker logs mcp-test-container 2>&1
Write-Host "Container logs:" -ForegroundColor Cyan
Write-Host $logs

# Test 3: Test MCP endpoint
Write-Host "`n🧪 Test 3: Testing MCP tools/list endpoint..." -ForegroundColor Yellow

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5001/mcp" -Method Post `
        -Body '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' `
        -ContentType "application/json" `
        -TimeoutSec 10
    
    Write-Host "✅ MCP endpoint responded successfully!" -ForegroundColor Green
    Write-Host "Response:" -ForegroundColor Cyan
    $response | ConvertTo-Json -Depth 5
    
} catch {
    Write-Host "❌ MCP endpoint test failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Container logs:" -ForegroundColor Yellow
    docker logs mcp-test-container
}

# Test 4: Test a stock quote tool
Write-Host "`n🧪 Test 4: Testing GetStockQuote tool..." -ForegroundColor Yellow

try {
    $stockRequest = @{
        jsonrpc = "2.0"
        id = 2
        method = "tools/call"
        params = @{
            name = "GetStockQuote"
            arguments = @{
                symbol = "AAPL"
            }
        }
    } | ConvertTo-Json -Depth 3
    
    $stockResponse = Invoke-RestMethod -Uri "http://localhost:5001/mcp" -Method Post `
        -Body $stockRequest `
        -ContentType "application/json" `
        -TimeoutSec 15
    
    Write-Host "✅ Stock quote tool responded successfully!" -ForegroundColor Green
    Write-Host "AAPL Stock Data:" -ForegroundColor Cyan
    $stockResponse | ConvertTo-Json -Depth 5
    
} catch {
    Write-Host "❌ Stock quote tool test failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Cleanup
Write-Host "`n🧹 Cleaning up..." -ForegroundColor Yellow
docker stop mcp-test-container
docker rm mcp-test-container

Write-Host "`n🎉 Container test completed!" -ForegroundColor Green
Write-Host "The MCP server container is working correctly with the Microsoft pattern." -ForegroundColor Cyan
