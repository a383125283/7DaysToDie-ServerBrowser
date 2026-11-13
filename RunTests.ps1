$ErrorActionPreference = 'Stop'
$compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$sourceFiles = Get-ChildItem -LiteralPath "$PSScriptRoot\Source" -Filter '*.cs' | Where-Object { $_.Name -ne 'SingleFileLauncher.cs' } | ForEach-Object { $_.FullName }
Push-Location $PSScriptRoot
try {
    foreach ($test in @('CoreTests', 'AppearanceTests')) {
        & $compiler /nologo /target:exe /platform:x64 /optimize+ "/main:$test" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "/reference:$PSScriptRoot\EOS.dll" "/reference:$PSScriptRoot\com.rlabrecque.steamworks.net.dll" "/out:$PSScriptRoot\$test.exe" $sourceFiles
        if ($LASTEXITCODE -ne 0) { throw "$test compilation failed" }
        & (Join-Path $PSScriptRoot "$test.exe")
        if ($LASTEXITCODE -ne 0) { throw "$test failed" }
    }
} finally { Pop-Location }
