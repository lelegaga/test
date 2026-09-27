# 三国身份卡牌对战 · 技术架构

> 本文是项目的总体技术设计与目录约定。所有规则逻辑都在与 Unity 无关的 `Sanguo.Core` 程序集中实现，
> 可以在 Unity（Mono / IL2CPP）、纯 .NET（测试、未来的专用服务器）中原样运行。

## 1. 目标与约束

| 目标 | 设计回应 |
| --- | --- |
| Android APK/AAB、iOS、Windows 调试版 | Unity 6 + C# 9；核心代码只用 .NET Standard 2.1 API，无反射序列化，IL2CPP 安全 |
| 局域网多人、3v3/5v5/10v10、身份模式 | Server-Authoritative Host：房主设备运行 `GameSession`（服务器）+ 本地客户端 |
| 防作弊 | 客户端只发送“意图”命令；服务器 `GameRuleEngine` 校验一切；隐藏信息按观察者裁剪后才下发 |
| AI 补位、断线重连 | AI 通过与人类相同的命令通道行动；断线超时由 AI 托管；重连发送快照 |
| 扩展新武将/卡牌/模式 | 卡牌 = 数据（JSON 效果组合）；技能 = 数据 + 可复用技能类；模式 = `IGameMode` 实现 |
| 中端手机 60 FPS | 规则层无每帧开销、增量事件同步、UI 对象池、20 人桌面分级显示 |

## 2. 技术选型

- **引擎**：Unity 6（6000.0 LTS），C# 9。
- **UI**：**UGUI**。理由：移动端卡牌游戏大量依赖精灵图集、粒子/Shader 特效、拖拽与缓动动画、`RectMask2D`
  裁剪，UGUI 在这些方面成熟且与 DOTween 类动画方案契合；UI Toolkit 运行时对自定义着色器、粒子混排和复杂动画的支持仍较弱。
  UI 完全在代码或 Prefab 中构建，并通过 `SafeArea` 组件适配刘海屏/挖孔屏。
- **网络**：
  - 房间发现：**UDP 广播**（端口 47777，每秒一次房间公告），另配合手动输入 IP 与房间邀请码；iOS 声明 Bonjour 服务类型以满足本地网络权限。
  - 对局通信：**TCP + 长度前缀二进制帧**。回合制游戏最看重可靠、有序、实现简单；TCP 天然满足，无需自建可靠 UDP。
  - 心跳 + Ping：每 2 秒一次，超时判定断线。
- **数据**：内容 JSON（`Assets/Resources/Data`）+ 自研轻量 JSON DOM（`Sanguo.Data.JsonValue`），不依赖 Newtonsoft/反射，IL2CPP 无裁剪风险；后续可叠加 ScriptableObject 编辑器导入。
- **测试**：NUnit（Unity Test Framework 使用同一套 API）。`DotNet/` 目录提供无需 Unity 的构建与测试工程，CI 与本地均可运行。

## 3. 分层

```
┌──────────────────────────────────────────────────────────────┐
│ Sanguo.Client（Unity）  UI · 动画 · 音频 · 存档 · GameManager    │  只持有 ClientGameState（表现层副本）
├──────────────────────────────────────────────────────────────┤
│ Sanguo.Network（纯 C#） INetworkTransport · IGameServer ·        │  LanGameServer / 未来 DedicatedGameServer
│                        IRoomService · 协议编解码 · 房间发现       │
├──────────────────────────────────────────────────────────────┤
│ Sanguo.Core（纯 C#，noEngineReferences）                          │
│   Game: GameEngine · GameSession · 结算动作                        │
│   Core: GameState · 状态机 · 命令 · 请求 · 规则引擎 · 修正器 · 可见性 │
│   Cards · Effects · Characters · Skills · GameModes · AI · Events │
└──────────────────────────────────────────────────────────────┘
```

依赖只能自上而下。`Sanguo.Core` 不引用 UnityEngine，因此规则逻辑可以在专用服务器、测试和工具中复用。

### 程序集（asmdef）

