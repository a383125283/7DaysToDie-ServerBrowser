$ErrorActionPreference = 'Stop'
& "$PSScriptRoot\Build.ps1"
Add-Type -AssemblyName System.IO.Compression
$output = Join-Path $PSScriptRoot '免安装单文件版'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$payload = Join-Path $env:TEMP ('AnanBrowserPayload-' + [Guid]::NewGuid().ToString('N') + '.zip')
$stream = [IO.File]::Create($payload)
try {
    $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        $files = @{
            'BrowserCore.exe' = "$PSScriptRoot\七日杀服务器浏览器.exe"
            'com.rlabrecque.steamworks.net.dll' = "$PSScriptRoot\com.rlabrecque.steamworks.net.dll"
            'EOS.dll' = "$PSScriptRoot\EOS.dll"
            'steam_api64.dll' = "$PSScriptRoot\steam_api64.dll"
            'steam_appid.txt' = "$PSScriptRoot\steam_appid.txt"
        }
        foreach ($file in Get-ChildItem -LiteralPath "$PSScriptRoot\Assets" -File -Filter '*.png') { $files['Assets/' + $file.Name] = $file.FullName }
        foreach ($font in Get-ChildItem -LiteralPath "$PSScriptRoot\Assets\Fonts" -File) { $files['Assets/Fonts/' + $font.Name] = $font.FullName }
        foreach ($name in ($files.Keys | Sort-Object)) {
            $entry = $zip.CreateEntry($name, [IO.Compression.CompressionLevel]::Optimal)
            $input = [IO.File]::OpenRead($files[$name]); $entryStream = $entry.Open()
            try { $input.CopyTo($entryStream) } finally { $entryStream.Dispose(); $input.Dispose() }
        }
    } finally { $zip.Dispose() }
} finally { $stream.Dispose() }
try {
    & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ /main:SingleFileLauncher /reference:System.Windows.Forms.dll /reference:System.Core.dll /reference:System.IO.Compression.dll "/win32icon:$PSScriptRoot\Browser.ico" "/resource:$payload,BrowserPayload.zip" "/out:$output\七日杀服务器浏览器.exe" "$PSScriptRoot\Source\SingleFileLauncher.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Single-file build failed' }
} finally { Remove-Item -LiteralPath $payload }
Get-Item -LiteralPath "$output\七日杀服务器浏览器.exe" | Select-Object FullName,Length
