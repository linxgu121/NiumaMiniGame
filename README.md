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
- `MiniGameUIRoot/StartScreen`：挂 `MiniGameStartScreenUI`，新版流程只绑定一套页面：`HomePanel` 入口页、`NamingPanel` 取名页、`PreparePanel` 预备页、`RoomInputPanel` 房间号输入页、`RoomPanel` 房间大厅页。只保留新版分页字段，策划按当前 README 重新绑定即可。
- `MiniGameUIRoot/StartScreen` 场景跳转配置：绑定 `SceneController` 为全局 `NiumaSceneController`；`Fallback Return Scene Name` 填外部 RPG 场景名，例如 `TestSence1`。正式从 RPG 入口进入时会走 ReturnContext；直接打开 MiniGame 场景测试时会用该兜底场景。
- `MiniGameUIRoot/StartScreen/HomePanel`：放“开始游戏”和“退出游戏”。开始游戏按钮只绑定 `EnterGameButton`，退出游戏按钮只绑定 `ExitGameButton`。
- `MiniGameUIRoot/StartScreen/NamingPanel`：放昵称输入框、确认按钮和返回按钮。确认按钮绑定 `ConfirmNameButton`，返回按钮绑定 `NamingBackButton`。
- `MiniGameUIRoot/StartScreen/PreparePanel`：放创建房间、加入房间、观战加入和返回按钮。创建按钮绑定 `CreateRoomButton`，加入按钮绑定 `JoinRoomButton`，观战按钮绑定 `JoinAsViewerButton`，返回按钮绑定 `PrepareBackButton`。
- `MiniGameUIRoot/StartScreen/RoomInputPanel`：放房间号输入框、进入按钮和返回按钮。进入按钮绑定 `RoomInputEnterButton`，返回按钮绑定 `RoomInputBackButton`。
- `MiniGameUIRoot/StartScreen/RoomPanel`：放房间号、当前玩家数量、玩家昵称列表、观战者昵称列表、聊天框、模式切换、身份切换、房主开始游戏、普通玩家准备、房间返回和退出游戏。房间返回按钮只绑定 `RoomBackButton`，只离开房间回预备页；退出游戏按钮继续绑定 `ExitGameButton`，返回 RPG。
- 可选调试按钮：`ConnectButton` 只在需要单独测试连接状态时绑定。正常创建/加入房间会自动连接，不需要给正式 UI 摆连接按钮。
- 房间大厅名单显示：使用 `PlayersDisplay` 显示玩家昵称、本机标记、房主、准备/未准备、状态、离线；使用 `ViewersDisplay` 显示观战者昵称、本机标记、观战者、状态、离线。若 UI 只有一个名单区域，绑定 `NicknameListDisplay` 合并显示玩家和观战者。
- `MiniGameUIRoot/Bridge`：挂 `MiniGameUIViewBridge`，绑定 NiumaMiniGameController 和 StartScreen/GameScreen Receiver。
- 游戏中场景 `MiniGameUIRoot/GameplayScreen`：挂 `MiniGameGameplayScreenUI`，模块根节点负责开放/显示/隐藏，显示内容优先使用整组 Display 绑定。
- `MiniGameUIRoot/DrawingCanvas`：挂 `MiniGameDrawingCanvas`，绑定画布 RawImage/Texture、笔刷工具和输入事件。
- 礼物按钮物体挂 `MiniGameGiftDragButton`，拖拽目标限制在画布或答案模块。
- 游戏结束返回房间由 UI/Controller 处理；退出整个小游戏时通过 `MiniGameGameplaySceneReturnBridge` 或 NiumaScene.ReturnToPreviousScene 回 RPG。
- 开发测试场景可挂 `MiniGameMockTestRunner` 和 `MiniGameUIDebugReceiver`。

### MiniGameStartScreenUI 摆放速查
建议 UI 层级按下面搭，名字不要求完全一致，但页面职责建议保持一致：

