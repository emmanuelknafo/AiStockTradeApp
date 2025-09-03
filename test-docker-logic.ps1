# Test script to verify Docker command logic
param(
    [switch]$RemoveVolumes
)

Write-Host "Testing Docker command generation..."
Write-Host "RemoveVolumes parameter: $RemoveVolumes"

if ($RemoveVolumes) {
    Write-Host "Command would be: docker compose down --rmi all --volumes --remove-orphans" -ForegroundColor Yellow
} else {
    Write-Host "Command would be: docker compose down --rmi all --remove-orphans" -ForegroundColor Green
}
