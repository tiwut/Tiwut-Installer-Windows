# Tiwut Launcher Installer

A lightweight, zero-dependency, modern Windows setup wizard and uninstaller in a single executable (`TiwutInstaller.exe`) designed exclusively for distributing and managing Tiwut Launcher applications.

Built natively in C# targeting **.NET Framework 4.8** (pre-installed on Windows 10 & 11) and styled with custom GDI+ owner-drawn controls, the entire compiled binary is only **59 KB** in size.

---

## Features

- **OS Theme Syncing**: Automatically reads the Windows Personalize Registry keys to match the user's active theme:
  - *Dark Mode*: Sleek obsidian card panels withIndigo/Violet accents and rounded hover states.
  - *Light Mode*: Minimal white panels, grey separators, and dark typography.
- **Strict Remote Config Loading**: Loads installation settings on-the-fly exclusively from a single GitHub raw URL:
  `https://raw.githubusercontent.com/tiwut/Tiwut-Launcher-Windows/refs/heads/main/tiwut-installer-config.json`
- **Modern Connection Fail-safes**: Automatically initiates modern TLS 1.2, 1.1, and 1.0 protocols to resolve standard `.NET Framework` SSL/TLS secure channel errors when contacting GitHub.
- **Fail-safe Network Downloader**: Includes connection retry logic (3 attempts), file size checks, and ZIP magic bytes signature verification (`PK` header check) to prevent corrupted packages or server error pages from executing.
- **Active Process Closure**: Checks for and gracefully terminates any running instances of the target application executable before extraction to prevent file lock errors.
- **Vulnerability Defense**: Contains Zip Slip detection and prevention to safeguard against path traversal extraction exploits.
- **Shortcuts & Registry Integration**: Creates Desktop, Start Menu, and Taskbar (Quick Launch best-effort) shortcuts using native COM reflection. Registers the application under Windows **Add/Remove Programs** (Registry uninstall key) with estimated size calculations.
- **Dual-Mode Executable & Self-Destruct Uninstaller**: Copies itself into the installation folder as `Uninstall.exe`. When launched with `/uninstall` (e.g. from Control Panel), it purges all shortcuts, registry keys, and directories, spawning a detached hidden CMD command to delete the uninstaller executable itself on exit.

---

## Configuration Schema

The installer strictly downloads its configuration from the remote URL. Here is a sample format (as found in `sample-config.json`):

```json
{
  "appName": "Tiwut Launcher",
  "zipUrl": "https://github.com/tiwut/Tiwut-Launcher-Windows/archive/refs/heads/main.zip",
  "licenseUrl": "https://raw.githubusercontent.com/tiwut/Tiwut-Launcher-Windows/main/LICENSE",
  "installDir": "%LocalAppData%\\TiwutLauncher",
  "exeName": "dist\\TiwutLauncher.exe",
  "requireAdmin": false,
  "requireRestart": false,
  "iconUrl": "https://raw.githubusercontent.com/tiwut/Tiwut-Launcher-Windows/main/favicon.ico",
  "shortcuts": {
    "desktop": true,
    "startMenu": true,
    "taskbar": true
  }
}
```

### Parameter Reference:
- `appName` (String, required): Name of the software.
- `zipUrl` (String, required): Download URL of the ZIP archive containing application files.
- `licenseUrl` (String, optional): URL to download the license text. If provided, the installer forces a license agreement page.
- `installDir` (String, required): Path for extraction (supports environment variables like `%LocalAppData%`, `%AppData%`, `%ProgramFiles%`).
- `exeName` (String, required): Location of the main executable file relative to the extraction root.
- `requireAdmin` (Boolean): If `true`, requests Windows UAC prompt.
- `requireRestart` (Boolean): If `true`, prompts for a system restart on completion.
- `iconUrl` (String, optional): Download URL for a custom `.ico` file to use on shortcuts.
- `shortcuts` (Object): Flags (`desktop`, `startMenu`, `taskbar`) toggling default shortcut options.

---

## Building and Compiling

### Prerequisites
- Windows 10 or 11 OS.
- PowerShell 5.0+ (to execute the script).
- Pre-installed `.NET Framework` references (automatic).

### Compile Command
Open a PowerShell terminal in the repository folder and run the compilation script:

```powershell
Set-ExecutionPolicy Bypass -Scope Process
.\compile.ps1
```

The script will automatically:
1. Compile the program icon generator (`IconGenerator.cs`).
2. Run it to draw and save the Vista-compatible `app.ico` file.
3. Compile the main installer (`TiwutInstaller.cs`) incorporating the generated icon.
4. Clean up any temporary executable resources.

The output will be saved as **`TiwutInstaller.exe`** (approx. **59 KB**).

---

## License

This project is licensed under the MIT License.