| 程序集 | 位置 | 说明 |
| --- | --- | --- |
| `Sanguo.Core` | `Scripts/Core/Sanguo.Core.asmdef`，其余核心目录通过 `.asmref` 并入 | 规则内核，`noEngineReferences: true` |
| `Sanguo.Client` | `Scripts/App/Sanguo.Client.asmdef` | Unity 层：GameManager、内容加载、（阶段 8）UI/音频/存档 |
| `Sanguo.Network` | `Scripts/Network/Sanguo.Network.asmdef` | 纯 C# 网络层（`noEngineReferences: true`） |
| `Sanguo.Editor` | `Editor/Sanguo.Editor.asmdef` | 编辑器：项目设置、打包、iOS plist 后处理 |
| `Sanguo.Tests.EditMode` | `Tests/EditMode/` | 编辑器测试，同时可由 `DotNet/Sanguo.Tests` 在 .NET 下运行 |

## 4. 目录结构

```
games/sanguo-cards/                  Unity 工程根目录
├── Assets/
│   ├── Scripts/
│   │   ├── Core/        状态(State)、流程(Flow)、命令(Commands)、请求(Requests)、规则(Rules)、
│   │   │                修正器(Modifiers)、可见性/快照(Visibility)、GameContext
│   │   ├── Events/      GameEvent 及全部事件、GameEventBus、EventLog、GameLogFormatter
│   │   ├── Cards/       CardBase/BasicCard/TrickCard/EquipmentCard、CardInstance、TargetRule、CardDatabase、牌堆
│   │   ├── Effects/     ICardEffect、IContinuousEffect、各效果、EffectRegistry
│   │   ├── Characters/  CharacterData、CharacterDatabase
│   │   ├── Skills/      SkillBase 及三类基类、SkillTriggerManager、SkillRegistry、Builtin/ 内置技能类
│   │   ├── GameModes/   IGameMode、GameModeBase、GameModeConfig、胜利条件、模式注册表
│   │   ├── Game/        GameEngine（权威引擎）、GameSession（主机编排）、Actions/ 结算动作
│   │   ├── AI/          PlayerPerspective、HeuristicAI、AIActionEvaluator、技能顾问、AIController
│   │   ├── Data/        JsonValue、GameContent、ContentLoader、内容来源
│   │   ├── Utils/       确定性随机数、时钟、对象池
│   │   ├── App/         （Unity）GameManager、Resources 内容加载、调试对局视图
│   │   ├── Network/     协议/编解码(Protocol)、序列化、传输(Transport)、房间(Rooms)、服务器(Server)、客户端(Client)、发现(Discovery)
│   │   ├── UI/ Save/ Audio/（阶段 8）
│   ├── Editor/          项目设置、打包脚本、iOS Info.plist 后处理
│   ├── Plugins/Android/ SanguoNetwork.androidlib（局域网权限）
│   ├── Resources/Data/  cards.json · skills.json · characters.json
│   └── Tests/EditMode/  NUnit 测试
├── Packages/manifest.json
├── ProjectSettings/ProjectVersion.txt
├── DotNet/              无 Unity 的构建/测试/模拟工程（Sanguo.sln）
├── docs/                本文档
└── tools/               generate_meta.py（生成确定性 GUID 的 .meta）
```

## 5. 核心数据流

```
客户端 UI ──(PlayCardCommand{CardID=123, Targets=[5], Seq, RequestId})──▶ 服务器
                                                                         │
     GameEngine.Submit ─▶ 序号检查(重复/乱序) ─▶ GameRuleEngine 校验(阶段/回合/归属/距离/次数/存活/目标)
                                                                         │ 合法
     请求被回答 ─▶ 动作栈推进（UseCard → 响应请求 → Damage → Dying → Death …）直到需要玩家输入
                                                                         │
     GameStateMutator 修改 GameState，同时写入 GameEvent（带递增序号）
                                                                         │
     GameSession 按观察者裁剪事件（ProjectFor）──▶ 各客户端 ClientGameState.Apply（增量）
```

