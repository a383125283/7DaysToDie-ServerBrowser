$ErrorActionPreference = 'Stop'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ "/win32icon:$PSScriptRoot\Browser.ico" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "/reference:$PSScriptRoot\EOS.dll" "/reference:$PSScriptRoot\com.rlabrecque.steamworks.net.dll" "/out:$PSScriptRoot\七日杀服务器浏览器.exe" "$PSScriptRoot\Source\Browser.cs" "$PSScriptRoot\Source\ServerDetails.cs" "$PSScriptRoot\Source\DetailsPanel.cs" "$PSScriptRoot\Source\ServerBanner.cs" "$PSScriptRoot\Source\SettingsPages.cs" "$PSScriptRoot\Source\StatisticsBar.cs" "$PSScriptRoot\Source\GameTheme.cs" "$PSScriptRoot\Source\EosDirectory.cs" "$PSScriptRoot\Source\RuntimePaths.cs" "$PSScriptRoot\Source\Appearance.cs" "$PSScriptRoot\Source\DescriptionFormatting.cs" "$PSScriptRoot\Source\GameFontFallback.cs" "$PSScriptRoot\Source\SystemGlyphCoverage.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }








