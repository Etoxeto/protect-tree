# 特效制作与接入说明

本文档记录当前项目中 Unity 表现层特效的制作入口。这里的特效只负责画面表现，不直接修改 Lua 战斗规则。

## 金色粒子凝聚爆发

当前已提供一个通用特效组件：

```text
Assets/Game/Runtime/VFX/GoldenGatherBurstVfx.cs
```

它会表现为：

1. 金色粒子从周围向中心直线凝聚。
2. 中心形成一个金色光球。
3. 光球短暂停留后向外爆发，并产生一圈柔光闪烁。

### 生成 prefab

在 Unity 顶部菜单执行：

```text
Protect Tree > VFX > Create Golden Gather Burst Prefab
```

生成位置：

```text
Assets/Resources/Prefabs/VFX/GoldenGatherBurstVfx.prefab
```

这个 prefab 只有一个根节点脚本，粒子系统和柔光节点会在运行时自动创建。这样可以减少手工节点配置，也方便后续统一调整。

### 场景中临时预览

1. 通过菜单生成 prefab。
2. 将 `GoldenGatherBurstVfx.prefab` 拖到场景中。
3. 进入 Play Mode。
4. 如果 `Play On Enable` 为开启状态，特效会自动播放。

常用可调参数：

| 参数 | 作用 |
| --- | --- |
| `Gather Duration` | 粒子向中心凝聚的时间 |
| `Core Hold Duration` | 光球形成后的停留时间 |
| `Burst Duration` | 爆发阶段持续时间 |
| `Gather Particle Count` | 凝聚粒子数量 |
| `Gather Radius` | 粒子初始分布半径 |
| `Core Max Scale` | 中心光球最大尺寸 |
| `Flash Max Scale` | 爆发柔光最大尺寸 |
| `Burst Particle Count` | 爆发粒子数量 |
| `Burst Upward Bias` | 爆发时给粒子一点向上的初始趋势，方便形成抛物线 |
| `Burst Gravity Modifier` | 爆发粒子的脚本下落强度，数值越大下落越明显 |
| `Sorting Layer Name` | 渲染层 |
| `Sorting Order` | 渲染顺序 |

### 调整尺寸

最快的方法是直接调整 prefab 根节点的 `Transform Scale`。当前粒子系统使用
`Hierarchy` 缩放模式，所以根节点缩放会同时影响凝聚范围、光球、爆发柔光和粒子尺寸。

如果需要更细致地调整，可以改组件上的参数：

| 目标 | 推荐调整 |
| --- | --- |
| 整个特效一起放大/缩小 | 调整根节点 `Transform Scale` |
| 粒子从更远处飞向中心 | 增大 `Gather Radius` |
| 中心光球更大 | 增大 `Core Max Scale` |
| 爆炸瞬间的光圈更大 | 增大 `Flash Max Scale` |
| 粒子本身更粗/更亮眼 | 增大 `Gather Particle Size` 和 `Burst Particle Size` |
| 爆炸飞得更远 | 增大 `Burst Min Speed` 和 `Burst Max Speed` |
| 爆炸后更像散落 | 增大 `Burst Gravity Modifier`，必要时略微增大 `Burst Upward Bias` |

`Burst Gravity Modifier` 不是直接使用 Unity 粒子系统的内置重力，而是由
`GoldenGatherBurstVfx` 在脚本里逐帧改变爆发粒子位置。这样在短生命周期特效中，参数变化会比内置重力更明显。

建议工作流：先用根节点缩放确定整体尺寸，再只微调 `Core Max Scale`、
`Flash Max Scale` 和粒子尺寸。

### 代码中播放

如果只是临时生成并自动销毁，可以调用：

```csharp
using ProtectTree.Runtime.VFX;

GoldenGatherBurstVfx.Spawn(worldPosition, effectRoot, sortingOrder);
```

参数说明：

| 参数 | 作用 |
| --- | --- |
| `worldPosition` | 特效生成的世界坐标 |
| `effectRoot` | 可选父节点，建议使用场景中的 EffectRoot/VfxRoot |
| `sortingOrder` | 渲染顺序，通常要高于棋盘格子和角色底层阴影 |

如果需要复用对象池，也可以手动实例化 prefab，然后调用：

```csharp
vfx.Play();
```

对象池场景下建议关闭 `Auto Destroy`，播放结束后由池管理器回收。

### 放进动画中

Unity 的 Animation Event 会调用挂在同一个动画对象上的公开方法。因此有两种常见做法：

1. 简单预览：把 `GoldenGatherBurstVfx` 组件直接挂在带 `Animator` 的对象上，在动画事件中调用 `Play`。
2. 正式项目：在带 `Animator` 的对象上挂一个“特效触发器”脚本，由动画事件调用触发器方法，再在指定锚点生成 prefab。

第二种更适合 Boss 技能、角色攻击和合成反馈，因为特效位置可以绑定到 `CastPoint`、
`WeaponPoint`、`FootPoint` 等锚点，不会被迫出现在角色根节点。

当前项目已提供通用触发脚本：

```text
Assets/Game/Runtime/VFX/AnimationVfxTrigger.cs
```

推荐节点结构：

```text
BossOrPiece
├── Visual
│   └── Animator
└── CastPoint
```

也可以把 `CastPoint` 放在 `Visual` 下，只要在脚本字段中拖对即可。

组件绑定流程：

