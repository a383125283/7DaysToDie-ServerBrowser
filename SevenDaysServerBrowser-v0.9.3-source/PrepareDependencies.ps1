param([Parameter(Mandatory=$true)][string]$GamePath)
$ErrorActionPreference = 'Stop'
$game = (Resolve-Path -LiteralPath $GamePath).Path
$expected = Get-Content -LiteralPath "$PSScriptRoot\DEPENDENCY-HASHES.json" -Raw | ConvertFrom-Json
$files = @{
    'EOS.dll' = '7DaysToDie_Data\Managed\EOS.dll'
    'com.rlabrecque.steamworks.net.dll' = '7DaysToDie_Data\Managed\com.rlabrecque.steamworks.net.dll'
    'steam_api64.dll' = '7DaysToDie_Data\Plugins\x86_64\steam_api64.dll'
}
foreach ($name in $files.Keys) {
    $source = Join-Path $game $files[$name]
    if (-not (Test-Path -LiteralPath $source)) { throw "Missing game dependency: $source" }
    $actual = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    if ($actual -ne $expected.$name) { Write-Warning "$name differs from the dependency used for v0.9.3; compatibility needs verification." }
    Copy-Item -LiteralPath $source -Destination (Join-Path $PSScriptRoot $name)
}
Write-Host 'Dependencies copied from your installed game. No game files were modified.'
