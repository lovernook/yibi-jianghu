# 阶段 D：原生骨骼人物与资源 UI 验证

2026-09-27。本轮落实用户的“换掉旧人物、从已有资源换 UI”。工程为 `一笔江湖demo/一笔江湖`，Unity 2022.3.62f3c1；MCP 每次重新握手并检查 Assets 路径，未操作《星海学园》。修改前副本：`Backups/Before-StageD-20260927-152641`。本页保留失败与修复过程，最终测试结果在末尾记录。

## 实际保存的内容

- 从用户角色压缩包定向提取七组 FBX／贴图，保存原始文件哈希与来源。主角、剑师、山贼、木人均使用原生蒙皮和八个配套动作；药师、掌柜、女侠只有原配 Stand。详情见[人物接入](阶段D人物资源接入.md)。
- Unity 编辑器中实际导入 Generic Avatar、制作 URP 材质、Controller、CharacterMotionProfile 和七套 Prefab，替换六场景的十九处正式／工作台角色；主菜单、山谷、两擂台、练功院和工作台均保存。木人当前在工作台，未声称已接入正式练功台。另更新可复用 PlayerExplorer Prefab。
- 从 C1869 的 UI 切图选择十二张 PNG：纸面、暗纹、标题、按钮、边框、任务与六技能图标。七个页面场景、七个已有 Prefab 保存 UISkinBinding 与 `GuiyunUISkin.asset` 引用。运行时不靠重新生成页面实现换皮。
- 保留角色移动／交互容器、战斗特效挂点及按钮持久回调；外观只消费结果。动画不计算伤害；胜利映射 Relax 收势，未制作专用胜利动画。独立动作配置可调整映射和时长，UI 配置可替换图与颜色；新增规则仍须接入规则层。

## 真实发现与修复

1. **资源误判修正**：FBX 6.1 使用 Take／Channel／Key，先前仅搜索 AnimationCurve 会漏检。此次以 Unity 实际 Avatar、骨骼、片段和曲线为准，不再沿用“未搜到等于无动画”的结论。
2. **两次应用缩放**：默认 BakeMesh 后再 TransformPoint，导致人物高度估计错误。Unity 对照探针确认后统一使用 `BakeMesh(mesh, true)` 再转世界坐标；人物约 2.4 米、木人约 2.2 米。证据 `StageD-bake-scale-probe.json`、`StageD-character-repair.json`。
3. **木人碎块姿态污染**：Die 的 310 条曲线多于 Stand 的 120 条，采样 Stand 不能还原所有碎裂骨骼。改为每次采样前、制作结束后恢复源模型完整 TRS；不删除碎块网格或修改骨权重掩盖问题。
4. **保存后白框**：跨场景制作时原有配置对象引用失效，首次保存了绑定却未实际应用图片。每个根重新加载配置，Apply 后校验，并保存、重开场景再次检查。金边只画九宫格边缘，中心不会遮住业务选中颜色。证据 `StageD-ui-repair.json`。
5. **关闭弹窗暗字**：默认 inactive 弹窗内的按钮未被父级查找识别，误用纸面深墨文字。查找明确包含 inactive，并以实际 Button 槽迁移专用亮字角色。修复后实际查看 `StageD-journal-final.png`。证据 `StageD-button-text-repair.json`。
6. **台上人物埋脚及旧 Prefab 遗留**：听松先生父节点已在石台表面 1.2 米，但视觉子节点被错误归到地面；遵守父节点位置并保存。PlayerExplorer 清除六个禁用旧视觉组件，保留 Transform／碰撞，将行走／奔跑统一为 3.5／6。证据 `StageD-placement-and-explorer-repair.json`。
7. **剔除坐标与采样**：Unity 实证 SMR 的 localBounds 跟随 rootBone，不能直接保存 renderer-local 顶点包围盒。修正转换后，稀疏九次采样仍遗漏山贼攻击和木人碎裂极值；提高到至少 60 Hz 制作采样，并用至少 90 Hz 的不同时间网格独立检查。两轮失败分别保留在 `StageD-culling-repair-audit.json` 和 `StageD-culling-final-audit.json`；文件名里的 final 不代表其内容通过，须以最终复查为准。
8. **木人骨架显示根**：加密采样仍显示木人 Spine 根不足以可靠覆盖独立碎块。临时实例把剔除根设为稳定 NativeModel，经八片段四十姿态、104,840 顶点对照，世界顶点最大变化为 0；保留骨权重和 bones。保存前一轮尚未加载新脚本，`StageD-culling-verified.json` 仍失败；强制导入并完成编译后重做，真正最终 `StageD-culling-confirmed.json` 为七角色、5,106 姿态、9,362,932 顶点，全部越界 0。探针原始记录在 `StageD-dummy-stable-root-probe.json`。
9. **练功高亮覆盖皮肤色**：补看练功页发现 SelectRoute 仍把按钮刷成旧浅金／浅蓝，亮字对比不足。将选中／普通色改为序列化字段，默认兼容旧值；界面制作从皮肤的 selectedCard／normalCard 保存配置，不改采样评分或练习流程。证据 `StageD-route-colour-repair.json`。

原样精选文件共 26 个（人物十四个、UI 十二个），最终 SHA256 检查无不一致，见 `StageD-source-hash-check.json`。旧资源保留，未将采购素材上传或发布。

## 画面证据与边界

