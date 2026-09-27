# 阶段 D 使用与讲解

2026-09-27。本轮接入用户提供的原生骨骼人物及配套动作，并将固定界面换成购买资源中的独立 PNG。制作结果保存在 Unity 场景、Prefab、材质、Animator Controller 和配置资产中。本页解释结构和接手方法；具体运行、构建与回归结论以[开发进度](开发进度.md)及对应 Evidence 为准，不以文档代替实际验收。

**学习状态：以下两组均待本人操作、解释与验收。** 助手完成导入、实现、测试或讲解，不代表用户已经掌握。可以先完成作品，再集中完成这些练习。

## 1. 原生人物：从源文件到结果驱动的动作

详细角色来源、实际导入的片段和骨骼见[阶段 D 人物资源接入](阶段D人物资源接入.md)。正式精选目录为 `Assets/_Game/Art/Selected/StageD/Characters`：玩家 PlayerSwordsman、剑师 SwordMaster、竹林对手 Bandit、工作台木人 TrainingDummy，以及 Physician、Innkeeper、Swordswoman 三名对话人物。四套完整角色使用自己的 Stand、Walk、Run、Attack、Magic、Hit、Die、Relax；对话人物只有 Stand。工作台保留的原生守卫候选只有行走，不能与完整动作组混淆。

本轮没有重新为无骨骼模型自动生成骨架。所选 FBX 自带骨骼、蒙皮权重和原配片段，采用 Generic 和自身 Avatar。部分源文件是 FBX 6.1，动画使用 Take/Channel/Key；只搜索新版 AnimationCurve 字样会漏检，最终须以 Unity 真正导入的 AnimationClip、Avatar 和 SkinnedMeshRenderer 为准。

制作调用链：

`明确名单与来源清单 → 精选 FBX/漫反射图 → StageDCharacterSetup.ImportSelected → ModelImporter/TextureImporter → InspectSelected → BuildPrefabs → 保存材质/Controller/动作配置/角色 Prefab → ReplaceSceneActors/ReplaceExplorerPrefab → 场景中的固定引用`。

代码入口在 `Editor/StageDCharacterSetup.cs`；初始精选工具在 `Tools/SelectStageDCharacters.ps1`。源压缩包与旧 Prefab 保留，后续美术调整应直接编辑精选配置，不依赖每次运行游戏重新生成角色。制作工具保留已有 Controller、配置和材质的 Inspector 调整。

| 在 Unity 中打开 | 负责的内容 | 不负责的内容 |
| --- | --- | --- |
| `Characters/PlayerSwordsman/PlayerSwordsman.prefab` | 原生模型、蒙皮、Animator、CharacterMotion、材质引用 | 探索输入、战斗伤害 |
| 同目录 `PlayerSwordsman.controller` | Locomotion 混合树和动作状态，原配片段与状态播放速度 | 判断招式是否合法 |
| 同目录 `PlayerSwordsman_Motion.asset` | 动作 ID 到状态名、过渡、持续时间及保持到重置 | 扣血、扣内力和发奖励 |
| `Scenes/AnimationWorkshop.unity` | 保存的角色实例、预览按钮和动作选项 | 正式战斗结果判定 |
| `Scenes/Valley.unity`、两座 Arena 场景 | 探索视觉引用、战斗演员容器及场景表现 | 购买源资源的批量导入 |

运行时有两条需要讲清的链路。

**探索移动：** 玩家输入进入 `ValleyExplorer`，由 CharacterController 执行位移；代码用这一帧实际产生的水平位移除以时间得到速度，再调用 `CharacterMotion.SetSpeed`。该组件把速度传给 Animator 的 `Speed`，Locomotion 混合树选择 Stand/Walk/Run。被障碍挡住时实际位移变小，动画也随之降低速度。模型不通过根运动反过来决定本项目的移动距离。

