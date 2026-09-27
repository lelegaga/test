# 三国身份卡牌对战（Unity 6）

三国题材的多人身份类回合制卡牌对战游戏，目标平台 Android / iOS / Windows。架构采用 **Server-Authoritative Host**：
房主设备同时运行服务器逻辑与客户端，其他玩家以客户端身份加入，所有规则判定都在服务器完成。

完整设计见 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)。

## 当前进度

| 阶段 | 内容 | 状态 |
| --- | --- | --- |
| 1 | 核心：GameState、PlayerState、卡牌、事件、命令、规则引擎、状态机、模式接口 | 完成 |
| 2 | 最小可玩版本：AI 自动对局（摸牌、出牌、攻击、响应、掉血、濒死求救、死亡、换回合、胜负） | 完成 |
| 3 | 身份模式（主公/忠臣/反贼/内奸，人数与身份配置、主公先选将、主公技、奖惩、AI 身份推断） | 完成 |
| 4 | 3v3 / 5v5 / 10v10（随机/手动/房主分队、队长或平等、全灭或击杀队长、座位排列、先手补偿） | 完成 |
| 5 | 局域网服务器：TCP 协议、房间（创建/加入/准备/踢人/房主转移/设置/分队/聊天/观战）、UDP 房间发现、邀请码、Android/iOS 网络权限 | 完成 |
| 6 | 两台设备完整对局（测试中以两个独立 TCP 客户端 + AI 补位跑完整局验证） | 完成（真机验证待 UI） |
| 7 | 断线重连（重连令牌、快照恢复、命令序号续接）、超时 AI 托管、回来收回控制 | 完成 |
| 8 | 手机 UI：主菜单、单机、设置、局域网大厅、房间、牌桌（座位条/手牌/技能/按钮/战报/倒计时/目标高亮/选将/选牌/结算）、动画、音效、存档 | 完成（真机体验待阶段 9–10） |
| 9–11 | Android/iOS 真机测试与适配、性能优化 | 待开发 |

## 目录

```
Assets/Scripts/Core ...   纯 C# 规则内核（Sanguo.Core，无 UnityEngine 依赖）
Assets/Scripts/Network    纯 C# 网络层（Sanguo.Network：协议、编解码、TCP/内存传输、LanGameServer、GameClient、房间发现）
Assets/Scripts/App        Unity 层（GameManager、NetworkManager、ProfileService、Resources 内容加载、平台适配）
Assets/Scripts/UI         UGUI 界面（全部代码构建：主题、控件工厂、安全区、补间、对象池、各界面、牌桌、对话框、动画）
Assets/Scripts/Audio      程序合成的占位音效（订阅游戏事件）
Assets/Scripts/Presentation 无引擎依赖的交互模型（点选→意图命令）、座位布局、战报
Assets/Scripts/Save       本地存档（昵称、头像、音量、画质、上次服务器、上次房间设置）
Assets/Editor             编辑器脚本（一键项目设置、命令行打包 APK/AAB/iOS/Windows、iOS Info.plist 权限）
Assets/Plugins/Android    Android 局域网权限（manifest 库）
Assets/Resources/Data     cards.json · skills.json · characters.json（全部原创名称、占位美术）
Assets/Tests/EditMode     NUnit 测试（Unity Test Framework / .NET 通用）
DotNet/                   无需 Unity 的构建、测试、模拟工程
```

## 在 Unity 中打开

1. 用 Unity Hub 打开 `games/sanguo-cards`（Unity 6000.0 LTS）。
2. 进入 Play 模式：`GameManager` 自动创建并显示主菜单（无需场景或预制体，界面全部由代码构建）。
   - **单机游戏**：选择模式/人数/身份配置/选将/出牌时间，你坐 1 号位，其余由 AI 担任。
   - **局域网对战**：一台设备“创建房间”，其他设备在房间列表中加入，或输入房主 IP / 8 位邀请码加入。
   - 长按卡牌、技能或头像可查看说明；Android 返回键 / Esc 打开菜单（托管、战报、退出）。
   - 输入使用旧版 Input Manager（工程未安装 Input System 包）；若自行安装，请把 Active Input Handling 设为 Both。
3. 测试：Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All。

## 无需 Unity 的构建与测试

需要 .NET 8 SDK：

```bash
cd games/sanguo-cards/DotNet
dotnet test Sanguo.sln                                  # 编译全部工程并运行测试
dotnet run --project Sanguo.Sim -- --players 4 --seed 3  # 打印一局 AI 对局日志（玩家视角）
dotnet run --project Sanguo.Sim -- --batch 300 --players 8 --characters random  # 批量统计
dotnet run --project Sanguo.Sim -- --mode identity --players 8 --characters random --seed 5  # 身份局
dotnet run --project Sanguo.Sim -- --batch 100 --mode team10v10 --players 20 --characters random  # 10v10 统计
```

`Sanguo.Client` 与 `Sanguo.Editor` 工程分别用 NuGet 上的 UnityEngine / UGUI / UnityEditor 引用程序集编译 `Assets/Scripts/App|UI|Audio` 与 `Assets/Editor`，用于在没有 Unity 的环境中检查 Unity 层代码能否通过编译。
界面的交互逻辑（`InteractionModel`）在 .NET 测试中以“只通过点选”的方式完整打完多种模式的对局，保证界面能回答服务器的每一种请求，且界面给出的可选项从不被服务器拒绝。

## 打包

在 Unity 中执行菜单 **Sanguo ▸ Apply Recommended Settings**（横屏、IL2CPP、ARM64、包名、启动场景），然后 **Sanguo ▸ Build ▸ …**；
或命令行：

```bash
Unity -batchmode -quit -projectPath games/sanguo-cards -executeMethod Sanguo.Editor.SanguoBuild.BuildAndroidApk
Unity -batchmode -quit -projectPath games/sanguo-cards -executeMethod Sanguo.Editor.SanguoBuild.BuildAndroidAab
Unity -batchmode -quit -projectPath games/sanguo-cards -executeMethod Sanguo.Editor.SanguoBuild.BuildIOS
Unity -batchmode -quit -projectPath games/sanguo-cards -executeMethod Sanguo.Editor.SanguoBuild.BuildWindowsDebug
```

Android 正式签名通过环境变量 `SANGUO_KEYSTORE`、`SANGUO_KEYSTORE_PASS`、`SANGUO_KEY_ALIAS`、`SANGUO_KEY_PASS` 提供。

新增或删除 `Assets/` 下的文件后运行 `python3 tools/generate_meta.py`，为新文件生成确定性 GUID 的 `.meta`。

## 版权说明

开发阶段所有卡牌、技能名称均为原创，美术为占位资源；武将使用历史人物名称。之后可自行导入拥有合法授权的资源。
