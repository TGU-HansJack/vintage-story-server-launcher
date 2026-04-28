# VSSL · Vintage Story Server Launcher

VSSL is a desktop launcher and operations tool for Vintage Story dedicated servers. Its goal is to unify package download, instance creation, configuration, console control, save management, mod management, map preview, and QQ-bot integration in one UI.

## Project Status

This project is under active development. The UI and features are still evolving. Most core pages are available, while a few pages are currently placeholders.

## UI Preview

| Preview Type | Image |
| --- | --- |
| Animated Demo | ![VSSL Demo](Screenshots/VSSL.gif) |
| Dark Theme | ![Dark Theme](Screenshots/Dark.png) |
| Light Theme | ![Light Theme](Screenshots/Light.png) |

## For Users

The application uses a three-zone layout. The top area contains window controls and project links, the left side is the primary navigation, and the center area contains submenus and business pages. On first launch, an onboarding dialog asks for default theme and language. Current locales are Simplified Chinese and English.

| Module | User Value | Current Behavior |
| --- | --- | --- |
| Dashboard | Unified status view for server and robot runtime. | Shows runtime state, memory usage, uptime, online player count, and recent trend charts. |
| Workspace Console | Fast server startup and daily operations. | Supports quick create-and-start, start/stop, command input, log tailing, and log export. |
| Map Preview | Inspect terrain and coordinates without launching the game. | Reads `.vcdbs` directly, renders color and grayscale maps, supports zoom, drag, and coordinate hover. |
| Instance Download | Fetch server packages. | Loads official entries from `stable-unstable.json`, filters Windows server builds, and downloads to local workspace. |
| Instance Create | Manage profile lifecycle. | Creates profiles by installed version, manages profile list, and supports bulk delete. |
| Config | Visual configuration for `serverconfig.json`. | Edits server basics, world parameters, world rules, and supports advanced JSON editing. |
| Save | Manage `.vcdbs` save files. | Creates saves, switches active save, deletes saves, and writes changes back to profile config. |
| Mod | Manage the Mods directory. | Imports zip mods, parses `modinfo.json`, toggles enable state, and reports missing dependencies. |
| Robot Config | Configure VS2QQ integration. | Configures OneBot WebSocket, token, polling, encoding, super users, and database path. |
| Robot Console | Operate VS2QQ runtime. | Starts and stops robot service, refreshes and clears logs, and shows runtime connection state. |
| About | Version and project links. | Displays current version, checks GitHub Releases updates, and opens repository or community links. |
| Feedback | Issue reporting entry. | One-click jump to the issue tracker page. |

| Typical Workflow | Description |
| --- | --- |
| Download Package | Download `vs_server_win-x64_*.zip` from the Instance Download page. |
| Create Profile | Select a version and create a profile in Instance Create. |
| Check Config | Verify port, world rules, and save path in Config. |
| Start Server | Start the server and monitor output in Workspace Console. |
| Maintain Assets | Maintain saves and mods in the Save and Mod pages. |
| Bridge to QQ | Enable VS2QQ in Robot Config and Robot Console. |
| Inspect Map | Load and inspect map previews and coordinates in Map Preview. |

## Data Layout

The default workspace root is `%LOCALAPPDATA%\VSSL\workspace`.

| Path | Purpose |
| --- | --- |
| `launcher-preferences.json` | Launcher preferences including onboarding state, theme, and language. |
| `profiles.json` | Profile index storing metadata for all instances. |
| `packages` | Downloaded server package files. |
| `servers\windows\<version>` | Extracted server runtime directory containing `VintagestoryServer.exe`. |
| `data\<profileId>` | Profile data directory containing `serverconfig.json`, `Logs`, and `Mods`. |
| `saves\<profileId>` | Profile save directory containing `.vcdbs` files. |
| `robot\vs2qq-settings.json` | Local VS2QQ settings file. |
| `exports` | Export folder for console log files. |
| `.tmp` | Temporary directory for install and intermediate operations. |

## For Developers

The project targets .NET 10 and Avalonia 11, with a layered design of bootstrap (`VSSL.App`), UI (`VSSL.Ui`), services (`VSSL.Services`), and domain models (`VSSL.Domains`), all wired through dependency injection.

| Project | Responsibility |
| --- | --- |
| `VSSL.App` | Entry point, host composition, configuration loading, logging bootstrap, onboarding trigger. |
| `VSSL.Ui` | Avalonia views, view models, navigation, theming, and localization services. |
| `VSSL.Services` | Core services for download, profiles, config, saves, mods, server process, map preview, robot, and update checks. |
| `VSSL.Domains` | DTOs, config models, runtime state models, world rules, and menu models. |
| `VSSL.Abstractions` | Interfaces for business and UI services. |
| `VSSL.Common` | Shared constants and helper utilities. |
| `VSSL.Tests` | xUnit test project. |

### Local Development

```bash
dotnet restore VSSL.sln
dotnet build VSSL.sln -c Debug
dotnet run --project VSSL.App/VSSL.App.csproj
dotnet test VSSL.Tests/VSSL.Tests.csproj -c Debug
```

### Publish and Packaging

```bash
dotnet publish VSSL.App/VSSL.App.csproj -c Release -r win-x64 --self-contained true -p:Version=0.0.0-local -o artifacts/publish/win-x64
dotnet publish VSSL.App/VSSL.App.csproj -c Release -r win-x64 --self-contained true -p:Version=0.0.0-local -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/publish/portable
```

Windows installer packaging is defined in `.github/workflows/windows-packages.yml`, with Inno Setup script at `installer/VSSL.iss`.

### Extension Points

| Extension Scenario | Entry Point |
| --- | --- |
| Add a Page | Update `VSSL.Domains/Enums/ViewName.cs`, `VSSL.Ui/Services/Ui/DefaultNavigationService.cs`, `VSSL.Services/Configs/menus.json`, and related view or viewmodel files. |
| Add a Service | Add a `*Service` class and interface in `VSSL.Services`. DI auto-registration follows naming convention. |
| Adjust Menu | Edit `VSSL.Services/Configs/menus.json`. |
| Localization | Edit `VSSL.Ui/Assets/I18n/Resources*.resx`. |
| Theme Behavior | Adjust `VSSL.Ui/Services/Ui/ThemeService.cs` and theme resource files. |

### Robot Commands

Built-in VS2QQ currently supports `/help`, `/bindqq`, `/unbindqq`, `/mybind`, `/bindserver`, `/unbindserver`, `/listserver`, and `/bindserverregex`.

## Platform Notes and Limitations

| Topic | Notes |
| --- | --- |
| Download Source | Current download flow focuses on Windows server builds named `vs_server_win-x64_*.zip`. |
| Instance Manage Page | `Instance / Manage` is currently a placeholder page. |
| Workspace Dependency | The default flow depends on workspace structure. Refresh pages after manual file moves. |

## License

This project is licensed under GPLv3 (GNU General Public License v3.0). See [LICENSE](LICENSE) for details.

## Links

Repository: <https://github.com/TGU-HansJack/vintage-story-server-launcher>  
Issues: <https://github.com/TGU-HansJack/vintage-story-server-launcher/issues>  
Community: <https://vintagestory.top/>
