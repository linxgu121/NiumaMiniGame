# NiumaMiniGame

## 模块定位
NiumaMiniGame 是联机小游戏前端模块，当前核心玩法为你画我猜/画猜传话，负责房间协议、Mock 客户端、网络抽象、房间黑板、绘画点位、聊天、礼物、游戏中 UI 数据和 Gal 入口桥接。

## 框架设计思路
- 前端通过 IRealtimeNetworkClient 发消息，业务层不直接依赖具体 TCP/UDP 实现。
- 先用 MockRoomServer 验证玩法，再替换 Java Netty 后端，业务层不改。
- 可靠消息用于房间、阶段、聊天、提交答案；绘画点位后续可走 UDP 或降级 TCP。
- GalBridge 通过 MiniGameDialogueActionHandler 接收 DialogueActionType.OpenMiniGame 并调用 NiumaScene 进入 2D 小游戏场景。

## 核心流程
1. NPC 对话选项触发 OpenMiniGame。
2. NiumaScene 切换到 MiniGame 开始场景并压入返回上下文。
3. Start UI 输入昵称，进入准备界面。
4. 创建/加入/观战房间。
5. 房间内同步玩家、房主、聊天、准备状态和模式。
6. 房主开始游戏后进入游戏中 UI，按阶段展示画板、回答、评价和倒计时。
7. 游戏结束统一返回房间。
8. 退出小游戏场景时优先通过 NiumaScene 返回 RPG；如果是直接从 Unity 打开 MiniGame 场景测试，没有返回上下文，则使用 StartScreen 上配置的兜底 RPG 场景名返回。

## 模块用法
- 场景中放置 NiumaMiniGameController，并绑定网络客户端或 Mock 客户端。
- UI 通过 MiniGameUIViewBridge 接收 MiniGamePanelViewData。
- 对话入口不再使用旧二选一面板脚本，统一使用 Gal 选项 + MiniGameDialogueActionHandler。

## 场景使用方法
推荐放置方式：MiniGame 拆成两个功能集物体：`MiniGameRoot` 管网络/房间，`MiniGameUIRoot` 管开始/房间/游戏中 UI。

- RPG 场景 `NPC_MiniGame`：对话选项配置 OpenMiniGame Action。
- RPG 场景 `MiniGameGalBridge`：挂 `MiniGameDialogueActionHandler`，绑定 NiumaDialogueController 和 NiumaSceneController。
- MiniGame 开始/房间场景 `MiniGameRoot`：挂 `NiumaMiniGameController`，绑定 IRealtimeNetworkClient 实现或 MockRealtimeNetworkClient。
- `MiniGameUIRoot/StartScreen`：挂 `MiniGameStartScreenUI`，新版流程绑定一套页面即可：`HomePanel` 入口页、`NamingPanel` 取名页、`PreparePanel` 预备页、`RoomInputPanel` 房间号输入页、`RoomPanel` 房间大厅页。`EntryPanel` 只用于旧版兼容，新 UI 不建议再绑。
- `MiniGameUIRoot/StartScreen` 场景跳转配置：绑定 `SceneController` 为全局 `NiumaSceneController`；`Fallback Return Scene Name` 填外部 RPG 场景名，例如 `TestSence1`。正式从 RPG 入口进入时会走 ReturnContext；直接打开 MiniGame 场景测试时会用该兜底场景。
- `MiniGameUIRoot/StartScreen/HomePanel`：放“开始游戏”和“退出游戏”。开始游戏按钮绑 `EnterGameButton`，退出游戏按钮绑 `ExitGameButton`，退出游戏会先调用 `NiumaSceneController.ReturnToPreviousScene()` 返回 RPG；若报 `ReturnContextMissing`，则加载 `Fallback Return Scene Name`。
- `MiniGameUIRoot/StartScreen/NamingPanel`：放昵称输入框、确认按钮和返回按钮。确认按钮绑 `ConfirmNameButton`，返回按钮绑 `NamingBackButton`。
- `MiniGameUIRoot/StartScreen/PreparePanel`：放创建房间、加入房间、观战加入和返回按钮。返回按钮绑 `PrepareBackButton`，只返回入口页，不退出小游戏。
- `MiniGameUIRoot/StartScreen/RoomInputPanel`：放房间号输入框、进入按钮和返回按钮。进入按钮绑 `RoomInputEnterButton`，返回按钮绑 `RoomInputBackButton`。
- `MiniGameUIRoot/StartScreen/RoomPanel`：放房间号、当前玩家数量、玩家昵称列表、观战者昵称列表、聊天框、房主开始游戏、普通玩家准备、房间返回和退出游戏。房间返回按钮绑 `RoomBackButton` 或 `LeaveRoomButton`，只离开房间回预备页；退出游戏按钮绑 `ExitGameButton`，返回 RPG。
- 房间大厅观战者显示：若 UI 只有一个名单文本，绑定 `NicknameListText` 会同时显示玩家和观战者；若 UI 分开显示，则 `PlayersText` 绑定玩家列表，`ViewersText` 绑定观战者列表。
- `MiniGameUIRoot/Bridge`：挂 `MiniGameUIViewBridge`，绑定 NiumaMiniGameController 和 StartScreen/GameScreen Receiver。
- 游戏中场景 `MiniGameUIRoot/GameplayScreen`：挂 `MiniGameGameplayScreenUI`。
- `MiniGameUIRoot/DrawingCanvas`：挂 `MiniGameDrawingCanvas`，绑定画布 RawImage/Texture、笔刷工具和输入事件。
- 礼物按钮物体挂 `MiniGameGiftDragButton`，拖拽目标限制在画布或答案模块。
- 游戏结束返回房间由 UI/Controller 处理；退出整个小游戏时通过 `MiniGameGameplaySceneReturnBridge` 或 NiumaScene.ReturnToPreviousScene 回 RPG。
- 开发测试场景可挂 `MiniGameMockTestRunner` 和 `MiniGameUIDebugReceiver`。

