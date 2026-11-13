# 7 Days to Die Server Browser — v0.9.3

Author: ANAN (安安ANAN). Contact: QQ 383125283.

Source and review materials for the portable Windows server browser. All application C# source is included, including the single-file launcher. This archive does not contain a prebuilt executable, game assemblies, developer settings, credentials, favorites, logs, or screenshots.

## Build requirements

- Windows x64 with .NET Framework 4.8 runtime and the Framework64 C# compiler (`csc.exe`).
- PowerShell 5.1 or newer.
- A locally installed 7 Days to Die Windows client. This release was developed against V3.4.
- Steam signed in to an account owning the game is required for live searches; compilation does not require Steam to be running.
- No NuGet packages or downloads are performed by the build scripts.

## Build from source

Extract this archive, open PowerShell in its folder, and run:

```powershell
.\PrepareDependencies.ps1 -GamePath 'D:\Steam\steamapps\common\7 Days To Die'
.\BuildSingleFile.ps1
```

Replace the example path with your own game installation. If local execution policy blocks the scripts, a process-scoped invocation is sufficient; no permanent policy change is needed:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\PrepareDependencies.ps1 -GamePath 'D:\Steam\steamapps\common\7 Days To Die'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\BuildSingleFile.ps1
```

`PrepareDependencies.ps1` copies three DLLs from the installed game into this build folder. It does not modify the game. Expected SHA-256 values are recorded in `DEPENDENCY-HASHES.json`; different DLL versions produce a warning.

`Build.ps1` compiles the application. `BuildSingleFile.ps1` then embeds the core executable, dependency DLLs, icons and font resources into a portable launcher.

Output: `免安装单文件版/七日杀服务器浏览器.exe` (portable launcher). The root-level `七日杀服务器浏览器.exe` is the non-bundled core and requires the adjacent DLLs and Assets directory.

This review package omits the developer's `game-path.txt` from the packaging script. Runtime application source is unchanged. The application detects the installed game through Steam; an optional user-created `game-path.txt` can override detection. Build timestamps and ZIP metadata mean rebuilds are not expected to be byte-for-byte identical.

## Tests

```powershell
.\RunTests.ps1
```

Tests exercise endpoint validation, duplicate selection, search result parsing, sorting, region mapping, description formatting, symbol fonts, language and themes. Appearance tests briefly create their own test windows, close them, and save isolated test preferences under `开发验证/appearance-test-state`. They do not automate screen capture. Recorded live-result fixtures are not included; fixture-specific tests skip when those files are absent.

## Source map

- `Browser.cs`: WinForms UI, Steam queries, results, sorting, deduplication, connection requests and worker process management.
- `EosDirectory.cs`: EOS supplementary queries and Steam-ticket authentication.
- `ServerDetails.cs`: server metadata parsing and TCP metadata requests.
- `DetailsPanel.cs`, `DescriptionFormatting.cs`: server details, NGUI-style text formatting and readable colors.
- `GameFontFallback.cs`, `SystemGlyphCoverage.cs`: private font resources and installed-font fallback.
- `Appearance.cs`, `GameTheme.cs`, `SettingsPages.cs`, `StatisticsBar.cs`, `ServerBanner.cs`: appearance and UI components.
- `RuntimePaths.cs`: application-data paths.
- `SingleFileLauncher.cs`: embedded payload extraction and launch.

See `SECURITY-REVIEW.md` for runtime behavior and `THIRD-PARTY-NOTICES.md` for dependency/asset provenance. No open-source license is granted for ANAN's application code by this package; it is provided for inspection and local review/build. Third-party components retain their own terms.
