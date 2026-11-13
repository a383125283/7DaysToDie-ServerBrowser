# Runtime behavior for manual review

## Portable launcher

`SingleFileLauncher.cs` embeds a ZIP resource named `BrowserPayload.zip`. It hashes the payload and extracts it to `%LOCALAPPDATA%/Anan/SevenDaysServerBrowser/Runtime/<SHA256>/`. It verifies existing extracted files by hash before reusing them, then starts `BrowserCore.exe` and waits for it to exit. Extraction checks that paths remain inside the runtime folder.

This extraction/child-process pattern is intentional packaging behavior; it is not an installation service. The archive does not contain browser profiles, login cookies or account passwords. User preferences, favorites and optional game-path settings are stored in the application-data directory. No startup registration or scheduled task is created by the launcher.

## Search and connection

The application uses Steamworks and Epic Online Services APIs for server directory queries. It sends TCP metadata requests to listed game server endpoints. Steam may show the game as running while a query session is active. Additional instances of the application core are used for isolated query work.

For EOS, `EosDirectory.Begin` loads the native EOS SDK and reads the EOS client configuration from the user's installed game assemblies at runtime. A Steam encrypted application ticket is passed to the EOS Connect login API. Tickets and client configuration are not bundled as literal secrets or written to log files by this code. These operations should be reviewed explicitly; this is not a claim of endorsement by the game or platform providers.

Connections are requested using Steam URI handlers. The program does not patch game assemblies, inject into the game process, or bypass EAC. Actual server admission remains controlled by the game/server.

## Scope of build validation

Compilation and automated tests cannot establish that software is malware-free or that it meets every hosting policy. Nexus Mods staff should review the complete source and bundled resources and perform their own scanning. Automated quarantine must be resolved through the site's normal manual-review process.