**战斗动作：** `BattlePresenter.Confirm/Resolve` 先检查行动条件；离线交给 `BattleReducer.Apply`，在线提交给 `NetSession` 并等待权威状态。已接受的新旧状态进入 `BattleFeedback.Transition`，区分攻击与支援后发出 Attack/Cast；生命或护盾变化驱动 Hit，结算驱动 Victory/Defeat。`CharacterMotion.TryPlayAction` 按本角色的 `CharacterMotionProfile` 找到 Animator 状态，检查状态与时长后执行过渡。普通动作到时回 Locomotion；终结姿态保持到 `ResetPose`。

因此，输入“播放 Attack”只产生动画；真正的伤害已经由规则结果决定。网络恢复若跨过多次 revision，界面不会伪造自己没收到的逐招动画。动作丢失时组件返回失败或回到移动状态，也不能重新结算一次伤害来“补动画”。

当前映射为 Attack→原配 Attack、Cast→Magic、Hit→Hit、Defeat→Die、Victory→Relax。**Relax 是收势替代，不是新制作的专用胜利动作。** 武器与身体动作仍是来源资源的表现，不能把“已接入”解释为所有镜头和每招动作都已达到专业精修。

本轮实际遇到的制作问题也可用于理解边界：采样 Die 后，只采样 Stand 无法还原 Stand 没有覆盖的碎裂骨骼，木人会残留散块；剔除边界还须使用正确的模型和骨骼空间，不能仅把数值调大。修复过程保留源姿态，逐次采样前恢复，再计算并保存边界；完整问题与证据见人物接入文档。不要用关闭网格来掩盖骨骼状态错误。

**小修改练习（待本人完成）：** 在 Unity 中复制玩家 Controller，给工作台一个玩家实例绑定副本；把 Locomotion 中 Walk 阈值由 3.5 改为 2.5。在同样预览速度下比较混合比例，再到探索场景确认没有修改移动速度。保存副本和观察记录，最后恢复实例的原 Controller。验收时需解释“阈值改变的是动画选择，不是 CharacterController 的位移”。

**调试问题（待本人回答）：** 同一人物有 8 个有效片段，按攻击按钮后伤害正确但模型不动，你如何依次区分未收到表现调用、动作 ID/状态名不匹配、Animator 未激活、Avatar/骨骼路径不匹配及剔除边界问题？如果攻击动画播放两次，为什么不能据此推断伤害也结算了两次？

## 2. 购买素材 UI：从资源到明确绑定的外观

精选目录为 `Assets/_Game/Art/Selected/StageD/UI`，来源清单在 `Docs/Evidence/StageD-ui-source-manifest.json`。12 张独立 PNG 原样来自 `Assets/_Game/资源/C1869/UI/UI切图`，没有生成新的位图替代资源，也没有使用原包作品 logo。Sprite 导入器负责 Clamp、关闭 MipMap/Read-Write、九宫格和合适尺寸；原文件与精选副本的 SHA256 可对照。

| 资产或组件 | 输入 | 保存或输出 |
| --- | --- | --- |
| `Content/UI/GuiyunUISkin.asset` / `UISkinProfile` | 纸面、暗纹、标题、按钮、边框、图标及色板 | 一套可替换的外观配置 |
| 各 UI 根上的 `UISkinBinding` | 配置及显式 Image/Text 目标、角色 | 对已有目标应用 Sprite、九宫格和文字颜色 |
| `Editor/StageDUiSetup.cs` | 已保存的布局、按钮和配置 | 保存 Prefab 与场景覆盖，核对持久回调，重开检查 |
| `ValidateApplied()` | 当前已绑定目标和配置 | 未应用 Sprite、错误边框中心或按钮文字色等错误列表 |

调用链为：

`原 PNG → 精选 Sprite → UISkinProfile → StageDUiSetup 识别首次迁移的视觉角色 → UISkinBinding 保存明确目标 → Apply → 保存 Prefab/场景 → 重新打开并 ValidateApplied → 正常运行的 Presenter 更新业务内容`。