```text
Canvas
└── MiniGameUIRoot
    └── StartScreen（挂 MiniGameStartScreenUI）
        ├── HomePanel（入口页）
        │   ├── StartButton -> EnterGameButton
        │   └── ExitButton -> ExitGameButton
        ├── NamingPanel（取名页）
        │   ├── NameInput -> DisplayNameInput
        │   ├── ConfirmButton -> ConfirmNameButton
        │   └── BackButton -> NamingBackButton
        ├── PreparePanel（预备页）
        │   ├── CreateRoomButton -> CreateRoomButton
        │   ├── JoinRoomButton -> JoinRoomButton
        │   ├── WatchRoomButton -> JoinAsViewerButton
        │   └── BackButton -> PrepareBackButton
        ├── RoomInputPanel（房间号输入页）
        │   ├── RoomIdInput -> RoomIdInput
        │   ├── EnterButton -> RoomInputEnterButton
        │   └── BackButton -> RoomInputBackButton
        ├── RoomPanel（房间大厅页）
        │   ├── RoomIdDisplayGroup -> RoomIdDisplay.Root / Texts
        │   ├── PlayerCountDisplayGroup -> PlayerCountDisplay.Root / Texts
        │   ├── ModeDisplayGroup -> ModeDisplayGroup.Root / Texts / Images
        │   ├── PlayersDisplayGroup -> PlayersDisplay.Root / Texts
        │   ├── ViewersDisplayGroup -> ViewersDisplay.Root / Texts
        │   ├── ChatMessagesDisplayGroup -> ChatMessagesDisplay.Root / Texts
        │   ├── ChatInput -> ChatInput
        │   ├── SendChatButton -> SendChatButton
        │   ├── HostControls -> HostRoomControls
        │   ├── GuestControls -> GuestRoomControls
        │   ├── ViewerControls -> ViewerRoomControls
        │   ├── ModeSelectButton -> ModeSelectButton（房主切游戏模式）
        │   ├── SwitchRoleButton -> SwitchRoleButton（玩家/观战身份互切）
        │   ├── RoomBackButton -> RoomBackButton
        │   └── ExitButton -> ExitGameButton
        ├── HintDisplayGroup -> HintDisplay.Root / Texts（顶层提示，可放页面底部）
        ├── ErrorDisplayGroup -> ErrorDisplay.Root / Texts（顶层错误提示）
        └── ToastDisplayGroup -> ToastDisplay.Root / Texts（顶层短提示）
```

`HomePanel / NamingPanel / PreparePanel / RoomInputPanel / RoomPanel` 是页面根节点，脚本会自动控制显示隐藏。按钮和文本字段不是用来填写文字内容，而是把场景里已经摆好的 UI 组件拖到脚本上，让运行时自动刷新。

模式图片不要只在某个 Image 上放一张固定图。正确做法是：把模式显示整组拖到 `ModeDisplayGroup`，其中 `Images` 数组拖需要跟随模式切换的展示图组件；每个 `ModeOptions` 元素的 `DisplaySprite` 分别配置该模式自己的展示图。

### MiniGameStartScreenUI 场景初始显隐设置
这里说的是 **UI 场景制作完成并保存时**，各个 UI 物体在 Hierarchy 里应该是激活还是隐藏。运行时切页由 `MiniGameStartScreenUI` 负责，策划不要在按钮事件里手动 `SetActive` 这些页面根节点。

场景保存时建议状态：

