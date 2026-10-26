# Unity-2048 TikTok 小游戏代码审计报告

**审计日期：** 2026-08-08  
**项目：** Unity-2048（TikTok 小游戏平台）  
**审计范围：** `Assets/Scripts/` 全部 13 个脚本 + `Assets/Editor/` 3 个脚本 + 配置文件 + 隐私/条款页  
**SDK 插件：** `Assets/Plugins/com.tiktok.minigame/`（第三方，未深入审计）

---

## 一、严重（Critical）— 影响核心功能

### C1. 滑动移动检测失效 — 游戏近乎不可玩
**文件：** `Assets/Scripts/Core/Game2048.cs` 第 207–213 行  
**问题：** `MoveTiles()` 方法开头执行 `t.PreviousCol = t.Col; t.PreviousRow = t.Row;`，使得 `PreviousCol == Col`。随后在写回网格时检测移动：

```csharp
if (tile.Col != tile.PreviousCol) moved = true;  // 永远 false！
tile.Col = i;
```

`tile.Col` 此时仍是旧值，与 `PreviousCol` 相同，所以条件永远为 `false`。`moved` 标志**只有合并时**（第 190 行 `moved = true;`）才会被置为 `true`。

**后果：** 玩家滑动后，如果方块只是滑动而未发生合并，`Move()` 返回 `false`——不生成新方块、不增加步数、不触发状态更新。只有包含合并的移动才有效。这让游戏基本不可玩。

**修复：**
```csharp
if (i != tile.PreviousCol) moved = true;  // 比较新位置 i 与旧位置
tile.Col = i;
```
垂直方向同理：`if (i != tile.PreviousRow) moved = true;`

### C2. TikTok SDK 从未初始化
**文件：** 全项目  
**问题：** `TikTokBridge.Initialize()` 在整个代码库中**从未被调用**（已全局搜索确认）。因此 `_isInitialized` 永远为 `false`，所有 TikTok 平台功能（广告、登录、分享、添加桌面快捷方式、入口任务）全部走 fallback 路径，等同于禁用。

**后果：**
- `ShareScore()` — 直接 return，不分享
- `ShowRewardedAd()` — 调用 `onComplete(false)` + `onUnavailable()`，复活广告永远"不可用"
- `ShowInterstitialAd()` — 直接 return
- `SilentLogin()` — 返回 null
- `AddHomeScreenShortcut()` — 返回 false
- `StartEntranceMission()` / `GetEntranceMissionReward()` — 直接 return

**修复：** 在 `GameManager.Start()` 或 `GameBootstrapper.Initialize()` 中添加：
```csharp
TikTokBridge.Initialize(() => {
    Debug.Log("[GameManager] TikTok SDK initialized");
});
```

---

## 二、高（High）— 功能缺陷或配置问题

### H1. 无效移动污染撤销历史
**文件：** `Game2048.cs` 第 95–97 行  
**问题：** `Move()` 在调用 `MoveTiles()` 之前就执行 `SaveState()`。如果移动无效（`moved == false`，由 C1 加剧），一个与当前状态完全相同的快照被推入 `_history`。玩家执行 Undo 时会撤销一个空操作，浪费一次撤销机会。

**修复：** 将 `SaveState()` 移到 `if (moved)` 块内，或在 `moved == false` 时弹出刚保存的快照。

### H2. 复活广告"不可用"时免费复活 — 变现绕过
**文件：** `GameManager.cs` 第 195–205 行  
**问题：** `OnReviveClicked()` 中，`onUnavailable` 回调直接调用 `Game.Revive()`——广告不可用时免费复活。结合 C2（SDK 未初始化），**每次**点 Revive 都走 `onUnavailable` 路径，等于无限免费复活。

```csharp
TikTokBridge.ShowRewardedAd(
    rewarded => { if (!rewarded) return; Game.Revive(); ... },
    () => { Game.Revive(); ... }  // 永远走这条
);
```

**建议：** 确定产品意图。如果希望"广告不可用时仍可复活"，应限制次数或给出明确提示；如果希望强制看广告，`onUnavailable` 应显示提示而非直接复活。

### H3. 广告配置为空 — 生产环境无广告
**文件：** `Assets/Resources/AdConfig.json`  
**问题：** `rewardedAdUnitId` 和 `interstitialAdUnitId` 均为空字符串。即使 SDK 初始化了，广告也不会展示（代码中有 `string.IsNullOrEmpty` 检查并 return）。

### H4. appid 为空 — 无法发布
**文件：** `project.config.json`  
**问题：** `"appid":""` — 没有填写应用 ID，游戏无法在 TikTok 平台发布或被正确识别。

### H5. publishType 与平台矛盾
**文件：** `project.config.json`  
**问题：** `"publishType":"AndroidNativeOnly"` — 但项目是 TikTok 小游戏（WebGL 构建目标），且有 WebGL 相关的音频桥接和 PostBuild 脚本。配置与实际用途不一致。

### H6. starkSdkVersion 显示 "Without StarkSDK"
**文件：** `project.config.json`  
**问题：** `"starkSdkVersion":"Without StarkSDK"` — 如果这意味着 StarkSDK 未集成，则 `#if TTSDK` 宏可能未定义，所有 TikTok 功能编译时就被排除。需要确认 `TTSDK` 定义符号是否已设置（`TTSDKSetup.EnableTTSDKDefine()` 需手动调用）。

