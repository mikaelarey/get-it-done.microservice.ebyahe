param([string]$StorageLoginPath)

$ErrorActionPreference = 'Stop'
if (!$StorageLoginPath) {
  $StorageLoginPath = Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'get-it-done.storage/development/config/storage-login.json'
}
$settings = Get-Content -LiteralPath $StorageLoginPath -Raw | ConvertFrom-Json
$endpoint = [Uri]$settings.endpoint
if (!$endpoint.IsLoopback -or $endpoint.UserInfo -or $endpoint.Query -or $endpoint.Fragment) {
  throw 'This script only imports local Docker development storage settings.'
}
$credential = $settings.environments.dev
if (!$credential.bucket -or !$credential.accessKey -or !$credential.secretKey) { throw 'The DEV bucket credential is missing.' }
$secretDirectory = Join-Path $env:APPDATA 'Microsoft/UserSecrets/get-it-done-microservice-ebyahe'
$secretPath = Join-Path $secretDirectory 'secrets.json'
New-Item -ItemType Directory -Path $secretDirectory -Force | Out-Null
$secrets = if (Test-Path -LiteralPath $secretPath) { Get-Content -LiteralPath $secretPath -Raw | ConvertFrom-Json } else { [pscustomobject]@{} }
@{
  'Storage:Endpoint' = $settings.endpoint
  'Storage:Region' = $settings.region
  'Storage:Bucket' = $credential.bucket
  'Storage:AccessKey' = $credential.accessKey
  'Storage:SecretKey' = $credential.secretKey
}.GetEnumerator() | ForEach-Object { $secrets | Add-Member -NotePropertyName $_.Key -NotePropertyValue $_.Value -Force }
$secrets | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $secretPath -Encoding UTF8
Write-Output 'Configured Ebyahe with the existing Docker DEV bucket credentials. Secret values were not printed.'
$secrets = $null; $credential = $null; $settings = $null