| UI 物体 | 保存场景时状态 | 原因 |
| --- | --- | --- |
| `Canvas` | 激活 | Unity UI 必须可用。 |
| `EventSystem` | 激活 | Button / InputField 需要它接收点击。 |
| `MiniGameUIRoot` | 激活 | 整个开始 UI 的总根节点。 |
| `StartScreen` | 激活 | 挂 `MiniGameStartScreenUI` 的物体必须激活，脚本才会运行。 |
| `StartRoot` | 激活 | 开始界面的总内容根节点；脚本会在需要时保持它显示。 |
| `HomePanel` | 激活 | 玩家进入 MiniGame 开始场景后第一眼看到入口页。 |
| `NamingPanel` | 隐藏 | 点击“开始游戏”后由脚本显示。 |
| `PreparePanel` | 隐藏 | 取名确认后由脚本显示。 |
| `RoomInputPanel` | 隐藏 | 点击“加入房间”或“观战加入”后由脚本显示。 |
| `RoomPanel` | 隐藏 | 创建/加入/观战成功并收到房间快照后由脚本显示。 |
| `HostRoomControls` | 隐藏 | 进入房间后，如果本机是房主，脚本显示。 |
| `GuestRoomControls` | 隐藏 | 进入房间后，如果本机是普通玩家，脚本显示。 |
| `ViewerRoomControls` | 隐藏 | 进入房间后，如果本机是观战者，脚本显示。 |
| `HintDisplay / ErrorDisplay / ToastDisplay` 的 Root | 建议激活或按设计隐藏 | 内容可为空，脚本会写入提示；如果做成整块弹窗，可开启 `Hide Root When Empty` 自动隐藏。 |
| `ModeDisplayGroup` 的 Root | 跟随 `RoomPanel` | 它放在房间页里，房间页隐藏时它自然隐藏。 |

制作规则：

- 页面级根节点只用这五个：`HomePanel / NamingPanel / PreparePanel / RoomInputPanel / RoomPanel`。
- 某个按钮、图片、输入框属于哪个页面，就放到对应 Panel 下面，不要散放在 `Canvas` 根下。
- 如果一个 UI 物体一开始应该看不见，但后续随页面出现，把它放到对应 Panel 下面，并让这个 Panel 控制它的显示。
- 不要把需要运行时显示的页面根节点删掉或不绑定；隐藏可以，未绑定则脚本不知道它存在。
- `RoomPanel` 里的房主按钮、普通玩家按钮、观战按钮建议分别放进 `HostRoomControls / GuestRoomControls / ViewerRoomControls` 三个根节点，由脚本根据身份显示。
- `退出游戏按钮` 可以放在多个页面，但都绑定同一个 `ExitGameButton` 字段；它的职责是退出 MiniGame 回 RPG。
- `返回按钮` 不退出 MiniGame，只返回上一个 UI 页面；例如 `RoomBackButton` 是离开房间回 `PreparePanel`。

运行时显隐流程：

| 运行时动作 | 脚本显示 | 脚本隐藏 |
| --- | --- | --- |
| 进入 MiniGame 开始场景 | `HomePanel` | `NamingPanel`、`PreparePanel`、`RoomInputPanel`、`RoomPanel` |
| 点击入口页“开始游戏” | `NamingPanel` | `HomePanel`、`PreparePanel`、`RoomInputPanel`、`RoomPanel` |
| 取名页点击“确认” | `PreparePanel` | `HomePanel`、`NamingPanel`、`RoomInputPanel`、`RoomPanel` |
| 取名页点击“返回” | `HomePanel` | `NamingPanel`、`PreparePanel`、`RoomInputPanel`、`RoomPanel` |
| 预备页点击“加入房间” | `RoomInputPanel` | `HomePanel`、`NamingPanel`、`PreparePanel`、`RoomPanel` |
| 预备页点击“观战加入” | `RoomInputPanel` | `HomePanel`、`NamingPanel`、`PreparePanel`、`RoomPanel` |
| 房间号输入页点击“返回” | `PreparePanel` | `HomePanel`、`NamingPanel`、`RoomInputPanel`、`RoomPanel` |
| 创建房间成功 / 加入房间成功 / 观战加入成功 | `RoomPanel` | `HomePanel`、`NamingPanel`、`PreparePanel`、`RoomInputPanel` |
| 房间页点击“返回” | `PreparePanel` | `HomePanel`、`NamingPanel`、`RoomInputPanel`、`RoomPanel` |
| 任意页面点击“退出游戏” | 由 `NiumaScene` 切回 RPG | MiniGame UI 随场景卸载 |