---

## 三、中（Medium）— 合规/UX/健壮性

### M1. 隐私政策声明与实际不符
**文件：** `privacy.html`  
**问题：**
- 声称使用 "Unity Analytics (game performance analysis)" — 代码中无任何 Unity Analytics 集成
- 声称 "display leaderboards and achievements" — 游戏无排行榜和成就系统
- 声称收集 "Account Information (username, avatar)" — `SilentLogin` 从未被调用

**风险：** 隐私政策声明了不存在的数据收集行为，可能违反平台审核要求。建议移除不准确的声明，或添加对应功能。

### M2. 键盘 'N' 无确认直接重开
**文件：** `InputHandler.cs` 第 32 行  
**问题：** `Input.GetKeyDown(KeyCode.N)` 直接调用 `StartNewGame()`，无确认对话框。玩家可能误按 N 丢失当前进度。

### M3. 多个 TikTok Bridge 功能已实现但从未调用
**文件：** `TikTokBridge.cs`  
**问题：** 以下方法定义完整但全项目无调用点：
- `ShareScore(int score)` — 分享功能未接入
- `ShowInterstitialAd()` — 插屏广告未接入
- `SilentLogin()` — 静默登录未接入
- `StartEntranceMission()` / `GetEntranceMissionReward()` — 入口任务未接入

**建议：** 要么接入这些功能，要么移除死代码。

### M4. GameManager 可能被创建两次
**文件：** `GameBootstrapper.cs` + `SceneSetup.cs`  
**问题：** `GameBootstrapper`（运行时自动创建）和 `SceneSetup`（编辑器场景中创建）都会创建 GameManager。Singleton 的 `Awake()` 会销毁第二个，功能上没问题，但浪费一次创建/销毁周期。

### M5. BlinkRoutine 协程无终止条件
**文件：** `ScoreDisplay.cs` 第 196 行  
**问题：** `while (true)` 无限循环协程，在对象销毁时会产生 MissingReferenceException（访问已销毁的 `_eyeL`/`_eyeR`）。实际上由于挂在 DontDestroyOnLoad 的 Canvas 上，生命周期内不太会销毁，但不够健壮。

**修复：** 添加 `if (_eyeL == null) yield break;` 或使用 `while (_eyeL != null && _eyeR != null)`。

### M6. TTSDKSetup 需手动调用
**文件：** `TTSDKSetup.cs`  
**问题：** `EnableTTSDKDefine()` 是静态方法，无自动化触发（不像 SceneSetup 有 `[InitializeOnLoad]`）。开发者可能忘记设置 `TTSDK` 宏，导致所有平台功能被编译排除。

---

## 四、低（Low）— 代码质量/可维护性

### L1. UI 代码大量硬编码魔法数字
**文件：** `ScoreDisplay.cs`, `BoardRenderer.cs`  
**问题：** 位置、尺寸、颜色、字体大小等大量以字面量写死（如 `new Vector2(0, -85)`, `fontSize = 48`），难以维护和适配不同屏幕。

### L2. TileColors 仅覆盖到 2048
**文件：** `BoardRenderer.cs` 第 19 行  
**问题：** `TileColors` 字典最大 key 为 2048。超过 2048 的方块统一使用 fallback 颜色（`0.93f, 0.76f, 0.14f`），视觉上无法区分 4096、8192 等。

### L3. ConfettiEffect 使用 GameObject 而非对象池
**文件：** `ConfettiEffect.cs`  
**问题：** 每次播放彩纸效果创建 50–60 个 GameObject，结束时逐个 `Destroy`。频繁触发时（如连续合并 ≥256）可能产生 GC 压力。对小游戏来说影响不大，但可用对象池优化。

### L4. 联系邮箱为个人邮箱
**文件：** `terms.html`  
**问题：** 联系邮箱 `yanbaiyuyu@hotmail.com` 为个人邮箱。如果是正式发布的产品，建议使用企业/团队邮箱。

### L5. SceneSetup 使用 orthographic 相机，GameManager.Start() 创建 perspective 相机
**文件：** `SceneSetup.cs` vs `GameManager.cs`  
**问题：** 编辑器场景创建正交相机，运行时 `GameManager.Start()` 检测到无主相机时创建透视相机（`cam.orthographic = false`）。两者不一致，虽然 2D UI 不依赖相机投影模式，但逻辑上应统一。

---

## 五、总结

| 严重度 | 数量 | 关键项 |
|--------|------|--------|
| **Critical** | 2 | 滑动检测失效、SDK 未初始化 |
| **High** | 6 | 撤销污染、免费复活、空广告配置、空 appid、发布类型矛盾、SDK 宏可能未设 |
| **Medium** | 6 | 隐私政策不符、无确认重开、死代码、重复创建、协程无终止、手动宏设置 |
| **Low** | 5 | 硬编码、颜色表不全、无对象池、个人邮箱、相机模式不一致 |

### 优先修复顺序
1. **C1** — 修复 `MoveTiles` 的 `moved` 检测（一行改动，影响最大）
2. **C2** — 调用 `TikTokBridge.Initialize()`
3. **H1** — 修复 `SaveState` 时机
4. **H4** — 填写 appid
5. **H5/H6** — 修正 publishType 和 SDK 配置
6. **M1** — 修正隐私政策
7. 其余按优先级逐步处理
