[CmdletBinding()]
param([string]$AccountProject = '')
$ErrorActionPreference = 'Stop'
if (!$AccountProject) { $AccountProject = Join-Path $PSScriptRoot '../../get-it-done.microservice.account/Account.Application/Account.Application.csproj' }
$ebyaheProject = Join-Path $PSScriptRoot '../EByahe.Application/EByahe.Application.csproj'
$projects = @($ebyaheProject, $AccountProject)
$keys = @()
foreach ($project in $projects) {
  [xml]$xml = Get-Content -LiteralPath $project
  $id = @($xml.Project.PropertyGroup.UserSecretsId | Where-Object { $_ })[0]
  if (!$id) { throw 'Expected a UserSecretsId on each application project.' }
  $path = Join-Path $env:APPDATA "Microsoft/UserSecrets/$id/secrets.json"
  if (Test-Path -LiteralPath $path) {
    $secrets = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    $key = $secrets.'PartnerMembers:PasswordEncryptionKey'
    if ($key) { if ([Convert]::FromBase64String($key).Length -ne 32) { throw 'Invalid existing encryption key.' }; $keys += $key }
  }
}
if (@($keys | Select-Object -Unique).Count -gt 1) { throw 'The service encryption keys differ. Reconcile keys before processing requests.' }
if ($keys.Count) { $key = $keys[0] } else {
  $bytes = New-Object byte[] 32; $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
  try { $rng.GetBytes($bytes); $key = [Convert]::ToBase64String($bytes) } finally { $rng.Dispose(); [Array]::Clear($bytes, 0, $bytes.Length) }
}
foreach ($project in $projects) {
  & dotnet user-secrets set 'PartnerMembers:PasswordEncryptionKey' $key --project $project | Out-Null
  if ($LASTEXITCODE -ne 0) { throw 'Could not configure the service encryption key.' }
}
$key = $null; $keys = $null; $secrets = $null
Write-Output 'Matching partner member encryption keys configured in local user secrets. No secret was printed.'
