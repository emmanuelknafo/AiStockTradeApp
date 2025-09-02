$hdrs = @{ 'Accept' = 'application/json, text/event-stream' }
$body = '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
try {
  $wr = Invoke-WebRequest -Uri 'http://localhost:5000/mcp' -Method Post -ContentType 'application/json' -Body $body -Headers $hdrs -TimeoutSec 10 -ErrorAction Stop
  $c = $wr.Content
  if ($c -match '(?s)data:\s*(\{.*\})') { $json = $matches[1] } else { $json = $c }
  $obj = ConvertFrom-Json $json
  $obj | ConvertTo-Json -Depth 5
} catch {
  Write-Error $_.Exception.Message
  exit 2
}
