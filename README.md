# VSSL · Vintage Story Server Launcher

VSSL 是面向《Vintage Story》服务器玩家和服主的桌面启动器。它把版本下载、档案管理、配置编辑、存档维护、模组维护、控制台日志和 QQ 机器人联结放到同一个工作区中，目的是把开服与运维流程从多窗口切换变成单界面闭环。

## 面向玩家

VSSL 的日常使用路径很直接。你先在实例下载页面获取服务端版本，再在档案页面创建对应档案，然后在配置页面设置世界参数和规则。准备完成后，去总览控制台选择存档并启动服务器。服务器开始运行后，你可以继续在存档页面管理存档文件，在模组页面管理模组包，在联结页面配置机器人互通。

下载页面只负责下载服务端文件。档案页面负责组织服务器实例。配置页面负责定义新世界生成时使用的参数。总览控制台负责启动、停止、发命令和查看实时输出。限制页面负责在玩家进服握手阶段采集客户端模组信息，并将选中模组写入 `ModIdBlackList`。

如果你启用了 QQ 机器人联结，推荐先完成 OneBot 连接，再绑定服务器和群。服务器到群、群到服务器的消息桥接都依赖这一步。

## 数据目录

默认工作区位于 `%LOCALAPPDATA%\VSSL\workspace`。常见目录与文件如下。

| 路径 | 用途 |
| --- | --- |
| `profiles.json` | 保存档案索引 |
| `packages` | 保存下载后的服务端压缩包 |
| `servers\windows\<version>` | 保存解压后的服务端程序 |
| `data\<profileId>` | 保存档案配置、日志和模组目录 |
| `saves\<profileId>` | 保存档案关联的 `.vcdbs` 存档 |
| `robot\vs2qq-settings.json` | 保存机器人配置 |
| `.runtime` | 保存运行时状态和临时采集数据 |

## 面向开发者

项目采用 .NET 10 与 Avalonia 11。`VSSL.App` 负责启动与宿主装配，`VSSL.Ui` 负责界面与交互，`VSSL.Services` 负责业务实现，`VSSL.Domains` 与 `VSSL.Abstractions` 负责模型和接口边界，`VSSL.Tests` 提供测试。

本地开发可以直接使用下面的命令。

```bash
dotnet restore VSSL.sln
dotnet build VSSL.sln -c Debug
dotnet run --project VSSL.App/VSSL.App.csproj
dotnet test VSSL.Tests/VSSL.Tests.csproj -c Debug
```

发布与打包可使用下面的命令。

```bash
dotnet publish VSSL.App/VSSL.App.csproj -c Release -r win-x64 --self-contained true -p:Version=0.0.0-local -o artifacts/publish/win-x64
dotnet publish VSSL.App/VSSL.App.csproj -c Release -r win-x64 --self-contained true -p:Version=0.0.0-local -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/publish/portable
```

如果要新增页面，请同步更新 `VSSL.Domains/Enums/ViewName.cs`、`VSSL.Ui/Services/Ui/DefaultNavigationService.cs`、`VSSL.Services/Configs/menus.json` 和对应的 ViewModel 与 View。  
如果要新增服务，请在 `VSSL.Services` 中添加 `*Service` 类与接口定义，并保持现有依赖注入约定。  
如果要更新文案，请修改 `VSSL.Ui/Assets/I18n/Resources*.resx`。  

## 机器人命令

| 命令 | 说明 |
| --- | --- |
| `/help` | 命令帮助 |
| `/bindqq <player_name>` | 绑定 QQ 到玩家名 |
| `/unbindqq` | 解绑当前 QQ |
| `/mybind` | 查看当前 QQ 绑定 |
| `/bindserver <host_or_ip_port_or_domain> <token> <qq_group_id>` | 绑定远程服务器 |
| `/unbindserver <host_or_ip_port_or_domain> <qq_group_id>` | 解绑远程服务器 |
| `/listserver` | 查看远程服务器绑定 |
| `/server status [n]` | 查询最近状态 |
| `/bindlogserver <server_id> <log_path>` | 绑定本机日志服务器 |
| `/unbindlogserver <server_id>` | 解绑本机日志服务器 |
| `/listlogserver` | 查看本机日志服务器绑定 |
| `/bindlogregex <server_id> <regex>` | 设置日志匹配正则 |

## 许可证

本项目采用 GPLv3（GNU General Public License v3.0）许可证，详见 [LICENSE](LICENSE)。

## 链接

项目仓库地址为 <https://github.com/TGU-HansJack/vintage-story-server-launcher>。  
问题反馈地址为 <https://github.com/TGU-HansJack/vintage-story-server-launcher/issues>。  
中文社区地址为 <https://vintagestory.top/>。