如果运行时出现“点击创建房间后全部 UI 隐藏”的情况，优先检查：

1. `RoomPanel` 是否绑定到了 `MiniGameStartScreenUI.RoomPanel`。
2. `RoomPanel` 是否只是保存场景时隐藏，而不是被删除或挂在未激活的父物体外层。
3. `MiniGameUIViewBridge` 的 Receiver 是否绑定到 `MiniGameStartScreenUI`。
4. 后端或 Mock 是否发送了房间快照；未收到快照时脚本无法进入房间页。

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
| `Home / Naming / Prepare / RoomInput / Room Panel` | 拖对应 UI 根节点 | 必填 | 留空则脚本不会控制该页显隐，正式 UI 不建议留空 |
| `Exit Game Button` | 拖退出 MiniGame 回 RPG 的按钮 | 可以 | 需要手动绑定事件 |
| `EnterGameButton` | 拖入口页“开始游戏”按钮 | 不建议 | 玩家无法从入口页进入取名/预备页 |
| `ConfirmNameButton / NamingBackButton` | 拖取名页确认和返回按钮 | 取名页存在时不建议留空 | 取名页无法确认或返回 |
| `CreateRoomButton / JoinRoomButton / JoinAsViewerButton / PrepareBackButton` | 拖预备页创建、加入、观战、返回按钮 | 预备页存在时不建议留空 | 对应功能无法点击 |
| `RoomInputEnterButton / RoomInputBackButton` | 拖房间号输入页进入和返回按钮 | 房间输入页存在时不建议留空 | 无法确认房间号或返回 |
| `RoomBackButton` | 拖房间大厅“返回预备页”按钮 | 可以 | 不绑定则玩家不能从房间页返回预备页 |
| `ModeSelectButton` | 拖房间大厅的“切换模式”按钮，建议只放在房主控件区域 | 可以 | 不绑定则房主不能在大厅切换模式 |
| `SwitchRoleButton` | 拖房间大厅的“切换玩家/观战身份”按钮，建议放在大厅公共操作区 | 可以 | 不绑定则玩家加入后不能从 UI 切换身份 |
| `ConnectButton` | 可选调试连接按钮 | 可以 | 正式流程不需要，创建/加入会自动连接 |
| `ConnectionDisplay / RoomSummaryDisplay / HintDisplay / ErrorDisplay` | 有背景板/图标/文字组合时拖整组 Root，并把组内一个或多个 TMP_Text 加到 Texts 数组 | 推荐 | 整组显示/隐藏，避免只改文字导致背景板残留 |
| `PlayersDisplay / ViewersDisplay / NicknameListDisplay` | 拖名单整组 Root，并把组内一个或多个 TMP_Text 加到 Texts 数组 | 至少按 UI 方案绑定一种 | 分开显示或合并显示名单，背景板/标题/图标会跟着内容显隐 |
| `RoomIdDisplay / PlayerCountDisplay / ModeDisplayGroup` | 拖房间号、人数、模式显示整组 Root，并把组内文字加到 Texts、需要替换 Sprite 的图片加到 Images | 推荐 | 留空则该块 UI 不自动刷新 |
| `ChatMessagesDisplay / ToastDisplay` | 拖聊天记录和短提示整组 Root，并把组内一个或多个 TMP_Text 加到 Texts 数组 | 推荐 | 留空则聊天或短提示不显示 |
| `ModeDisplayGroup.Images` | 拖模式显示组里需要跟随模式切换的 Image，可拖多个 | 可以 | 留空则只显示模式文字，不显示模式图 |
| `ModeOptions` | 每个元素配置一个模式：`ModeId`、`DisplayName`、`DisplaySprite`、人数规则 | 不建议 | 留空时只能使用 `DefaultModeId`，模式按钮没有可切换的图文配置 |
| `Gameplay Scene Name` | 填游戏中场景名 | 分场景玩法不可以 | 房间进入 Playing 后不会自动切玩法场景 |
| `Fallback Return Scene Name` | 测试时填 RPG 场景名 | 可以 | 没有 ReturnContext 时无法退出回 RPG |

