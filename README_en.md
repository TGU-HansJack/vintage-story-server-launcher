# VSSL · Vintage Story Server Launcher

VSSL is a desktop launcher for Vintage Story server players, hosts, and maintainers. It brings server download, profile management, config editing, save maintenance, mod maintenance, live console logs, and QQ bot linkage into one workspace so daily server operations can stay in a single flow.

## For Players

The common workflow is straightforward. You download a server build on the Download page, create a profile on the Profile page, set world parameters and rules on the Config page, then choose a save in Overview Console and start the server. After startup, you can continue to manage save files on the Saves page, mods on the Mods page, and message linkage on the Linkage page.

The Download page is only for server package download. The Profile page manages server instances. The Config page defines settings used when a new world is generated. The Overview Console page handles start, stop, command input, and real-time output. The Restrictions page captures client mod information only during player join handshake, then writes selected entries to `ModIdBlackList`.

If you use QQ bot linkage, it is best to finish OneBot connectivity first, then bind server and group. Message relay in both directions depends on that setup.

## Workspace Data

The default workspace path is `%LOCALAPPDATA%\VSSL\workspace`. Common paths are shown below.

| Path | Purpose |
| --- | --- |
| `profiles.json` | Stores the profile index |
| `packages` | Stores downloaded server archives |
| `servers\windows\<version>` | Stores extracted server binaries |
| `data\<profileId>` | Stores per-profile config, logs, and mods |
| `saves\<profileId>` | Stores `.vcdbs` save files for each profile |
| `robot\vs2qq-settings.json` | Stores bot linkage settings |
| `.runtime` | Stores runtime state and temporary capture data |

## For Developers

The solution uses .NET 10 and Avalonia 11. `VSSL.App` is the application entry and host composition layer. `VSSL.Ui` contains UI and interaction logic. `VSSL.Services` contains business logic. `VSSL.Domains` and `VSSL.Abstractions` define models and service contracts. `VSSL.Tests` contains automated tests.

Use the following commands for local development.

```bash
dotnet restore VSSL.sln
dotnet build VSSL.sln -c Debug
dotnet run --project VSSL.App/VSSL.App.csproj
dotnet test VSSL.Tests/VSSL.Tests.csproj -c Debug
```

Use the following commands for release output.

```bash
dotnet publish VSSL.App/VSSL.App.csproj -c Release -r win-x64 --self-contained true -p:Version=0.0.0-local -o artifacts/publish/win-x64
dotnet publish VSSL.App/VSSL.App.csproj -c Release -r win-x64 --self-contained true -p:Version=0.0.0-local -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/publish/portable
```

When adding a new page, update `VSSL.Domains/Enums/ViewName.cs`, `VSSL.Ui/Services/Ui/DefaultNavigationService.cs`, `VSSL.Services/Configs/menus.json`, and the matching ViewModel and View implementation.  
When adding a new service, add the `*Service` implementation in `VSSL.Services` and keep the existing dependency injection convention.  
When updating localization text, edit `VSSL.Ui/Assets/I18n/Resources*.resx`.  

## Bot Commands

| Command | Description |
| --- | --- |
| `/help` | Command help |
| `/bindqq <player_name>` | Bind QQ to a player name |
| `/unbindqq` | Unbind current QQ |
| `/mybind` | Show current QQ binding |
| `/bindserver <host_or_ip_port_or_domain> <token> <qq_group_id>` | Bind remote server |
| `/unbindserver <host_or_ip_port_or_domain> <qq_group_id>` | Unbind remote server |
| `/listserver` | Show remote server bindings |
| `/server status [n]` | Query recent status |
| `/bindlogserver <server_id> <log_path>` | Bind local log server |
| `/unbindlogserver <server_id>` | Unbind local log server |
| `/listlogserver` | Show local log server bindings |
| `/bindlogregex <server_id> <regex>` | Set log matching regex |

## License

This project is licensed under GPLv3 (GNU General Public License v3.0). See [LICENSE](LICENSE).

## Links

Repository: <https://github.com/TGU-HansJack/vintage-story-server-launcher>  
Issue Tracker: <https://github.com/TGU-HansJack/vintage-story-server-launcher/issues>  
Chinese Community: <https://vintagestory.top/>
