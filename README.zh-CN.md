# Capri 简体中文分支（面向中文开发者）

> 非官方分支。原项目：[lachydotmcg/capri-desktop](https://github.com/lachydotmcg/capri-desktop)（PolyForm Noncommercial 1.0.0）。
> 本分支只做**汉化**和几处**本地可用性**改动，不改她的性格、人设和安全规则。

---

## 一、这个分支多了什么

| 功能 | 文件 | 说明 |
|---|---|---|
| 简体中文界面 | `localization/trans.tsv` + 构建后打补丁 | 主控台、右键菜单、托盘气泡，约 270 条 |
| 简体中文字幕 | `app/*.cs`（已直接改在源码里） | 264 条固定台词（动作、语音、报错、彩蛋） |
| DeepSeek 能看图 | `app/Brain.cs`、`app/Capri.cs` | 原版把 DeepSeek 一律当"看不见屏幕"，这里删掉那三处判断（V4.1-Flash 实测支持图片） |
| 截图兜底 | `app/Capri.cs` 的 `CaptureScreen()` | 前台窗口那台屏抓成全黑时，改抓整个桌面重试 |
| `check_balance` 工具 | `app/Hands.cs`、`app/Brain.cs` | 问她"我还有多少额度"会真的去查 `/user/balance` |
| 双击查余额 | `app/Capri.cs` 的 `ShowBalance()` | 双击她 = 查余额并说出来（原来的"骑鼠标"移到右键菜单） |
| 中文台词英文配音 | `app/Brain.cs` 的 `Translate()` + `app/Capri.cs` 的 `Dubbed()` | 字幕还是中文，嘴里用原版英文嗓子念英文译文 |
| DSH 状态桥 | `plugins/capri-bridge/` | 把 DeepSeek Harness 会话状态写到本地文件，她完成一轮会主动播报 |

---

## 二、构建

原项目的 `ci/build-windows.ps1` 用 **.NET Framework 4.8 的 csc.exe** 直接编译，不依赖 dotnet SDK，但需要 Windows SDK 的 WinRT 元数据：

```
C:\Program Files (x86)\Windows Kits\10\UnionMetadata\<版本>\Windows.winmd
```

没装 Windows SDK 时 `csc` 会报 `CS0234 Windows.Media.SpeechRecognition 不存在`。

本分支多给了 `ci/build-cn.ps1`，和官方脚本只有一处差别：**自动找 winmd**（Windows Kits → 系统自带 `C:\Windows\System32\WinMetadata` → NuGet 缓存），其余编译参数完全照抄官方。

```powershell
pwsh -NoProfile -File .\ci\build-cn.ps1
# 产物：out\windows\Capri.exe
```

`out/` 已在 `.gitignore` 里。

---

## 三、汉化是怎么做的（两层）

原版的字符串全部写死在 C# 里，没有 i18n 表，所以分两层处理：

**1）她的台词 —— 已改在源码里**

`localization/speech-map.tsv` 是英中对照（264 条）。用 `SourceSwap.cs` 编译出 `swap.exe`，对 `app/` 做整句精确替换：

```powershell
csc /out:swap.exe localization\SourceSwap.cs
.\swap.exe app localization\speech-map.tsv
```

拼接字符串（`"...。" + Brain.Stuck` 这类）匹配不到整句，需要手工改，见 `Brain.Stuck` / `Capri.GuideLine` / `Hands.cs` 的网站前缀。

**2）界面文案 —— 构建后打二进制补丁**

`localization/trans.tsv` 是英中对照，`scopes.txt` 限定只在这 20 个方法里替换（防止误改逻辑里当键用的字符串）。`Patch.cs` 用 Mono.Cecil 直接改 IL 里的 `ldstr`：

```powershell
# 需要 Mono.Cecil（net40 版）
csc /out:patch.exe /reference:Mono.Cecil.dll localization\Patch.cs
.\patch.exe out\windows\Capri.exe Capri-cn.exe localization\trans.tsv localization\scopes.txt
```

输出会列出 `unused`（没命中的条目，通常是上游改了文案）。

> 为什么界面文案不也改源码？因为它散在 19k 行的 `Capri.cs` 里、还混着大量 `--smoke-test` 断言，二进制替换更安全、可回滚。

---

## 四、新增功能怎么用

### 查余额

主控台把大脑接上 DeepSeek Key（`Tier=2`）后：

- 问她「我还有多少额度 / 余额还剩多少 / 你花了多少钱」
- 或**双击她**

她会调 `GET https://api.deepseek.com/user/balance`，用当前大脑那把 Key。**Key 不落盘、不写进 exe**，只在内存里从 `brain.Remote.Key` 取。

非 DeepSeek 的 Key 会直接说明"只有 DeepSeek 能查"。

### 中文台词英文配音

免费本地嗓子（Kokoro）只装英文音素库，念中文是洋腔。这个分支会在发声前把中文丢给当前大脑翻成英文（`Brain.Translate()`），再把译文喂给原嗓子；**屏幕上仍是中文**。

- 只对「含汉字」+「当前声音只会英文」的句子生效
- 按原句缓存（最多 64 条），同一句重复说不重复花钱
- 译文里汉字超过 20% 视为翻译失败，直接用原句
- 想关掉：注释 `Voiced()` 开头的 `words = Dubbed(words);` 重新编译

### DSH 状态桥（可选）

见下节。

---

## 五、DSH 状态桥安装

`plugins/capri-bridge/` 是个 DeepSeek Harness 宿主插件，把会话状态写到：

```
%LOCALAPPDATA%\Capri\dsh-state.json
```

Capri 每 1.5 秒读一次，`doneId` 变化时播报「「<会话标题>」搞定了：<最后一句>」。

手动安装：

1. 把 `plugins/capri-bridge` 复制到 `%USERPROFILE%\.dsh\plugins\capri-bridge`
2. 在 `%USERPROFILE%\.dsh\profiles\<当前 profile>\package.json` 里：
   - `dependencies` 加 `"capri-bridge": "link:C:/Users/<你>/.dsh/plugins/capri-bridge"`
   - `dsh.profile.bundles` 数组里加 `"capri-bridge"`
3. 建目录联接：`mklink /J "%USERPROFILE%\.dsh\profiles\<profile>\node_modules\capri-bridge" "%USERPROFILE%\.dsh\plugins\capri-bridge"`
4. 重启 DeepSeek Harness

状态文件格式：

```json
{ "at": 0, "sessionId": "...", "title": "会话标题", "status": "thinking|working|done|idle",
  "detail": "跑命令：npm test", "summary": "她读到的一句话", "doneAt": 0, "doneId": 3 }
```

插件监听的事件：`session/event`（`turn/start`、`tool/call`、`tool/result`、`assistant/message`、`turn/end`、`session/title`）和 `agent/status`。

---

## 六、已知限制

- **中文语音靠的是别的模型**：想让她用中文嗓子说话，得在 `声音 → 自定义本地` 里接一个 sherpa-onnx 中文模型（Piper / Kokoro multi-lang / MeloTTS）。本分支不内置中文语音包。
- **配音是"中文台词 → 英文语音"**，不是"中文说成中文"；她嘴里说的是英文。
- **DeepSeek 看图**已打开，但它仍可能对屏幕内容描述得比专门的视觉模型粗。
- **DSH 桥只播报"完成"**，运行中的 `thinking/working` 已经写进状态文件，但还没接进她的任务面板。
- 上游一更新，`trans.tsv` 里的文案可能失效（`patch.exe` 会打印 `unused`），`check_balance` 这类改动需要重新 rebase。

---

## 七、许可与署名

- 上游代码与美术：**PolyForm Noncommercial 1.0.0**，禁止商用；分发任何部分都要保留 `LICENSE.md` 与对 [lachydotmcg/capri-desktop](https://github.com/lachydotmcg/capri-desktop) 的署名。
- 本分支的汉化文本、`localization/` 工具、`plugins/capri-bridge/` 同样按上述非商业条款提供。
- Capri 是同人项目，与 CD PROJEKT RED、动画版权方及声优无隶属或背书关系。
- 语音部分请遵守原项目对"克隆/模仿真人声音"的限制。

---

## 八、立绘桌宠 `sprite-pet/`（与本体的关系）

`sprite-pet/` 是一个**只做立绘**的独立小程序（单文件 `Pet.cs`，约 480 行，编译出来 16 KB）：

- 三套素材：`pets/capri-clear`、`pets/capri-pixel`、`pets/capri-anime`（各 8 列 × 9 行，192×208/帧）
- 右键换装 / 调大小 / 总在最前；拖拽、双击挥手、自己溜达；托盘可「呼到这里」
- 分层窗口 + 逐像素透明，能压在任何窗口上，不依赖 DSH
- 编译：`sprite-pet\build.cmd`（用系统自带 csc，不需要 SDK），或
  ```
  csc /target:winexe /out:CapriPet.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll Pet.cs
  ```

它和主程序 `Capri.exe` **互不依赖**：主程序管大脑/语音/工具，这个只管把立绘放到桌面上。
两边可以同时跑，也可以只跑其中一个。

### 素材授权（重要）

`pets/` 下的图集来自 [X1a0heZzzz/capri-desktop-pet](https://github.com/X1a0heZzzz/capri-desktop-pet)，
该项目的 `ASSET_RIGHTS.md` 明确：**MIT 只覆盖代码，不覆盖角色素材**，公开可下载不等于获得再分发授权。
这里存放只为构建方便，**不在本仓库许可证范围内**；对外再分发前请自行确认权利，权利方可要求移除。