## 协作边界
MiniGame 不直接操作 RPG 玩家控制和存档。场景切换交给 NiumaScene，入口选择交给 NiumaGal，联机权威状态由后端房间状态机托管。

## 场景挂载与 Inspector 配置
### NiumaMiniGameController
建议挂载位置：`CoreScene/BootstrapRoot/MiniGameSessionRoot`。如果 MiniGame 只有一个独立场景，也可以先放在 MiniGame 开始场景。

用途：管理连接、PlayerId、RoomId、房间快照、聊天、准备、开始游戏和网络客户端。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Network Client Provider` | 拖 Mock 或真实网络客户端脚本 | 不可以 | 无法连接房间服务 |
| `Display Name` | 默认昵称，可由 UI 输入覆盖 | 可以 | 为空时可能显示短 PlayerId |
| `Default Room Id / Mode Id` | 测试时可填 | 可以 | UI 创建/加入时会用输入框覆盖 |
| `Auto Connect On Start` | 测试可开，正式通常由按钮触发 | 可以 | 关闭后需要 UI 按钮调用 Connect |
| `Register Service To Context` | 核心场景可开 | 可以 | 其他模块无法通过 GameContext 获取 MiniGame 服务 |

### MiniGameUIViewBridge
建议挂载位置：`MiniGameSessionRoot` 或 MiniGame UI 根节点。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `MiniGame Controller` | 拖 `NiumaMiniGameController` | 不建议 | UI 不刷新 |
| `Mini Game UI Receiver Provider` | 拖 `MiniGameStartScreenUI` 或游戏中 UI 接收脚本 | 不可以 | 房间数据无处显示 |

### MiniGameStartScreenUI
建议挂载位置：MiniGame 开始场景的 UI 根物体。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `MiniGame Controller` | 拖 `NiumaMiniGameController` | 不建议 | 按钮无法操作房间 |
| `Scene Controller` | 拖核心场景 `NiumaSceneController` | 退出/切玩法场景时不建议 | 退出游戏、进入玩法场景会失败 |
| `Home / Naming / Prepare / RoomInput / Room Panel` | 拖对应 UI 根节点 | 按流程决定 | 留空会走兼容流程或不控制该页显示 |
| `Exit Game Button` | 拖退出 MiniGame 回 RPG 的按钮 | 可以 | 需要手动绑定事件 |
| `Back Buttons` | 拖返回上一个 MiniGame 页面按钮 | 可以 | 页面返回需要手动处理 |
| `Gameplay Scene Name` | 填游戏中场景名 | 分场景玩法不可以 | 房间进入 Playing 后不会自动切玩法场景 |
| `Fallback Return Scene Name` | 测试时填 RPG 场景名 | 可以 | 没有 ReturnContext 时无法退出回 RPG |

### MiniGameDialogueActionHandler
建议挂载位置：RPG 中 MiniGame NPC 所在场景，或 Gal 行为桥接根物体。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Dialogue Controller` | 拖 `NiumaDialogueController` | 不建议 | 无法注册 OpenMiniGame 行为 |
| `Scene Controller` | 拖 `NiumaSceneController` | 不建议 | 无法切到 MiniGame 场景 |
| `Default MiniGame Scene Name` | 填 MiniGame 开始场景名 | 不可以 | 对话 Action 未指定场景时无法进入小游戏 |
| `Default Return Spawn Point Id` | 填 NPC 附近返回点 | 建议填写 | 返回 RPG 时可能落到默认点 |
| `Push Return Context` | RPG 进入 MiniGame 必须开启 | 不建议关闭 | 退出 MiniGame 会 ReturnContextMissing |


