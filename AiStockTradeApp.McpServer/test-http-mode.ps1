# PowerShell script to test the MCP server in HTTP mode
# This script demonstrates how to use the MCP server with HTTP transport

Write-Host "Testing AI Stock Trade MCP Server - HTTP Transport Mode" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green

# Check if the API server is running first
Write-Host "`nChecking if API server is running at https://app-aistock-dev-002.azurewebsites.net..." -ForegroundColor Yellow
try {
    $apiResponse = Invoke-RestMethod -Uri "https://app-aistock-dev-002.azurewebsites.net/api/stocks/quote?symbol=AAPL" -Method Get -TimeoutSec 5
    Write-Host "✓ API server is running and responsive" -ForegroundColor Green
} catch {
    Write-Host "⚠️  API server might not be running. The MCP server will use http://localhost:5000 as configured." -ForegroundColor Yellow
    Write-Host "   Make sure your stock trading API is running before testing MCP tools." -ForegroundColor Yellow
}

Write-Host "`nStarting MCP Server in HTTP mode..." -ForegroundColor Yellow
Write-Host "Using command: dotnet run -- --http" -ForegroundColor Cyan

# Start the MCP server in HTTP mode in the background
$job = Start-Job -ScriptBlock {
    Set-Location $args[0]
    dotnet run -- --http
} -ArgumentList (Get-Location)

# Wait a bit for the server to start
Start-Sleep -Seconds 3

Write-Host "`nMCP Server should now be running in HTTP mode." -ForegroundColor Green
Write-Host "You can test it with the following curl commands:" -ForegroundColor Yellow

Write-Host "`n1. List available tools:" -ForegroundColor Cyan
$listToolsCommand = 'curl -X POST http://localhost:5000/mcp -H "Accept: application/json, text/event-stream" -H "Content-Type: application/json" -d "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}"'
Write-Host $listToolsCommand -ForegroundColor White

Write-Host "`n2. Get a stock quote for AAPL:" -ForegroundColor Cyan
$stockQuoteCommand = 'curl -X POST http://localhost:5000/mcp -H "Accept: application/json, text/event-stream" -H "Content-Type: application/json" -d "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"GetStockQuote\",\"arguments\":{\"symbol\":\"AAPL\"}}}"'
Write-Host $stockQuoteCommand -ForegroundColor White

Write-Host "`n3. Get top 10 listed stocks:" -ForegroundColor Cyan
$listedStocksCommand = 'curl -X POST http://localhost:5000/mcp -H "Accept: application/json, text/event-stream" -H "Content-Type: application/json" -d "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\"GetListedStocks\",\"arguments\":{\"count\":10}}}"'
Write-Host $listedStocksCommand -ForegroundColor White

Write-Host "`n4. Generate a random number:" -ForegroundColor Cyan
$randomNumberCommand = 'curl -X POST http://localhost:5000/mcp -H "Accept: application/json, text/event-stream" -H "Content-Type: application/json" -d "{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"tools/call\",\"params\":{\"name\":\"GetRandomNumber\",\"arguments\":{\"min\":1,\"max\":100}}}"'
Write-Host $randomNumberCommand -ForegroundColor White

Write-Host "`n" -ForegroundColor Yellow
Write-Host "Press any key to stop the MCP server and exit..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

# Stop the background job
Stop-Job $job
Remove-Job $job

Write-Host "`nMCP Server stopped." -ForegroundColor Green
Write-Host "For STDIO mode, simply run: dotnet run" -ForegroundColor Cyan
Write-Host "For HTTP mode, run: dotnet run -- --http" -ForegroundColor Cyan