- 客户端**不可能**调用 `TakeDamage`/`DrawCard`/`ChangeHP`：这些操作只存在于 `GameStateMutator`，而它只能通过
  服务器端的 `GameContext` 访问，客户端进程里根本没有 `GameContext`。
- 伤害、胜负、身份等全部由服务器计算；客户端发送的只有“意图”。

## 6. 状态机与回合

`GameStateMachine` 维护阶段并只接受合法迁移：

```
Waiting → Preparing → GameStart → TurnStart → JudgePhase → DrawPhase → PlayPhase → DiscardPhase → TurnEnd → TurnStart …
任意对局阶段 → GameOver；TurnStart/Judge/Draw/Play → TurnEnd（当前角色阵亡或回合被终止）
WaitingResponse：覆盖状态，有响应请求打开时生效，关闭后回到原阶段
```

技能不能跳转状态机：“跳过出牌阶段”是由效果标记，`TurnAction` 仍然进入该阶段并立即离开（发出 `PhaseSkipped`）。

## 7. 动作栈（结算引擎）

规则结算不是递归函数调用，而是显式的**动作栈**（`ActionStack` + `GameAction.Step`）：

- 每个动作是一个小状态机，通过 `ctx.Push` 压入子动作并返回 `Continue`；子动作结算完才继续父动作。
- 需要玩家输入时动作打开一个请求（`PendingRequest`）并返回 `Wait`；引擎暂停，等到命令或超时再继续。
- 任意深度都可以等待玩家输入（例如：决斗中濒死 → 求桃 → 治疗触发技能 → 询问是否发动）。
- 安全阀：栈深上限 256、单次推进步数上限 20 万、技能触发嵌套上限 8 层 —— 保证不存在无限循环。
- 引擎单线程、确定性：**相同种子 + 相同命令序列 = 相同对局**，可直接用于录像回放和问题复现。

## 8. 事件与触发

两套机制，职责分离：

| | GameEvent（通知） | Trigger（触发点） |
| --- | --- | --- |
| 作用 | 描述“已经发生了什么” | 在结算中间提供可干预的时机 |
| 使用者 | UI、日志、动画、音频、网络广播、AI 记账 | 技能 |
| 例子 | `CardPlayed`、`DamageApplied`、`HpChanged`、`PlayerDied` | `OnDamageBefore`（可改伤害值）、`OnCardTargeted`（可取消目标） |

触发流程：结算到时机 → `SkillTriggerManager.Fire` 按座次（从当前回合角色起）收集技能 → 生成 `TriggerQueueAction`
→ 服务器依次处理（可选技能先询问拥有者，锁定技直接生效）→ 原动作继续。

## 9. 卡牌与效果（数据驱动）

卡牌定义只由数据组成，引擎中不存在 `if (cardName == ...)`：

```json
{ "id": "strike", "name": "攻击", "type": "basic",
  "target": { "kind": "single", "filters": ["notSelf"] }, "range": "attack",
  "usageKey": "strike", "usageLimit": 1,
  "effects": [ { "type": "RequireResponse", "card": "dodge",
                 "onFail": [ { "type": "Damage", "amount": 1 } ] } ] }
```

- 即时效果 `ICardEffect`：Damage、Heal、DrawCard、Discard、StealCard、AOE、Equip、RequireResponse、Duel、DelayedTrick、SkipPhase，可任意组合嵌套。
- 持续效果 `IContinuousEffect`（装备）：DistanceModifier、AttackRange、UsageLimit，通过修正器系统影响距离/攻击范围/次数。
- 新效果类型只需在 `EffectRegistry` 注册工厂；AI 通过遍历效果列表自动理解新卡牌。

## 10. 武将与技能

- `CharacterData`：ID、名称、势力、体力上限、性别、技能列表、描述、立绘键（JSON，可叠加 ScriptableObject）。
- `SkillBase` → `ActiveSkillBase`（主动技）/ `TriggerSkillBase`（触发技）/ `PassiveSkillBase`（被动/修正）；
  `IsLocked`（锁定技）、`IsLimited`（限定技）为标记。