1. 在带 `Animator` 的对象上添加 `AnimationVfxTrigger`。
2. 将 `CastPoint` 拖到 `Cast Point` 字段。
3. 将 `GoldenGatherBurstVfx.prefab` 拖到 `Golden Gather Burst Prefab` 字段。
4. `Effect Root` 可不填；如果场景里有统一的 `VfxRoot/EffectRoot`，可以拖进去方便整理层级。
5. `Auto Destroy Spawned Vfx` 保持勾选，播放结束后实例会自动销毁。
6. 根据需要调整 `Sorting Layer Name` 和 `Sorting Order`。

动画事件操作流程：

1. 选中对应动画 clip。
2. 在 Animation 窗口把时间轴拖到需要触发特效的帧。
3. 点击 `Add Event`。
4. Function 选择 `PlayGoldenGatherBurst`。

如果想在 Inspector 里不进动画先试效果，可以点 `AnimationVfxTrigger`
组件右上角菜单，执行 `Preview Golden Gather Burst`。

注意：Animation Event 只适合播放表现，不建议在动画事件里直接扣血或修改 Lua 玩法状态。

### 适合用途

- Boss 法术蓄力。
- 合成棋子后的高级反馈。
- 获得稀有棋子或稀有奖励。
- 场景内某个格子的高亮爆发提示。

## Boss 法术表现

当前 Boss prefab 可以通过 `AnimationVfxTrigger` 播放三类表现：

| 动画事件方法 | 用途 |
| --- | --- |
| `PlayGoldenGatherBurst` | 通用金色凝聚爆发 |
| `PlayShootingMagic` | 在 `Shooting Point` 生成持续射击法阵 |
| `PlayExplosionMagic` | 在 `Explosion Point` 生成爆破法阵 |

`MagicShooting` 和 `MagicExplosion` 是纯表现脚本，只负责播放动画和在持续时间结束后清理实例，不负责造成伤害。

### Magic Shooting

资源位置：

```text
Assets/Resources/Characters/enemy/magic/magic_shooting/magicShooting.prefab
Assets/Game/Runtime/VFX/BossMagic/MagicShooting.cs
```

绑定流程：

1. 在 Boss prefab 的 `AnimationVfxTrigger` 上绑定 `Shooting Point`。
2. 将 `magicShooting.prefab` 拖到 `Magic Shooting Prefab`。
3. `Shooting Magic Duration Seconds` 默认是 `2`，表示射击表现固定播放两秒。
4. 在 Boss 的 `magic_02` 动画最后一帧添加 Animation Event，函数选择 `PlayShootingMagic`。

当前这个表现不会自动打伤害。真正的“对前方直线敌人造成伤害”需要后续在 Lua/Boss 技能规则中生成权威伤害事件，然后表现层再按事件播放对应特效。

### Magic Explosion

资源位置：

```text
Assets/Resources/Characters/enemy/magic/magic_explosion/magicExplosion.prefab
Assets/Game/Runtime/VFX/BossMagic/MagicExplosion.cs
```

绑定流程：

1. 如果只是预览，可在 Boss prefab 下创建一个临时 `Explosion Point`，拖到 `AnimationVfxTrigger.Explosion Point`。
2. 将 `magicExplosion.prefab` 拖到 `Magic Explosion Prefab`。
3. 在 `magic_01` 动画的爆发帧添加 Animation Event，函数选择 `PlayExplosionMagic`。

正式玩法中的“目标脚底”不能只靠 Animation Event 判断。Animation Event 本身不知道当前 Boss 技能目标是谁。后续需要 Boss 技能规则或表现调度器把目标世界坐标传给：

```csharp
PlayExplosionMagicAt(worldPosition);
```

也就是说：动画事件适合做本地预览；真正对目标释放时，应由权威战斗事件或表现层控制器传入目标位置。

### Boss 动画语义

当前 Boss 动画建议按以下含义使用：

| 动画 | 含义 |
| --- | --- |
| `atk_01` | 近战攻击 |
| `magic_01` | 爆破法术 |
| `magic_02` | 射击法术 |
| `move` | 普通移动 |
| `transfer_in` | 传送离场/消失前段 |
| `transfer_out` | 传送入场/落地后段 |

通用敌人攻击事件会优先触发 Animator 参数 `atk`；如果 prefab 没有 `atk`
但有 `atk_01`，则会触发 `atk_01`。因此当前 Boss 的普通近战攻击可以继续使用
`atk_01` 参数，不需要为了通用敌人逻辑额外改名为 `atk`。

`transfer_in` 最后一帧可以作为“切换 Boss 位置”的表现时机，但权威位置切换仍应来自 Boss 换板/传送事件，而不是动画事件直接修改战斗状态。

当前 Boss 法术表现已经具备手动/动画事件入口，但还没有接入权威技能规则：

- `magic_01` 可以通过 `PlayExplosionMagic` 在 `Explosion Point` 或 `Cast Point` 预览爆破效果。
- `magic_02` 可以通过 `PlayShootingMagic` 在 `Shooting Point` 生成固定持续两秒的射击表现。
- 真正的目标选择、直线命中检测、伤害结算、目标脚底爆破位置，后续应由 Lua/Boss 技能事件提供。

### 后续扩展建议

如果需要更强的 Boss 法术表现，可以在这个通用特效外再叠加：

1. 地面法阵 Sprite 或粒子环。
2. 向中心吸入的竖向金色粒子。
3. 爆发后的火柱/光柱 prefab。
4. 屏幕震动和音效。

这些可以拆成多个 prefab 组合，不必把所有效果硬塞进同一个脚本。
