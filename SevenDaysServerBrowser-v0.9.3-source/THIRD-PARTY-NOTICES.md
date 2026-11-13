# Third-party components and assets

- `EOS.dll` and the native EOS SDK: Epic Online Services dependencies obtained from the user's installed game. Not included in this source archive. The application loads the native SDK from the user's game installation at runtime.
- `com.rlabrecque.steamworks.net.dll`: Steamworks.NET managed wrapper, copied from the installed game by the dependency preparation script. Not included here. Upstream project: https://github.com/rlabrecque/Steamworks.NET . Consult the upstream license for the specific version.
- `steam_api64.dll`: Valve Steamworks runtime, copied from the installed game by the preparation script. Not included here. Subject to Valve's applicable terms.
- Font subsets and mappings in `Assets/Fonts`: consult the included `OFL.txt` and `FONT-NOTICES.txt`. These resources are retained so the UI can be built as supplied.
- UI icon PNGs in `Assets`: game-derived interface icons used by the browser; third-party rights remain with their respective owners. `Browser.ico` is the project's current application icon. No broader redistribution rights are asserted by this archive.

This is an unofficial community tool. The game, Steamworks and EOS dependencies are not authored by ANAN. Their full source is not included, and the application's source availability does not change their licensing terms.