- skills.json 中每个技能 = `class`（C# 技能类）+ `params`（数值）。复用已有机制的新武将只需要数据；
  新机制新增一个技能类并注册，无需修改 GameManager / 回合 / 网络代码。

## 11. 模式系统

`IGameMode` 负责：座位、队伍、身份、阵营、身份可见性、行动顺序、体力加成、击杀奖惩、胜利判定。
胜利条件是可组合的 `IVictoryCondition`，只在服务器评估，UI 不参与。

| 模式 | 状态 |
| --- | --- |
| `FreeForAllMode`（混战，最小可玩版本） | 已完成 |
| `IdentityMode`（主公/忠臣/反贼/内奸，人数与身份分布可配置，主公先选将、主公技、击杀奖惩） | 已完成 |
| `TeamBattleMode` 及 `Team3v3Mode` / `Team5v5Mode` / `Team10v10Mode`（同一实现，仅队伍人数不同；分队方式、队长、胜负规则、座位排列、先手补偿均由配置控制） | 已完成 |
| 1v1、2v2、国战、竞技场、PVE、BOSS | 预留：实现 `IGameMode` 并注册到 `GameModeRegistry` |

## 12. 隐藏信息与同步

- 服务器持有唯一 `GameState`；客户端持有 `ClientGameState`（表现层副本）。
- **快照**（`SnapshotBuilder.Build(ctx, viewerId)`）：进入游戏、断线重连、检测到异常时发送。
  其他玩家手牌只给数量；隐藏身份为 `Unknown`；私密请求细节（如选将选项）只给被询问者。
- **增量事件**：正常对局只发事件。`CardMoveEvent` 根据区域自动判定可见性：涉及公开区（弃牌堆、处理区、装备区、判定区）即公开；
  手牌/牌堆之间的移动只对相关手牌拥有者可见，其他人只收到数量，且**不下发隐藏牌的实例 ID**，防止跨移动追踪。
- 每个事件带序号，客户端检测到断档即请求快照。测试保证“快照 + 事件流 = 新快照”，对每个观察者逐步校验。

## 13. 网络

- 抽象：`INetworkTransport`（监听/连接，`IConnection` 收发消息）、`IGameServer`（接入玩家、转发命令、下发事件）、
  `IRoomService`（发现/创建房间）。`TcpNetworkTransport` + `LanGameServer` + `LanRoomService`（UDP 广播）是局域网实现；
  `InMemoryNetworkTransport` 用于测试。`LanGameServer` 与传输无关，将来的 `DedicatedGameServer` 直接复用它和 `GameSession`。
- `NetworkMessage`：`MessageType`、`PlayerID`、`RoomID`、`SequenceID`、`Timestamp`、`Payload`；帧格式为 4 字节长度前缀 + 手写二进制
  （zig-zag varint、UTF-8 字符串），单帧上限 1 MB，读取端对所有长度做边界检查，随机垃圾数据只会抛 `ProtocolException`。
- 消息：Hello/Welcome、CreateRoom/JoinRoom/JoinAccepted/JoinRejected/LeaveRoom/RoomState、PlayerReady、KickPlayer、TransferHost、
  UpdateRoomSettings、ChangeTeam、Chat、StartGame、PlayCard/UseSkill/SelectTarget/RespondCard/EndTurn、SetAutoPlay、CommandResult、
  GameStateSync（快照）、GameEvents（增量批次）、RequestResync、Reconnect/ReconnectAccepted/ReconnectRejected、GameFinished、Ping/Pong。
- 安全：服务器**忽略客户端声明的 PlayerID**，一律使用连接绑定的座位；握手前的消息、协议版本不符、畸形帧、刷屏（超过每秒上限）直接断开；
  被限流的命令会收到明确的拒绝；房主权限（设置/开始/踢人/转移）在服务器校验；重连令牌由加密随机数生成。
