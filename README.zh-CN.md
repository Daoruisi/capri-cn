# Capri 汉化版（Windows 桌面宠物）

> 非官方汉化分支：跟随上游 [lachydotmcg/capri-desktop](https://github.com/lachydotmcg/capri-desktop) **0.8.5.1**。
> 界面中文化，并额外加了立绘换装、开屏动画、DSH 状态桥等本地功能。仅供个人非商业使用。

Capri 是一个会待在桌面上的同人桌宠：能聊天、能说话、能陪你打游戏，会爬到窗口边缘、桌面图标和屏幕底边待着，也能在 Claude Code / Codex 干活时当它们的「身体」。她不是官方产品，和 CD PROJEKT RED、动画权利方都没有关系。

本仓库是中文社区的汉化分支，**不是**上游官方仓库。代码和素材的授权跟随上游，见下文「授权与素材」一节。

## 和上游 0.8.5.1 的关系

- 基线：上游 `lachydotmcg/capri-desktop` 的 **0.8.5.1** 版本（分支 `cn-0.8.5`）。
- 我们做的是**汉化 + 少量本地化增强**，不改变她的角色设定、人格和安全规则。
- 上游发新版本时，汉化分支会跟着合并上游改动；上游的功能、文档、Discord 仍然适用。
- 上游地址：<https://github.com/lachydotmcg/capri-desktop> · 官网 <https://capriai.app/> · Discord <https://discord.gg/capridesktop>

## 汉化分支额外加的功能

| 功能 | 说明 |
| --- | --- |
| **4 套立绘 + 随机换装** | 原版 / 清晰 / 像素 / 动漫四套立绘，右键菜单 →「立绘」切换。主控台打开「随机换装立绘」后，可自己填间隔（分钟）和每套停留（秒）：到点随机换一套，停一会儿再回到你选的那套。 |
| **开屏动画** | 启动时先播一段约 1.3 秒的 DSH 风格接入动画，再进入桌面。 |
| **DSH 状态桥** | 配合 DSH 的 `capri-bridge` 插件读取会话状态：一轮任务跑完她会播报，需要你批准时先喊你，还会报本轮 token 数。插件不在本仓库内，没装插件时这块不触发。 |
| **右键查余额** | 右键菜单「查余额」：Key 是 DeepSeek 时直接查剩余金额（充值 / 赠送分开显示）。其他服务商没有查询接口。 |
| **DeepSeek 看图** | DeepSeek V4/Flash 这类接口本身能收图，上游按纯文本模型处理，这里放开了「看屏幕」能力，配合「看看我的屏幕」等指令使用。 |
| **中文台词英文配音** | 她的免费音色只会英语。中文台词会自动先翻成英文再念出来，屏幕上显示的仍是中文原话；不想这样可在设置里关掉（NoDub）。 |

界面汉化覆盖主控台、设置、右键菜单、提示气泡等主要文案；少量角落可能还残留英文，正在陆续补，欢迎提 issue 指出位置。

## 下载与免安装包

社区反馈「不会编译，想要打包好的 exe」，所以我们除了安装版，还提供**免安装 zip**：

1. 到本仓库的 [Releases](https://github.com/Daoruisi/capri-cn/releases) 下载 `capri-cn-<版本>-portable.zip`；
2. 解压到任意文件夹（建议单独建一个，比如 `D:\Capri`），双击 `Capri.exe`；
3. 第一次运行在主控台点「设置她的脑子」或「免费 Key」，再在 VOICE 里选音色即可。

免安装版和安装版功能一致，数据都在 `%LOCALAPPDATA%\Capri`；卸载就是退出后删掉解压目录和这个数据目录。完整说明见 zip 里的 `README-免安装.txt`。

> 程序没有代码签名，Windows SmartScreen 和杀毒软件可能误报。只从本仓库 Releases 下载，并按发布页的 SHA256 核对文件。

## 自己编译

需要 **Windows 10 / 11 x64**。用系统自带的 .NET Framework 4.8 编译器（`csc.exe`）即可，**不需要 Visual Studio**；如果系统里没有 Windows SDK 的 `Windows.winmd`，先装一下 Windows 10/11 SDK。

在仓库根目录执行：

```powershell
pwsh -File ci/build-windows.ps1
```

- 产物：`out\windows\Capri.exe`（主程序）和 `out\windows\Capri-Setup.exe`（安装程序）。
- 加 `-SmokeTest` 会顺带跑一遍运行时冒烟测试（会启动一个隔离的 Capri 实例，用临时数据目录，不碰你正在用的 `%LOCALAPPDATA%\Capri`）。
- 编译会把 `pets\packs\` 下的立绘图集、`assets\` 下的字体/图标/音效嵌入 exe，缺文件会编不过。

打包免安装 zip：

```powershell
pwsh -File scripts/package-portable.ps1
```

- 若 `out\windows\Capri.exe` 不存在，脚本会先调用 `ci/build-windows.ps1` 编译。
- 产物：`dist\capri-cn-<版本>-portable.zip`，版本号取自 `app\AssemblyInfo.cs`（取不到用 exe 版本，再取不到用当天日期）。
- 可重复运行：每次先删旧 zip 再生成；`dist\` 已加进 `.gitignore`，zip 不入库。
- 想强制重新编译再打包：`pwsh -File scripts/package-portable.ps1 -Rebuild`。

## 数据与卸载

- 数据目录：`%LOCALAPPDATA%\Capri`（设置、聊天记忆、`personality.md`、`errors.txt`、本地模型和语音文件都在这里）。
- 安装版卸载：Windows「设置 → 应用」里卸载 Capri 即可；记忆文件会保留，想彻底清掉再删数据目录。
- 免安装版卸载：退出后删掉解压目录，再删 `%LOCALAPPDATA%\Capri`。
- 出问题先看 `%LOCALAPPDATA%\Capri\errors.txt`。

## 授权与素材

- **本项目是非官方同人项目**，与 Capri 上游项目、CD PROJEKT RED、动画权利方均无隶属或背书关系。
- **上游代码与素材**：跟随上游使用 [PolyForm Noncommercial License 1.0.0](LICENSE.md)。允许个人非商业使用、修改和分享；**禁止售卖**或放进付费产品；二次分发必须保留署名（Copyright 2026 Lachy，并链接上游仓库）。
- **额外立绘素材**：4 套立绘图集来自 [X1a0heZzzz/capri-desktop-pet](https://github.com/X1a0heZzzz/capri-desktop-pet) 的同人桌宠素材。该项目的**角色素材不在 MIT 许可范围内**（见其 `ASSET_RIGHTS.md`，角色素材不随代码再许可）；公开可下载不等于获得再分发授权。
  - 本仓库引入这些图集是为了方便本地构建和汉化分支的桌面宠物用途；
  - 它们会随 exe 一起被嵌入打包（`ci/build-windows.ps1` 里以资源形式嵌入）；
  - **对外再分发（包括发布带立绘的 zip）前，请自行确认并取得相应权利**；如果权利方有异议，移除 `pets\packs\` 后重新编译即可得到只有原版立绘的包。
- 三方组件与语音模型说明见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

## 反馈

- 汉化问题、打包问题：到本仓库 [Issues](https://github.com/Daoruisi/capri-cn/issues) 反馈，附上 `errors.txt` 和截图。
- 上游功能问题、帮助文档：<https://capriai.app/help/>、[上游 Discord](https://discord.gg/capridesktop)。

—— 本项目免费，禁止售卖。玩得开心。