`ModeDisplayGroup.Images` 是 UI 上的模式图片显示容器，不是固定模式图。每个模式自己的展示图片填在 `ModeOptions` 的 `DisplaySprite` 中，例如 `draw_telephone` 配你画我猜图片，后续新增模式再给对应元素配另一张图片。

### MiniGameStartScreenUI 整组显示绑定
如果某个显示区域不是单独一行文字，而是“背景板 + 图标 + 文字”的组合，不要只绑定 `TMP_Text`。只绑定文字时，脚本只能改文字内容，背景板和图标不会跟着显示/隐藏，容易出现背景残留或层级错乱。

推荐绑定方式：

1. 给这个显示区域建一个整体根节点，例如 `RoomIdDisplayGroup`。
2. 把背景图、图标、文字都放到这个根节点下面。
3. 在 `MiniGameStartScreenUI` 的“整组显示绑定 - 可选”里找到对应字段，例如 `Room Id Display`。
4. `Root` 拖 `RoomIdDisplayGroup`。
5. `Texts` 数组里拖这个组里的一个或多个 TMP_Text，例如主文字、阴影文字、描边副本文字。
6. 如果这个组有需要由脚本替换 Sprite 的图片，`Images` 数组里拖对应 Image；纯背景图只要放在 Root 下即可，不必拖进 Images。
7. `Hide Root When Empty` 开启时，内容为空会隐藏整个组；关闭时只改文字，不隐藏整组。

常用对应关系：

| UI 区域 | 推荐绑定字段 | 用途 |
| --- | --- | --- |
| 连接状态整组 | `ConnectionDisplay` | 显示已连接/未连接和玩家 ID。 |
| 房间摘要整组 | `RoomSummaryDisplay` | 显示房间号、模式、房主、本机身份。 |
| 玩家名单整组 | `PlayersDisplay` | 显示玩家昵称、房主、准备状态、离线状态。 |
| 观战者名单整组 | `ViewersDisplay` | 显示观战者昵称和观战状态。 |
| 模式显示整组 | `ModeDisplayGroup` | 同时控制模式名称和模式图片。 |
| 房间号整组 | `RoomIdDisplay` | 显示系统分配或输入加入的房间号。 |
| 人数整组 | `PlayerCountDisplay` | 显示当前玩家人数，不包含观战者。 |
| 聊天记录整组 | `ChatMessagesDisplay` | 显示房间聊天记录。 |
| 错误提示整组 | `ErrorDisplay` | 显示服务器错误，3 秒后自动隐藏。 |
| 短提示整组 | `ToastDisplay` | 显示人数不足、玩家未准备等短提示。 |

单独文本字段已取消。现在统一绑定 Display 组，方便背景板、图标、文字一起显示和隐藏。

数量规则：

- `Root` 只有一个，代表这块 UI 的总根节点。
- `Texts` 可以配置多个，所有文本都会写入同一内容，适合文字阴影、描边副本、多个位置同步显示。
- `Images` 可以配置多个，所有图片都会写入同一 Sprite，适合模式图多层表现或图标副本。
- 静态背景板、装饰线、固定图标只要放在 `Root` 下面即可，不需要拖进 `Images`。

`ModeSelectButton` 和 `SwitchRoleButton` 是两个不同功能：`ModeSelectButton` 切换小游戏模式，只允许房主在 Lobby 大厅使用；`SwitchRoleButton` 切换自己是玩家还是观战者，玩家点后变观战者，观战者点后变玩家，也只在 Lobby 大厅可用。游戏开始后身份不允许切换，避免破坏当前回合链路。