- 同步：开局与重连发送该座位的快照，之后每帧把该座位的裁剪事件合并成一个 `GameEvents` 批次；公开事件对多名观察者只编码一次。
  客户端发现序号断档即发 `RequestResync`。
- 服务器提示：出牌请求附带“可用牌/合法目标/可用技能/可响应的牌”（仅发给被询问者），客户端 UI 无需规则代码即可高亮合法目标，
  服务器仍会完整校验。
- 断线重连：座位保留，客户端带 `RoomID + 座位 + ReconnectToken` 自动重连（指数退避），服务器校验后发送快照与最后接受的命令序号；
  断线超过 `DisconnectAITakeoverMs` 由 AI 托管，重连后收回控制权。AI 代打的命令走主机内部通道，不占用玩家的命令序号。
- 房间发现：房主每秒向受限广播地址与各网卡定向广播地址发送 UDP 公告（端口 47777），浏览端以包源地址为准、3.5 秒过期；
  **邀请码**把房主 IPv4 + 端口编码为 8 位（如 `C1M0-G1A5`），广播被屏蔽时可直接输入。
- 平台：Android 通过 `Plugins/Android/SanguoNetwork.androidlib` 合并 `INTERNET`、`ACCESS_NETWORK_STATE`、`ACCESS_WIFI_STATE`、
  `CHANGE_WIFI_MULTICAST_STATE` 权限，浏览/建房时持有 `MulticastLock`；iOS 打包后自动写入 `NSLocalNetworkUsageDescription`
  与 `NSBonjourServices`。iOS 14.5+ 收发 UDP 广播需要 Apple 审批的 multicast entitlement，因此 iOS 端以邀请码/手动 IP 为主，
  Bonjour（mDNS）发现在阶段 10 以原生插件实现（服务类型已在 plist 中声明）。前后台切换时客户端自动重连。

## 14. AI

```
AIController → PlayerPerspective（只含该玩家可知信息）
            → HeuristicAI：生成全部合法行动（经 GameRuleEngine 校验）→ AIActionEvaluator 打分 → 执行最高分
```

打分因素：伤害/治疗/击杀收益、敌友关系（hostility）、自身体力、手牌数量；响应、弃牌、选牌、选目标各有策略。
`IAIDecisionMaker` 接口预留蒙特卡洛、强化学习、LLM 等实现。身份模式中 AI 使用 `IdentityRelationEstimator`：
只根据本座位收到的公开事件（谁攻击/救助了谁、阵亡翻开的身份）估计每名角色对主公的“亲疏度”，再结合自己的秘密身份
换算敌友（反贼视主公为敌、内奸在反贼未清前保护主公等）。

## 15. UI 规划（阶段 8）

横屏 1920×1080 参考分辨率（CanvasScaler，按高度匹配），`SafeArea` 适配。
布局：中央牌桌、底部本人角色/体力/技能/手牌、左侧日志、右侧按钮（出牌/取消/确定/结束出牌）、上方其他玩家。
10v10：当前行动者与本人重点显示，邻近玩家中等头像，远端玩家小头像，横向滑动查看全部；头像、卡牌、飘字全部走对象池。
动画只消费事件，逻辑永不等待动画。

## 16. 性能

- 规则层零每帧开销：引擎只在命令/超时时推进；`Update` 中只有会话节拍。
- 热路径使用 `ListPool`，避免 LINQ；事件对象小而扁平。
- 增量同步：20 人对局约 2300 个事件/局，每次操作只广播几个事件；快照仅在进入/重连/异常时发送。
- 基准（本机 .NET 8，AI 自动对局）：4 人局约 3 ms/局，20 人局约 9 ms/局。

## 17. 长期扩展

互联网服务器、账号、好友、匹配、排位、战绩、观战（观察者 viewerId = -1 已支持）、录像回放（种子 + 命令日志 / 完整事件历史）、
排行榜、赛季、皮肤、扩展包（`ContentLoader.LoadInto` 追加内容）、新模式 —— 均不需要改动规则内核。