实际查看过 2560×1440 GameView 的工作台、主菜单、山谷、手札和青石擂台。首轮失败截图保留；`StageD-workshop-repaired.png`、`StageD-valley-final.png`、`StageD-journal-final.png` 是对应问题修复后的画面。自动流程、截图和编辑器检查不等于真人手感、全分辨率、前台性能、两台物理电脑或整章人工通关。

## 最终工程与成品回归

本轮全量 EditMode **132/132 通过，2.493 秒**，证据 `StageD-EditMode.json`。首次全量 PlayMode 共 50 项，47 项通过、3 项失败，证据 `StageD-PlayMode-first.json`；新增阶段 D 六项全部通过。失败来自旧测试固定等待 2.6 秒或 1.1 秒：当前原配攻击表现 1.5 秒，加 NPC 思考 1.2 秒，旧等待早于合法状态变化。修改两份测试中的三处等待为“按配置计算预算、逐帧等待实际状态、最多 10 秒”，保留原胜负、精确回合、重复提交、状态对象和按钮断言；没有加速生产动画绕过测试。完整 PVE 的 65 秒总上限保留。

八个保存的构建场景检查：缺失脚本 0、外观绑定错误 0，原有按钮持久目标与方法逐项对照无差异；证据 `StageD-final-scene-audit.json`、`StageD-final-scene-summary.json`。该检查不等于按钮实际点击，点击链由 PlayMode 与独立客户端流程另验。

全量 PlayMode **50/50 通过，146.101 秒**，证据 `StageD-PlayMode-final.json`，无跳过。包括七套角色的运行尺寸、真实骨骼攻击、死亡保持及复位、旋转实例剔除边界、听松台面位置、资源界面刷新、持久按钮、字体和原有任务／战斗／网络客户端逻辑。最后练功配色修改后，对 GestureLab、Polish 和阶段 D 展示相关测试再次运行，**16/16 通过，37.482 秒**，证据 `StageD-colour-tests.json`；没有把这次专项重跑写成又一次全量。

首个阶段 D Windows 包于 16:13 构建成功（140,618,075 字节，16.226 秒，0 错误／0 警告），记录保留为 `FrameworkStageD-first-build.txt`。实际独立客户端完成战斗全流程（11 次玩家行动、revision=21、玩家获胜、44.483 秒）、五次应用导航、任务领取与存档闭环（6 签令、2 残页、2 项任务），错误日志 0、退出码 0。证据目录为 `BattlePlayer-20260927-161403`、`FrameworkPlayer-20260927-161434`、`QuestPlayer-20260927-161453`。

同一首包和本地便携服务完成五局同机双客户端回归：每方向 0／100／300 ms 延迟、丢回执恢复、双方再战、回房间改构筑。两端逐局最终 JSON 完全相同，revision=22／22／22／32／32，退出网络线程 0。证据 `NetworkJourney-20260927-161552`、`StageD-first-network-comparison.json`。这些结果不计为两台物理电脑、真人联机或前台性能；隐藏客户端自动截图不作为可视验收证据。

## 最终交付包

配色调整后，**2026-09-27 16:23:15（北京时间）**再次实际构建成功，**0 错误／0 警告、140,618,135 字节、5.765 秒**。输出 `Builds/FrameworkStageD/YibiJianghu.exe`，必须保留同目录的数据文件；旧 Demo 和阶段 A/B/C 未覆盖。证据 `FrameworkStageD-build.txt`、`StageD-final-build-call.json`。排队请求不作为构建完成证据；同步调用后先前排队的构建也完成，以上数值以最终成功报告为准。

最终包重新实际运行：

| 项目 | 本轮实际结果 | 证据目录 |
| --- | --- | --- |
| 应用导航 | 5 次成功，请求／完成事件均 5；错误日志 0，退出码 0 | FrameworkPlayer-20260927-162343 |
| 任务流程 | 6 签令、2 残页、2 任务，包含采样、领奖、装备交换与隔离存档重载；错误日志 0，退出码 0 | QuestPlayer-20260927-162348 |
| 完整离线战斗 | 11 次玩家行动、revision=21、玩家获胜与重开，44.188 秒；错误日志 0，退出码 0 | BattlePlayer-20260927-162353 |
| 两个独立客户端 | 五局通过，最终状态逐局一致；revision=22/22/22/32/32，含延迟、丢回执恢复、再战与改构筑，退出网络线程 0 | NetworkJourney-20260927-162423 |

最终联网对比见 `StageD-final-network-comparison.json`。本轮自启客户端与服务均已退出、7777 无监听，见 `StageD-final-server-cleanup.json` 和 `StageD-final-process-check.json`。

最终 UI 实景补看并保存：`StageD-inventory-final.png`、`StageD-lobby-final.png`、`StageD-practice-contrast-verified.png`、`StageD-mainmenu-final.png`。练功图 `StageD-practice-final.png` 是配色修正前，名字不代替画面结论。最终实时工程状态：MainMenu、非 Play、无未保存变更、无编译／导入进行中，选中正式 PlayerSwordsman；Console **0 错误／0 警告**。证据 `StageD-final-live-editor.json`、`StageD-final-console.json`。

本批人物和购买 UI 的接入及上述条件下工程验收完成。深度镜头／舞台、专用胜利动作、六技能完整特效与声音、前台长期性能、多分辨率、真人手绘和整章人工通关继续按 D/E 排期；不称整款作品已达到全部发行标准。两核心模块讲解与练习见[阶段 D 使用与讲解](阶段D使用与讲解.md)，学习状态仍待用户本人验收。