### MiniGameGameplayScreenUI 整组显示绑定
游戏中 UI 和开始 UI 使用同一套整组绑定规则。凡是“背景板 + 图标 + 文字”的显示区域，都必须使用 Display 绑定。

推荐游戏中层级：

```text
Canvas
└── MiniGameUIRoot
    └── GameplayScreen（挂 MiniGameGameplayScreenUI）
        ├── DrawingBoardRoot
        │   ├── BrushToolsRoot
        │   ├── ColorPaletteRoot
        │   ├── CanvasRoot
        │   ├── DrawerNameDisplayGroup -> DrawerNameDisplay.Root / Texts
        │   └── FinishDrawingButton -> FinishDrawingButton
        ├── ChatRoot
        │   ├── ChatLogDisplayGroup -> ChatLogDisplay.Root / Texts
        │   ├── ChatInput -> ChatInput
        │   └── SendChatButton -> SendChatButton
        ├── AnswerRoot
        │   ├── AnswerInput -> AnswerInput
        │   └── SubmitAnswerButton -> SubmitAnswerButton
        ├── TopicRoot
        │   ├── TopicDisplayGroup -> TopicDisplay.Root / Texts / Images
        │   ├── AnswerDisplayGroup -> AnswerDisplay.Root / Texts
        │   └── AnswererDisplayGroup -> AnswererDisplay.Root / Texts
        ├── TimerRoot
        │   └── TimerDisplayGroup -> TimerDisplay.Root / Texts
        ├── PlayerListRoot
        │   └── PlayerListDisplayGroup -> PlayerListDisplay.Root / Texts
        ├── DrawPromptRoot
        │   └── DrawPromptDisplayGroup -> DrawPromptDisplay.Root / Texts / Images
        ├── AnswerPromptRoot
        │   └── AnswerPromptDisplayGroup -> AnswerPromptDisplay.Root / Texts / Images
        ├── EvaluationRoot
        │   ├── AgreeButton -> AgreeButton
        │   └── DisagreeButton -> DisagreeButton
        ├── EvaluationListRoot
        │   └── EvaluationListDisplayGroup -> EvaluationListDisplay.Root / Texts
        └── ToastDisplayGroup -> ToastDisplay.Root / Texts / Images
```

绑定规则：

- `DrawingBoardRoot / ChatRoot / AnswerRoot / TopicRoot / TimerRoot / PlayerListRoot / EvaluationRoot` 是模块根节点，脚本按玩家状态控制开放、显示或隐藏。
- `XXXDisplay.Root` 是该显示区域自己的整组根节点，适合放背景板、图标、文字、装饰。
- `XXXDisplay.Texts` 可以拖多个 TMP_Text，例如主文字、阴影文字、描边副本。
- `XXXDisplay.Images` 可以拖多个需要由脚本替换 Sprite 的 Image；固定背景图只放在 Root 下，不用拖进 Images。
- 如果某个模块有整块背景，例如题目板、倒计时牌、提示弹窗，务必拖 `Root`，不要只拖文字。

### MiniGameDialogueActionHandler
建议挂载位置：RPG 中 MiniGame NPC 所在场景，或 Gal 行为桥接根物体。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Dialogue Controller` | 拖 `NiumaDialogueController` | 不建议 | 无法注册 OpenMiniGame 行为 |
| `Scene Controller` | 拖 `NiumaSceneController` | 不建议 | 无法切到 MiniGame 场景 |
| `Default MiniGame Scene Name` | 填 MiniGame 开始场景名 | 不可以 | 对话 Action 未指定场景时无法进入小游戏 |
| `Default Return Spawn Point Id` | 填 NPC 附近返回点 | 建议填写 | 返回 RPG 时可能落到默认点 |
| `Push Return Context` | RPG 进入 MiniGame 必须开启 | 不建议关闭 | 退出 MiniGame 会 ReturnContextMissing |







