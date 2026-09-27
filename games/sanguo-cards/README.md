# 三国身份卡牌对战（Unity 6）

三国题材的多人身份类回合制卡牌对战游戏，目标平台 Android / iOS / Windows。架构采用 **Server-Authoritative Host**：
房主设备同时运行服务器逻辑与客户端，其他玩家以客户端身份加入，所有规则判定都在服务器完成。

完整设计见 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)。

## 当前进度

| 阶段 | 内容 | 状态 |
| --- | --- | --- |
| 1 | 核心：GameState、PlayerState、卡牌、事件、命令、规则引擎、状态机、模式接口 | 完成 |
| 2 | 最小可玩版本：AI 自动对局（摸牌、出牌、攻击、响应、掉血、濒死求救、死亡、换回合、胜负） | 完成 |
| 3 | 身份模式（主公/忠臣/反贼/内奸） | 待开发 |
| 4 | 3v3 / 5v5 / 10v10 | 待开发 |
| 5–7 | 局域网服务器、两机完整对局、断线重连 | 待开发 |
| 8–11 | 手机 UI、Android/iOS 真机测试、性能优化 | 待开发 |

## 目录

```
Assets/Scripts/Core ...   纯 C# 规则内核（Sanguo.Core，无 UnityEngine 依赖）
Assets/Scripts/App        Unity 层（GameManager、Resources 内容加载、临时调试视图）
Assets/Resources/Data     cards.json · skills.json · characters.json（全部原创名称、占位美术）
Assets/Tests/EditMode     NUnit 测试（Unity Test Framework / .NET 通用）
DotNet/                   无需 Unity 的构建、测试、模拟工程
```

## 在 Unity 中打开

1. 用 Unity Hub 打开 `games/sanguo-cards`（Unity 6000.0 LTS）。
2. 进入 Play 模式：`GameManager` 自动创建，并开始一局可观看的 4 人 AI 混战（临时 IMGUI 视图，阶段 8 替换为正式 UI）。
3. 测试：Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All。

## 无需 Unity 的构建与测试

需要 .NET 8 SDK：

```bash
cd games/sanguo-cards/DotNet
dotnet test Sanguo.sln                                  # 编译全部工程并运行测试
dotnet run --project Sanguo.Sim -- --players 4 --seed 3  # 打印一局 AI 对局日志（玩家视角）
dotnet run --project Sanguo.Sim -- --batch 300 --players 8 --characters random  # 批量统计
```

`Sanguo.Client` 工程会用 NuGet 上的 UnityEngine 引用程序集编译 `Assets/Scripts/App`，用于在没有 Unity 的环境中检查 Unity 层代码能否通过编译。

新增或删除 `Assets/` 下的文件后运行 `python3 tools/generate_meta.py`，为新文件生成确定性 GUID 的 `.meta`。

## 版权说明

开发阶段所有卡牌、技能名称均为原创，美术为占位资源；武将使用历史人物名称。之后可自行导入拥有合法授权的资源。