`UISkinBinding` 没有在 Awake/Update 中重建面板或每帧刷色。界面启动时使用保存的对象与引用；业务仍控制自己的动态内容，例如 `JourneyPresenter` 的秘籍拥有/选中颜色、`QuestRowView` 的任务分类/状态色、练功路线高亮和按钮是否可用。血条、地图标记、穴位、画线区域与模态遮罩不通过“全局给所有 Image 换图”的方式处理。战斗秘籍图标更新的是 `BattlePresenter.techniqueIcons` 来源数组，避免运行时刷新又盖回旧图。

换肤操作入口：退出 Play 并保存当前场景，在 Project 选择 `Content/UI/GuiyunUISkin.asset` 修改 Sprite 或色板，再运行 **一笔江湖 / 3.0 / 阶段D / 应用购买资源界面外观**。只想预览某个已经绑定的 UI，可在根对象 `UISkinBinding` Inspector 点击“应用外观”，然后保存该 Prefab 或场景。全工程应用负责同步战斗图标及业务组件的配色配置；单个绑定的按钮只作用于它列出的 Image/Text。

固定布局仍可在 MainMenu、Valley、GestureLab、两座 Arena、Lobby 与 AnimationWorkshop 场景中编辑。任务条目由保存的 `Prefabs/UI/QuestRow.prefab` 提供；运行时按任务数量生成条目是数据展示，其他固定面板不在运行时重建。换肤工具不会重新连接整套战斗 HUD 到 Prefab，从而保留场景演员与特效的外部引用。

本轮有三个具体故障值得会讲：

1. **新边框变白矩形。** 边框对象已经保存，但 Sprite 仍为空，不能只凭制作函数返回成功判定换肤完成。现在每个根重新解析配置，Apply 后检查，再保存并重开场景检查，避免跨场景制作中配置失效而静默跳过。
2. **边框盖住按钮颜色或画线内容。** 源边框中央是 alpha 255 的黑色；九宫格边框必须 `fillCenter=false`，只画四周。装饰图还须 `raycastTarget=false`，并在布局组中忽略布局，避免改变点击和排列。
3. **默认关闭的手札按钮文字太暗。** `GetComponentInParent<Button>()` 默认不含 inactive 祖先，首次迁移没有识别关闭弹窗里的按钮，把其文字误标为纸面 Ink。修复使用 `includeInactive=true`，按实际 Button 引用迁移为 ButtonLabel/PrimaryButtonLabel；两类资源按钮变暗时仍使用专用亮字。纸面正文继续深墨，未把整个界面改成白字。

**小修改练习（待本人完成）：** 在 Unity 中记录当前 `GuiyunUISkin.asset` 的普通按钮色和边框色，做一次幅度较小的色板调整并应用。打开主菜单、任务手札和行囊，分别观察正常、选中及禁用按钮；确认纸面正文仍清晰，点击任务按钮仍只触发一次，重新打开场景外观仍在。保存前后截图和实际结果，然后恢复原色。练习不要求改任何导航或任务代码。

**调试问题（待本人回答）：** 主菜单按钮正确，默认关闭的手札面板第一次打开却出现深底深字，你会检查哪些保存的 Text 角色与父对象状态？为什么只在当前 Play 中改颜色不能修复下次运行，为什么对所有 Text 设成白色也不合适？

## 学习记录

| 模块 | 修改练习 | 调试回答 | 验收状态 |
| --- | --- | --- | --- |
| 原生人物 / Animator / 结果驱动动作 | 待本人实际修改、观察并提交记录 | 待本人解释 | 待验收 |
| 资源 UI / 配置与明确绑定 / inactive 修复 | 待本人实际修改、观察并提交记录 | 待本人解释 | 待验收 |

面试讲解应区分“我能打开并修改哪项配置”“规则与表现为什么分离”“遇到什么实际问题、如何定位”和“还有哪些表现限制”。能够复述文档只算准备；独立完成修改并解释结果后，再由本人和实际证据更新[学习验收](学习验收.md)。
