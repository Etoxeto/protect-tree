# 游戏数据配置指南

本文档给策划使用，目标是：不需要程序经验，也能安全修改棋子、敌人、羁绊、商店、波次、玩家基础数值、流程时间，以及角色/敌人 prefab、头像、职业图标、投射物等资源映射。

如果只记一件事：**Lua 配置负责玩法数据，Unity Resources/Catalog 负责画面资源**。不要把 prefab 路径写进 Lua，也不要在 Unity prefab 上改玩法数值。

## 0. 修改前必读

### 推荐编辑器

建议用支持 UTF-8 的文本编辑器，例如 VS Code、Rider、Notepad++。

保存格式建议：

- 编码：UTF-8
- 换行：保持原文件即可
- 标点：使用英文半角符号，例如 `,`、`"`、`{}`、`[]`

### Lua 配置的基本规则

Lua 配置文件通常长这样：

```lua
return {
    SomeId = {
        display_name = "显示名称",
        cost = 3,
    },
}
```

编辑时注意：

- `SomeId` 是内部 ID，必须唯一。
- 字符串要用英文双引号，例如 `"Crab"`。
- 数字不要加引号，例如 `cost = 3`。
- 列表里的每一项后面建议保留逗号。
- 大小写要完全一致：`Crab` 和 `crab` 是两个不同 ID。
- 不要删除最外层的 `return { ... }`。

### 最常见错误

| 现象 | 常见原因 |
| --- | --- |
| Unity Console 出现 `config not found` | 某个 ID 写错、大小写不一致，或忘记在对应配置表中新增 |
| 商店不刷某个棋子 | 棋子的 `rarity` 不在商店权重中，或 `rarity` 超过 `Shop.rarity_count` |
| 商店图标缺失 | `portrait` 或 `class_id` 与 Resources 里的图片名不一致 |
| 场上显示方块占位 | `DefaultUnitVisualCatalog` 没有配置对应 prefab |
| 投射物不出现 | `DefaultProjectileCatalog` 没有配置该 `piece_id`，或棋子/敌人缺少可用锚点 |
| 波次启动时报错 | 波次引用了不存在的 `enemy_id`，或 Boss 敌人放进普通波次 |

## 1. 数据文件总览

### Lua 玩法配置

| 内容 | 文件 |
| --- | --- |
| 棋子/塔防单位 | `Assets/Game/Lua/Config/Pieces.lua.txt` |
| 敌人/Boss | `Assets/Game/Lua/Config/Enemies.lua.txt` |
| 羁绊 | `Assets/Game/Lua/Config/Synergies.lua.txt` |
| 商店等级、刷新费用、品质概率 | `Assets/Game/Lua/Config/Shop.lua.txt` |
| 波次计划、波次预设、刷怪点、刷怪时间 | `Assets/Game/Lua/Config/Waves.lua.txt` |
| 准备/结算阶段时长、Boss 换板时间 | `Assets/Game/Lua/Config/MatchFlow.lua.txt` |
| 玩家初始金币、血量、回合奖励 | `Assets/Game/Lua/Config/Players.lua.txt` |
| 棋盘格、路线、出生点、终点 | `Assets/Game/Lua/Config/Board.lua.txt` |

### 常用全局数值入口速查

| 目标 | 修改位置 |
| --- | --- |
| 玩家最大生命值 | `Assets/Game/Lua/Config/Players.lua.txt` 的 `build_default_player().max_health`，或单独改 `definitions[玩家ID].max_health` |
| 初始金币、回合奖励 | `Players.lua.txt` 的 `starting_gold`、`round_reward`、`round_reward_per_wave_increment` |
| 初始人口/初始可上场数 | `Players.lua.txt` 的 `starting_deployment_limit`，并保持它和 `Shop.lua.txt` 第 1 级 `deployment_limit` 一致 |
| 商店最大等级 | `Assets/Game/Lua/Config/Shop.lua.txt` 的 `levels` 表长度；新增 `[4]`、`[5]` 等等级即提高最大等级 |
| 每级升级花费 | `Shop.lua.txt` 中每个等级的 `upgrade_cost`；它表示“从当前等级升到下一级”的费用，最高级通常填 `0` |
| 每级人口/可上场数 | `Shop.lua.txt` 中每个等级的 `deployment_limit` |
| 每级商品数量 | `Shop.lua.txt` 中每个等级的 `offer_count` |
| 商品品质数量 | `Shop.lua.txt` 的 `rarity_count` |
| 每级商品出现概率 | `Shop.lua.txt` 中每个等级的 `rarity_weights`，按品质 1 到品质 N 顺序填写，总和必须为 100 |

### Unity 画面资源配置

| 内容 | 配置入口 |
| --- | --- |
| 棋子 prefab、敌人 prefab | `Assets/Resources/Board/DefaultUnitVisualCatalog.asset` |
| 远程投射物 prefab | `Assets/Resources/Board/DefaultProjectileCatalog.asset` |
| 商店头像/角色插图 | `Assets/Resources/UI/Characters/` |
| 职业图标 | `Assets/Resources/UI/Icons/CharacterType/` |
| 羁绊图标 | `Assets/Resources/UI/Icons/Synergy/` |
| 玩家头像 | `Assets/Resources/UI/Infos/Player/Avatars/` |
| 棋盘材质/格子视觉覆盖 | `Assets/Resources/Board/DefaultBoardVisual.asset` |

### 音频资源配置

当前音频资源使用 `Resources` 加载：

| 内容 | 放置位置 |
| --- | --- |
| 音效 | `Assets/Resources/Audio/SFX/` |
| 背景音乐 | `Assets/Resources/Audio/BGM/` |

音效 ID 等于文件名，不带扩展名。例如：

| 音效 ID | 文件 |
| --- | --- |
| `button_01` | `Assets/Resources/Audio/SFX/button_01.wav` |
| `coin_spend` | `Assets/Resources/Audio/SFX/coin_spend.wav` |

当前已接入：

- 主菜单按钮：`button_01`
- 商店锁定按钮：`button_01`
- 商店购买、刷新、升级：`coin_spend`
- 主菜单/准备/结算：`bgm_normal`
- 普通战斗/联防：`bgm_battle`
- Boss 战：`bgm_goatBoss`
- 棋子普通攻击：读取棋子配置中的 `attack_sfx_id`
- 敌人普通攻击：读取敌人配置中的 `attack_sfx_id`
- 风暴/魔力羁绊攻击：`atk_storm_01`、`atk_magic_02`
- 命中反馈：`atk_impact_01`
- 敌人死亡：`die_01`
- 本地玩家扣血：`battle_leak`
- 本地玩家本回合无最终漏怪：`battle_clear`

设置界面的主音量、BGM 音量、音效音量会保存到 `PlayerPrefs`。

棋子普通攻击音效入口在 `Assets/Game/Lua/Config/Pieces.lua.txt` 的每个棋子配置中：

```lua
golden_soldier = {
    portrait = "soldier_golden",
    class_id = "Guard",
    attack_sfx_id = "atk_sword_01",
}
```

`attack_sfx_id` 对应 `Assets/Resources/Audio/SFX/` 下的文件名，不带扩展名。
例如 `attack_sfx_id = "atk_magic_01"` 会加载
`Assets/Resources/Audio/SFX/atk_magic_01.wav`。如果该字段为空字符串或不填，
普通攻击不会播放棋子自己的攻击音效，但命中、死亡、漏怪等公共反馈仍会照常播放。

敌人普通攻击音效入口在 `Assets/Game/Lua/Config/Enemies.lua.txt` 的每个敌人配置中：

```lua
Crab = {
    attack_sfx_id = "atk_impact_01",
}
```

规则与棋子相同：字段值是 `Assets/Resources/Audio/SFX/` 下的文件名，不带扩展名。
如果不填或填空字符串，该敌人攻击时不播放专属攻击音效。

当前没有使用 Unity Audio Mixer。项目通过 `AudioManager` 管理两个 2D
`AudioSource`：一个循环播放 BGM，另一个用 `PlayOneShot` 播放音效。Unity
会自动把多个 `AudioSource` 混合输出；设置界面的音量滑条通过调整这两个
`AudioSource.volume` 实现主音量、BGM 音量和音效音量。

### 玩家头像

设置界面的头像选择使用 `Assets/Resources/UI/Infos/Player/Avatars/`
中的图片。头像 ID 等于文件名，不带扩展名。例如：

| 头像资源 | 保存路径 |
| --- | --- |
| `Assets/Resources/UI/Infos/Player/Avatars/sheep_head.png` | `UI/Infos/Player/Avatars/sheep_head` |

`UIAvatarSetting` 会读取 `AvatarSetting/Scroll View/Viewport/Content`
下每个 Button 的头像图片，点击后把对应路径保存到 `PlayerPrefs`。LAN
房间快照会同步该路径，所以房间内可以看到各玩家选择的头像。房间里的
`UIPlayerInfo` 会让头像与 `personalBackground` 使用同一张图片；进入战斗后，
`PlayerInfos` 会继续使用从房间带入的玩家头像路径。单人模式下则使用本地
`PlayerPrefs` 中保存的头像。

## 2. ID 与资源名的关系

本项目大量使用“内部 ID”。内部 ID 不一定是显示给玩家看的中文名。

### 棋子 ID

例：

```lua
Bloom = {
    display_name = "风暴术士",
}
```

其中：

- `Bloom` 是棋子内部 ID。
- `display_name` 是 UI 显示名。
- 波次、商店、prefab 映射、投射物映射都可能使用内部 ID。

### 敌人 ID

例：

```lua
Crab = {
    max_health = 10,
}
```

其中 `Crab` 是敌人内部 ID。波次中写的 `enemy_id = "Crab"` 必须与它完全一致。

### 羁绊 ID

例：

```lua
Arcane = {
    display_name = "奥术",
}
```

棋子里写：

```lua
synergies = {
    "Arcane",
},
```

这里的 `"Arcane"` 必须在 `Synergies.lua.txt` 中存在。

### 头像 portrait

棋子里写：

```lua
portrait = "soldier_arcane",
```

会加载：

```text
Assets/Resources/UI/Characters/soldier_arcane.png
```

配置里不要写 `.png`。

### 职业 class_id

棋子里写：

```lua
class_id = "Magician",
```

会加载：

```text
Assets/Resources/UI/Icons/CharacterType/Magician.png
```

配置里不要写 `.png`。

当前已有职业图标名：

- `Archer`
- `Guard`
- `Healer`
- `Magician`
- `Warrior`

如果新增职业图标，比如 `Assassin.png`，棋子中就写：

```lua
class_id = "Assassin",
```

## 3. 棋子配置

文件：

```text
Assets/Game/Lua/Config/Pieces.lua.txt
```

### 棋子完整模板

新增棋子时，可以复制下面模板，再改 ID 和字段：

```lua
NewPieceId = {
    display_name = "显示名称",
    portrait = "头像资源名",
    class_id = "职业图标名",
    cost = 3,
    rarity = 1,
    synergies = {
        "Arcane",
    },
    attack_interval_seconds = 1.0,
    default_facing = "Right",
    attack_range = {
        { forward = 0, right = 0 },
        { forward = 1, right = 0 },
    },
    recovery_seconds = 3.0,
    deployable_terrains = {
        Ground = true,
        HighGround = true,
    },
    levels = {
        [1] = {
            sell_value = 3,
            max_health = 12,
            max_block_count = 1,
            damage = 4,
        },
        [2] = {
            sell_value = 9,
            max_health = 24,
            max_block_count = 2,
            damage = 8,
        },
        [3] = {
            sell_value = 27,
            max_health = 48,
            max_block_count = 3,
            damage = 16,
        },
    },
},
```

### 字段解释

| 字段 | 说明 |
| --- | --- |
| `display_name` | 玩家看到的棋子名 |
| `portrait` | 商店和信息面板头像资源名，不写 `.png` |
| `class_id` | 职业图标资源名，不写 `.png` |
| `cost` | 商店购买费用 |
| `rarity` | 品质，当前支持 1 到 6 |
| `synergies` | 棋子的羁绊 ID 列表，至少 1 个 |
| `attack_interval_seconds` | 攻击间隔，越小攻击越快 |
| `attack_impact_delay_seconds` | 普通攻击从播放攻击动画到真正出伤害的延迟秒数；当前配置一般为 0.35，不填时代码默认 0.2 |
| `attack_type` | 攻击类型。可不填，默认 `"Single"`；填 `"Area"` 时会在命中帧同时攻击范围内所有敌人 |
| `default_facing` | 默认朝向：`Up`、`Right`、`Down`、`Left` |
| `attack_range` | 攻击范围，见下一节 |
| `recovery_seconds` | 棋子倒地后在本回合内恢复所需时间 |
| `deployable_terrains` | 可部署地形 |
| `levels` | 1 星、2 星、3 星属性 |

### 攻击范围 attack_range

攻击范围使用“相对坐标”：

```lua
attack_range = {
    { forward = 0, right = 0 },
    { forward = 1, right = 0 },
    { forward = 2, right = 0 },
},
```

含义：

- `forward`：面朝方向前进几格。
- `right`：相对角色右侧偏几格。
- `{ forward = 0, right = 0 }` 表示自身所在格。

例 1：攻击自己前方两格，包括自己所在格：

```lua
attack_range = {
    { forward = 0, right = 0 },
    { forward = 1, right = 0 },
    { forward = 2, right = 0 },
},
```

例 2：扇形范围，前方两排各三格：

```lua
attack_range = {
    { forward = 1, right = -1 },
    { forward = 1, right = 0 },
    { forward = 1, right = 1 },
    { forward = 2, right = -1 },
    { forward = 2, right = 0 },
    { forward = 2, right = 1 },
},
```

注意：

- 攻击范围既影响实际攻击判定，也影响战场上的橙红色范围预览。
- 配置中的格子超出棋盘不会报错，但不会命中任何敌人。

### 攻击类型 attack_type

```lua
-- 不填时等同于单体攻击
attack_type = "Single"

-- 群体攻击：命中帧对当前攻击范围内的所有敌人造成伤害
attack_type = "Area"
```

当前规则：
- `"Single"`：从攻击范围内选择一个目标，优先攻击正在被自己阻挡的敌人，其次攻击路线进度更靠前的敌人。
- `"Area"`：攻击动画只播放一次；到命中帧时，攻击范围内所有仍然存活且仍在范围内的敌人都会受到同一份伤害。
- `attack_type` 只影响棋子普通攻击，不影响【风暴】旋风、【魔力】辉剑、Boss 技能等额外效果。

### 可部署地形 deployable_terrains

地面和高台都能放：

```lua
deployable_terrains = {
    Ground = true,
    HighGround = true,
},
```

只能放地面：

```lua
deployable_terrains = {
    Ground = true,
},
```

只能放高台：

```lua
deployable_terrains = {
    HighGround = true,
},
```

当前可用地形：

- `Ground`：地面，敌人会经过，棋子可阻挡。
- `HighGround`：高台，敌人不经过，棋子通常不能被近战敌人攻击。
- `Obstacle`：障碍，不可部署。

### 等级属性 levels

每个棋子需要配置 1、2、3 级：

```lua
levels = {
    [1] = {
        sell_value = 3,
        max_health = 12,
        max_block_count = 1,
        damage = 4,
    },
    [2] = {
        sell_value = 9,
        max_health = 24,
        max_block_count = 2,
        damage = 8,
    },
    [3] = {
        sell_value = 27,
        max_health = 48,
        max_block_count = 3,
        damage = 16,
    },
},
```

字段解释：

- `sell_value`：出售返还金币。
- `max_health`：最大生命。
- `max_block_count`：最大阻挡数。高台角色可填 0。
- `damage`：每次攻击伤害。

### 新增棋子完整流程

1. 在 `Pieces.lua.txt` 里复制一个棋子配置，改成新 ID。
2. 设置 `display_name`、`cost`、`rarity`、属性和攻击范围。
3. 确认 `synergies` 中的羁绊 ID 已在 `Synergies.lua.txt` 中存在。
4. 把头像图片放入 `Assets/Resources/UI/Characters/`。
5. `portrait` 填图片文件名，不带 `.png`。
6. 把职业图标放入 `Assets/Resources/UI/Icons/CharacterType/`。
7. `class_id` 填职业图标文件名，不带 `.png`。
8. 做好棋子 prefab，根节点挂 `BoardUnitSocket`，远程棋子绑定 `FirePoint`。
9. 打开 `Assets/Resources/Board/DefaultUnitVisualCatalog.asset`。
10. 在 `Piece Entries` 中新增一项：`Unit Id = 棋子ID`，`Prefab = 棋子prefab`。
11. 如果是远程棋子，打开 `DefaultProjectileCatalog.asset`，为该棋子增加投射物配置。
12. 进入 Unity Play Mode，检查商店、部署、攻击范围、攻击动画和投射物。

## 4. 敌人配置

文件：

```text
Assets/Game/Lua/Config/Enemies.lua.txt
```

### 敌人模板

```lua
NewEnemyId = {
    max_health = 10,
    path_speed = 0.05,
    attack_damage = 3,
    attack_interval_seconds = 1.0,
    attack_type = "Melee",
    attack_sfx_id = "atk_impact_01",
    route_id = 1,
},
```

### Boss 模板

```lua
NewBossId = {
    max_health = 120,
    path_speed = 0.05,
    attack_damage = 8,
    attack_interval_seconds = 1.2,
    attack_type = "Melee",
    attack_sfx_id = "atk_impact_01",
    route_id = 1,
    is_boss = true,
},
```

### 字段解释

| 字段 | 说明 |
| --- | --- |
| `max_health` | 最大生命 |
| `path_speed` | 沿路线移动速度，越大越快 |
| `attack_damage` | 攻击被阻挡棋子的伤害 |
| `attack_interval_seconds` | 攻击间隔 |
| `attack_impact_delay_seconds` | 普通攻击从播放攻击动画到真正出伤害的延迟秒数；当前配置一般为 0.35，不填时代码默认 0.2 |
| `attack_type` | 当前主要使用 `"Melee"` |
| `attack_sfx_id` | 普通攻击音效 ID，对应 `Assets/Resources/Audio/SFX/` 下不带扩展名的文件名。不填则不播放专属攻击音效 |
| `route_id` | 默认路线。波次也可以覆盖路线 |
| `is_boss` | 是否 Boss，普通敌人不要填或填 `false` |

### 新增敌人完整流程

1. 在 `Enemies.lua.txt` 中复制一个敌人配置，改成新 ID。
2. 调整血量、速度、伤害等。
3. 做好敌人 prefab，根节点挂 `BoardUnitSocket`，绑定 `HitPoint`。
4. 打开 `Assets/Resources/Board/DefaultUnitVisualCatalog.asset`。
5. 在 `Enemy Entries` 中新增一项：`Unit Id = 敌人ID`，`Prefab = 敌人prefab`。
6. 在 `Waves.lua.txt` 的波次中引用这个敌人 ID。
7. 进入 Play Mode，确认敌人能生成、移动、被攻击、显示正确 prefab。

## 5. 羁绊配置

文件：

```text
Assets/Game/Lua/Config/Synergies.lua.txt
```

### 文件结构

```lua
return {
    order = {
        "Arcane",
        "Golden",
    },
    definitions = {
        Arcane = {
            display_name = "奥术",
            levels = {
                {
                    required_unique_pieces = 2,
                    damage_bonus = 3,
                },
            },
        },
    },
}
```

### order

`order` 决定 UI 中羁绊显示顺序。

新增羁绊时，建议同时加到 `order`：

```lua
order = {
    "Arcane",
    "Golden",
    "NewSynergy",
},
```

### definitions

每个羁绊需要一个定义：

```lua
NewSynergy = {
    display_name = "新羁绊",
    levels = {
        {
            required_unique_pieces = 2,
            damage_bonus = 3,
        },
        {
            required_unique_pieces = 4,
            damage_bonus = 8,
        },
    },
},
```

字段解释：

- `display_name`：玩家看到的羁绊名。
- `required_unique_pieces`：需要上场多少种不同棋子。
- `damage_bonus`：激活后给相关棋子增加的伤害。

当前羁绊统计规则：

- 只统计战场上的棋子。
- 同一个棋子 ID 多个复制品只算 1 种。
- 棋子必须在自己的 `synergies` 中拥有该羁绊。

### 羁绊图标

UI 会从这里加载羁绊图标：

```text
Assets/Resources/UI/Icons/Synergy/{synergy_id}.png
```

例如羁绊 ID 是 `Arcane`，图标文件应为：

```text
Assets/Resources/UI/Icons/Synergy/Arcane.png
```

### 羁绊详情面板

战斗界面顶部的羁绊栏可以点击单个羁绊，打开详情面板。

当前详情面板显示：

- 羁绊名和图标。
- 当前上场不同棋子数/激活所需不同棋子数。
- 临时效果描述。
- 当前玩家已经拥有的该羁绊棋子头像。

头像颜色规则：

- 该棋子正在战场上：原色显示。
- 该棋子在备战区或临时备战区：灰色显示。

注意：当前详情面板还不是完整图鉴列表。它不会显示“配置中存在、但玩家本局还没有获得过”的棋子。
如果后续需要完整图鉴，需要从 Lua 配置额外导出一份只读棋子配置列表给 UI 使用。

### 新增羁绊完整流程

1. 在 `Synergies.lua.txt` 的 `definitions` 中新增羁绊。
2. 在 `order` 中加入该羁绊 ID。
3. 在需要该羁绊的棋子 `synergies` 中加入该 ID。
4. 放入同名羁绊图标。
5. 进入 Play Mode，部署相关棋子，检查羁绊条是否显示进度和激活状态。

## 6. 商店配置

文件：

```text
Assets/Game/Lua/Config/Shop.lua.txt
```

### 示例结构

```lua
return {
    random_seed = 20260625,
    rarity_count = 6,
    refresh_cost = 2,
    levels = {
        [1] = {
            upgrade_cost = 4,
            deployment_limit = 2,
            offer_count = 2,
            rarity_weights = {
                100, 0, 0, 0, 0, 0,
            },
        },
    },
}
```

### 字段解释

| 字段 | 说明 |
| --- | --- |
| `random_seed` | 商店确定性随机种子。Host/Lua 权威端用它生成每名玩家的独立商店序列 |
| `rarity_count` | 品质数量。当前为 6，不建议随意修改 |
| `refresh_cost` | 刷新商店花费 |
| `levels` | 商店等级配置 |
| `upgrade_cost` | 升到下一级商店所需金币。最高级通常填 0 |
| `deployment_limit` | 当前等级可上场棋子数 |
| `offer_count` | 商店每次显示几个商品 |
| `rarity_weights` | 该等级刷出各品质棋子的概率 |

### rarity_weights

`rarity_weights` 有 6 个数字，对应品质 1 到 6。

例如：

```lua
rarity_weights = {
    60,
    30,
    6,
    2,
    1,
    1,
},
```

含义：

- 品质 1：60%
- 品质 2：30%
- 品质 3：6%
- 品质 4：2%
- 品质 5：1%
- 品质 6：1%

注意：

- 6 个数字总和必须是 100。
- 某个品质暂时没有棋子也可以保留概率。系统会临时向其它已有品质回退。以后添加该品质棋子后，会自动进入商店池。

## 7. 波次配置

文件：

```text
Assets/Game/Lua/Config/Waves.lua.txt
```

波次配置分三层：

1. `schedule`：决定普通波数量和 Boss 波编号。
2. `presets`：先做“波次预设”。
3. `wave_pools`：每一波从哪些预设中选择。

### schedule

```lua
schedule = {
    normal_wave_count = 6,
    boss_wave = 7,
},
```

字段解释：

| 字段 | 说明 |
| --- | --- |
| `normal_wave_count` | 普通波数量 |
| `boss_wave` | Boss 波编号，必须等于 `normal_wave_count + 1` |

注意：

- 如果 Boss 在第 7 波，应该写 `normal_wave_count = 6`、`boss_wave = 7`。
- `wave_pools` 中必须配置第 1 波到第 `normal_wave_count` 波，以及第 `boss_wave` 波。
- 普通波不能刷 Boss 敌人；Boss 波必须刷 Boss 敌人。

### 预设 preset

一个预设可以包含多个刷怪组：

```lua
Normal_Split_Light = {
    groups = {
        {
            group_id = "A",
            route_id = 1,
            start_seconds = 10,
            interval_seconds = 1.0,
            enemies = {
                { enemy_id = "Crab", count = 3 },
                { enemy_id = "Skitter", count = 2 },
            },
        },
        {
            group_id = "B",
            route_id = 2,
            start_seconds = 1,
            interval_seconds = 0.75,
            enemies = {
                { enemy_id = "Crab", count = 7 },
                { enemy_id = "Skitter", count = 3 },
            },
        },
    },
},
```

字段解释：

| 字段 | 说明 |
| --- | --- |
| `group_id` | 刷怪组名字，只用于阅读和日志 |
| `route_id` | 从哪条路线刷怪 |
| `start_seconds` | 战斗开始后第几秒开始刷 |
| `interval_seconds` | 该组每只怪之间间隔 |
| `enemies` | 敌人队列 |
| `enemy_id` | 敌人 ID，必须在 `Enemies.lua.txt` 中存在 |
| `count` | 该敌人数量 |

### wave_pools

```lua
wave_pools = {
    [1] = { "Normal_Warmup_A" },
    [2] = { "Normal_RouteB_Skitter", "Normal_Split_Light" },
    [6] = { "Normal_Final_Pressure" },
    [7] = { "Boss_AncientGuardian" },
},
```

含义：

- 第 1 波固定使用 `Normal_Warmup_A`。
- 第 2 波会从两个预设中随机选择一个。
- 第 6 波是普通波。
- 第 7 波是 Boss 波。

注意：

- 随机由 Host/Lua 权威决定，联机客户端不会各自随机。
- 普通波不能刷 `is_boss = true` 的敌人。
- Boss 波必须刷 Boss 敌人。
- `wave_pools` 中的波数需要与同文件里的 `schedule` 对应。

### 新增一波完整流程

1. 在 `presets` 中新增一个预设。
2. 每个刷怪组设置路线、开始时间、间隔和敌人队列。
3. 确保所有 `enemy_id` 都存在。
4. 在 `wave_pools` 中把该预设加入某一波。
5. 如果新增的是普通波，还要同步调整 `schedule.normal_wave_count`
   和 `schedule.boss_wave`，并给新的 Boss 波配置 Boss 预设。
6. 进入 Play Mode，观察该波刷怪点、时间和数量是否符合预期。

## 8. 流程时间配置

文件：

```text
Assets/Game/Lua/Config/MatchFlow.lua.txt
```

当前结构：

```lua
return {
    preparation_seconds = 90,
    battle_defense_seconds = 90,
    joint_defense_intro_seconds = 1.5,
    boss_preparation_seconds = 90,
    boss_defense_seconds = 300,
    joint_defense_seconds = 90,
    boss_transfer_intro_seconds = 0.75,
    settlement_seconds = 2,
}
```

字段解释：

| 字段 | 说明 |
| --- | --- |
| `preparation_seconds` | 普通备战阶段秒数 |
| `battle_defense_seconds` | 普通战斗防守时间，超时未击杀的已生成敌人按漏怪处理 |
| `joint_defense_intro_seconds` | 进入联防前的提示冻结时间；该阶段不转移敌人、不推进战斗 |
| `boss_preparation_seconds` | Boss 前备战阶段秒数 |
| `boss_defense_seconds` | Boss 战防守时间，超时则失败 |
| `joint_defense_seconds` | 联防防守时间，超时未处理的联防敌人按漏怪处理 |
| `boss_transfer_intro_seconds` | Boss 播放 `transfer_in` 后等待多久再切换到下一个玩家棋盘 |
| `settlement_seconds` | 结算阶段停留时间 |

修改建议：

- 只在这里修改阶段时长。
- 普通波数量和 Boss 波编号不要写在这里，改 `Waves.lua.txt` 的 `schedule`。

## 9. 玩家基础配置

文件：

```text
Assets/Game/Lua/Config/Players.lua.txt
```

当前使用一个默认玩家模板：

```lua
local function build_default_player()
    return {
        max_health = 10,
        starting_gold = 10,
        round_reward = 5,
        round_reward_per_wave_increment = 1,
        bench_capacity = board.reserve_capacity,
        starting_deployment_limit = 2,
    }
end
```

字段解释：

| 字段 | 说明 |
| --- | --- |
| `max_health` | 玩家最大血量 |
| `starting_gold` | 开局金币 |
| `round_reward` | 第 1 回合结算奖励金币 |
| `round_reward_per_wave_increment` | 每经过 1 个波次，结算奖励额外增加多少金币。例：`round_reward = 5` 且该值为 `1` 时，第 1/2/3 回合分别获得 5/6/7 金币 |
| `bench_capacity` | 普通备战区容量 |
| `starting_deployment_limit` | 初始可上场棋子数 |

`active_player_ids` 当前默认只启用玩家 1。正式多人由联机入口传入实际玩家数量，不建议策划在这里直接开启多个玩家。

## 10. 棋盘与路线配置

文件：

```text
Assets/Game/Lua/Config/Board.lua.txt
```

棋盘配置涉及路径合法性、部署合法性、出生点、终点和视觉格子，修改风险较高。普通数值调整通常不需要改这里。

### 地图字符

当前地图使用字符矩阵：

```lua
local rows_from_back_to_front = {
    "XXXXbbbbbbb", -- y = 6，最远离屏幕
    "XXB1.......", -- y = 5
    "XXA1.11...A", -- y = 4
    "XAA..11...A", -- y = 3
    "...........", -- y = 2
    "AAAAAAAAAAA", -- y = 1，临时备战区
    "11111111111", -- y = 0，最靠近屏幕的普通备战区
}
```

字符含义：

| 字符 | 地形 | 高度 | 说明 |
| --- | --- | --- | --- |
| `.` | Ground | 0 | 地面，敌人可走，棋子可部署 |
| `1` | HighGround | 1 | 一级高台 |
| `2` | HighGround | 2 | 二级高台 |
| `3` | HighGround | 3 | 三级高台 |
| `X` | Obstacle | 0 | 障碍 |
| `A` | Obstacle | 1 | 一级障碍 |
| `B` | Obstacle | 2 | 二级障碍 |
| `C` | Obstacle | 3 | 三级障碍 |

小写 `b` 当前也表示二级障碍。

### 特殊区域

特殊区域由坐标决定：

- `y = 0`：普通备战区。
- `y = 1`：临时备战区。
- `(10, 2)` 和 `(10, 5)`：刷怪点。
- `(2, 2)`：防守点/终点。
- 其它区域：战场。

### 路线

路线由 `add_route(route_id, coordinates)` 定义。

波次里的 `route_id` 必须引用这里存在的路线。

修改路线时注意：

- 路线必须从刷怪点开始。
- 路线必须到终点结束。
- 路线上所有格子必须允许敌人经过。
- 相邻路线点必须相邻，不要跳格。
- 改错后 Unity Console 会在启动 Session 时显示路线校验错误。

## 11. Unity 资源映射

### 棋子/敌人 prefab

配置入口：

```text
Assets/Resources/Board/DefaultUnitVisualCatalog.asset
```

打开方式：

1. 在 Unity Project 窗口找到该 asset。
2. Inspector 中有两个列表：
   - `Piece Entries`
   - `Enemy Entries`
3. 新增条目时：
   - `Unit Id` 填 Lua 内部 ID。
   - `Prefab` 拖入对应 prefab。

例：

```text
Piece Entries:
  Unit Id = Bloom
  Prefab = magician.prefab

Enemy Entries:
  Unit Id = Crab
  Prefab = crab.prefab
```

如果没有配置 prefab，游戏会显示临时方块占位。

### 棋子星级特效

棋子升到 2 星、3 星后，场上会自动显示星级视觉效果：

- 1 星：正常大小，无常驻光效。
- 2 星：棋子略微变大，并显示偏白色柔光。
- 3 星：棋子继续略微变大，并显示偏金色柔光。
- 合成瞬间：保留下来的那枚棋子会播放一次闪光爆发。

这套效果由以下脚本统一管理：

```text
Assets/Game/Runtime/Board/BoardPieceLevelVfx.cs
```

通常不需要给每个棋子单独制作一套特效。运行时如果棋子 prefab 没有挂
`BoardPieceLevelVfx`，程序会自动补一个默认光效。

如果美术希望手动调整某个棋子的星级效果，可以在该棋子 prefab 的根节点或子节点上挂
`BoardPieceLevelVfx`，并按需要绑定：

```text
Visual
├── Rig / PSBroot
├── Shadow
├── SelectionAnchor
└── LevelVfxRoot
    ├── LevelAura
    └── MergeBurst
```

推荐节点含义：

| 节点/字段 | 作用 |
| --- | --- |
| `BoardPieceLevelVfx` | 统一控制星级光效 |
| `LevelVfxRoot` | 光效根节点，会跟随 `SelectionAnchor` |
| `LevelAura` | 2 星/3 星常驻光晕，可挂 `SpriteRenderer` |
| `MergeBurst` | 合成瞬间闪光，可挂 `SpriteRenderer` |
| `Level Three Particles` | 可选，3 星常驻粒子 |

如果不绑定 `LevelAura` 或 `MergeBurst`，脚本会自动生成柔光圆形 Sprite。
如果绑定了自己的 `SpriteRenderer` 和图片，脚本会使用美术资源。

默认自动光晕会接收一个“角色尺寸提示”。这个尺寸提示目前由角色视觉实例根据可交互范围估算，
用于避免不同体型棋子的默认柔光过小。`BoardPieceLevelVfx` 本身不直接查找 `HitArea`，
只使用传入的普通尺寸数据，因此后续仍可以替换点击区域或 prefab 结构。

策划/美术常调参数：

| 参数 | 说明 |
| --- | --- |
| `Level Two Aura Color` | 2 星光晕颜色 |
| `Level Three Aura Color` | 3 星光晕颜色 |
| `Level Two Aura Scale` | 2 星光晕大小 |
| `Level Three Aura Scale` | 3 星光晕大小 |
| `Use Auto Size Hint` | 是否用角色尺寸提示自动撑大默认光晕 |
| `Minimum Auto Aura Size` | 自动光晕的最低大小 |
| `Level Two Auto Size Padding` | 2 星自动光晕相对角色尺寸的放大倍率 |
| `Level Three Auto Size Padding` | 3 星自动光晕相对角色尺寸的放大倍率 |
| `Aura Pulse Speed` | 常驻光晕呼吸速度 |
| `Aura Pulse Scale` | 常驻光晕呼吸幅度 |
| `Level Two Burst Color` | 2 星合成闪光颜色 |
| `Level Three Burst Color` | 3 星合成闪光颜色 |
| `Burst Duration` | 合成闪光持续时间 |

注意：

- 这里是表现层效果，不影响棋子的真实等级、攻击力、血量。
- 棋子真实等级仍由 Lua 的三合一规则决定。
- 如果未来需要真正“描边”而不是柔光，需要再做专门的 Sprite/Shader 方案。

### 远程投射物 prefab

配置入口：

```text
Assets/Resources/Board/DefaultProjectileCatalog.asset
```

字段：

| 字段 | 说明 |
| --- | --- |
| `Piece Id` | 棋子 ID |
| `Projectile Prefab` | 投射物 prefab |
| `Fire Delay Seconds` | 攻击动画播放后多久发射 |
| `Speed` | 飞行速度 |
| `Impact Hold Seconds` | 命中后停留多久 |
| `Rotate To Velocity` | 是否朝飞行方向旋转 |
| `Base Scale` | 投射物基础缩放，普通投射物通常为 `1` |
| `Layers Per Scale Step` | 羁绊层数每多少层计算一次尺寸增长，当前建议 `20` |
| `Scale Per Layer Step` | 每次层数尺寸增长增加的缩放值；`0.1` 表示每档增大 10% |

箭矢通常：

- `Rotate To Velocity = true`
- `Impact Hold Seconds` 可以略大，例如 `0.08`

法术通常：

- `Rotate To Velocity` 可开可关。
- `Impact Hold Seconds` 可为 `0` 或很小。

羁绊投射物也在这里配置，但 `Piece Id` 填羁绊投射物 ID：

| 效果 | `Piece Id` | 当前 prefab |
| --- | --- | --- |
| 风暴旋风 | `Storm` | `Storm_0_Projectile` |
| 魔力辉剑 | `MagicPower` | `MagicSword_0_Projectile` |

风暴和魔力当前按羁绊层数放大投射物：`Layers Per Scale Step = 20`，`Scale Per Layer Step = 0.1`，表示每 20 层尺寸增加 10%。普通棋子投射物如果不需要随层数变化，把 `Scale Per Layer Step` 保持为 `0`。

投射物 prefab 根节点需要挂：

```text
BoardProjectileVisual
```

棋子 prefab 需要：

```text
BoardUnitSocket.FirePoint
```

敌人 prefab 需要：

```text
BoardUnitSocket.HitPoint
```

### 商店头像

路径：

```text
Assets/Resources/UI/Characters/
```

配置：

```lua
portrait = "soldier_arcane",
```

对应文件：

```text
Assets/Resources/UI/Characters/soldier_arcane.png
```

### 职业图标

路径：

```text
Assets/Resources/UI/Icons/CharacterType/
```

配置：

```lua
class_id = "Magician",
```

对应文件：

```text
Assets/Resources/UI/Icons/CharacterType/Magician.png
```

### 羁绊图标

路径：

```text
Assets/Resources/UI/Icons/Synergy/
```

配置使用羁绊 ID：

```lua
synergies = {
    "Arcane",
},
```

对应文件：

```text
Assets/Resources/UI/Icons/Synergy/Arcane.png
```

## 12. 常用工作流

### 新增一个普通近战棋子

1. 在 `Pieces.lua.txt` 新增棋子。
2. 设置 `deployable_terrains = { Ground = true }`。
3. 设置 `max_block_count` 大于 0。
4. 设置攻击范围通常包含自身格和前方格。
5. 加入至少一个羁绊。
6. 准备头像和职业图标。
7. 做 prefab，并在 `DefaultUnitVisualCatalog.asset` 中绑定。
8. 如果需要单独微调星级光效，在 prefab 上挂 `BoardPieceLevelVfx`。
9. 进入 Play Mode 验证商店、部署、阻挡、攻击、二星/三星表现。

### 新增一个高台远程棋子

1. 在 `Pieces.lua.txt` 新增棋子。
2. 设置 `deployable_terrains = { HighGround = true }`。
3. 通常设置 `max_block_count = 0`。
4. 设置较远的 `attack_range`。
5. prefab 上绑定 `BoardUnitSocket.FirePoint`。
6. 在 `DefaultUnitVisualCatalog.asset` 绑定棋子 prefab。
7. 在 `DefaultProjectileCatalog.asset` 绑定投射物 prefab。
8. 如果需要单独微调星级光效，在 prefab 上挂 `BoardPieceLevelVfx`。
9. 进入 Play Mode 验证攻击范围、动画、投射物、二星/三星表现。

### 新增一个敌人并放入波次

1. 在 `Enemies.lua.txt` 新增敌人。
2. 做敌人 prefab，绑定 `HitPoint`。
3. 在 `DefaultUnitVisualCatalog.asset` 的 `Enemy Entries` 绑定 prefab。
4. 在 `Waves.lua.txt` 的某个 preset 中添加：

```lua
{ enemy_id = "NewEnemyId", count = 5 },
```

5. 进入 Play Mode 验证刷怪。

### 新增一个羁绊

1. 在 `Synergies.lua.txt` 的 `definitions` 中新增羁绊。
2. 在 `order` 中加入羁绊 ID。
3. 在棋子的 `synergies` 中引用它。
4. 添加羁绊图标。
5. 部署相关棋子验证羁绊进度。

### 调整商店概率

1. 打开 `Shop.lua.txt`。
2. 找到目标商店等级。
3. 修改 `rarity_weights` 的 6 个数字。
4. 确保总和为 100。
5. 进入 Play Mode 多刷新几次观察结果。

### 调整某一波敌人

1. 打开 `Waves.lua.txt`。
2. 在 `presets` 中找到目标预设。
3. 修改 `count`、`start_seconds`、`interval_seconds` 或敌人队列。
4. 确认 `wave_pools` 中对应波次使用了该预设。
5. 进入 Play Mode 验证该波。

## 13. 修改后的检查清单

每次改完数据，建议按这个顺序检查：

1. 保存文件。
2. 回到 Unity，等待编译或资源导入完成。
3. 看 Console 是否有红色错误。
4. 如果改了商店：进入备战阶段，刷新商店检查图片、职业图标和价格。
5. 如果改了棋子：购买、部署、查看信息面板、拖动、确认朝向、进入战斗。
6. 如果改了敌人/波次：进入对应波次，看刷怪点、数量、路线和血量。
7. 如果改了羁绊：部署相关棋子，看羁绊条是否显示进度和激活状态。
8. 如果改了 prefab/catalog：确认场上不是临时方块，占位资源没有丢失。

## 14. 不建议策划直接修改的内容

以下内容可以改，但更容易牵动程序逻辑。修改前建议先和程序确认：

- `Board.lua.txt` 中的路线、出生点、终点。
- `terrain_definitions` 和 `zone_definitions`。
- `Shop.rarity_count`。
- `NetworkProtocol`、C# 脚本、测试文件。
- Lua `Match` 目录下的逻辑文件。

如果只是做数值、角色、敌人、羁绊、波次内容，通常只需要改 `Lua/Config` 和 Resources 下的 catalog/图片/prefab。

## 15. 羁绊层数与棋子特性

当前羁绊有两套概念：

- 激活等级：由战场上不同棋子数量决定，例如凑够 2 个不同的【黄金】棋子后激活黄金羁绊。
- 层数：由棋子特性、完美作战、击杀、花费金币等规则增加，用来决定羁绊效果强度。

层数不会自动激活羁绊。比如【学院】有 60 层但场上没有凑够学院激活条件时，不会产生购买折扣。

### 在棋子上配置特性

棋子特性写在 `Assets/Game/Lua/Config/Pieces.lua.txt` 的棋子配置里，字段名是 `traits`：

```lua
gangzi = {
    display_name = "刚子",
    synergies = {
        "Lord",
        "College",
        "Golden",
    },
    traits = {
        {
            type = "OnPreparationEndGoldSpentAddActiveSynergyLayers",
            every_gold = 10,
            amount = 10,
            synergy_ids = {
                "College",
                "Golden",
            },
            requires_location = "Board",
        },
    },
}
```

常用特性类型：

| `type` | 作用 |
| --- | --- |
| `OnGrantAddOwnSynergyLayers` | 获得该棋子时，使自身拥有的所有羁绊层数增加 `amount` |
| `OnGrantAddSynergyLayers` | 获得该棋子时，使指定 `synergy_id` 层数增加 `amount` |
| `OnPerfectBattleAddSynergyLayers` | 本回合最终无漏怪时，使指定 `synergy_id` 层数增加 `amount` |
| `OnKillAddActiveSynergyLayers` | 该棋子击杀敌人时，若指定羁绊已激活，使该羁绊层数增加 `amount` |
| `OnGoldSpentAddActiveSynergyLayers` | 每花费 `every_gold` 金币，若指定羁绊已激活，使这些羁绊层数增加 `amount` |
| `OnPreparationEndGoldSpentAddActiveSynergyLayers` | 准备回合结束时，按本准备回合总花费结算，每满 `every_gold` 金币给指定已激活羁绊增加 `amount` 层 |
| `SelfAttackPercentFromActiveSynergyLayers` | 根据指定已激活羁绊的层数，提高自身攻击力百分比 |
| `SelfAttackAndHealthPercentFromActiveSynergyLayers` | 根据指定已激活羁绊的层数，提高自身攻击力和生命值百分比 |

`requires_location = "Board"` 表示只有该棋子在战场上时才触发。备战区里的棋子不会触发这类特性。

【君王】是特殊羁绊，不按层数成长。像 `gangzi` 这类“花费金币增加层数”的特性不要把 `Lord` 放进 `synergy_ids`。

### 在羁绊上配置层数效果

羁绊层数效果写在 `Assets/Game/Lua/Config/Synergies.lua.txt` 的 `layer_effect` 中：

```lua
Golden = {
    display_name = "黄金",
    layer_effect = {
        type = "AttackPercentForSynergy",
        base_percent = 10,
        layers_per_percent = 2,
        percent_per_step = 1,
    },
    levels = {
        { required_unique_pieces = 2, damage_bonus = 0 },
    },
},
```

攻击力/最大生命值百分比类羁绊当前统一使用这个成长公式：

```text
最终百分比 = base_percent + floor(层数 / layers_per_percent) * percent_per_step
```

默认设计是：0 层时为 10%，之后每 2 层增加 1%。例如 0/1 层是 10%，2/3 层是 11%，20 层是 20%。

当前支持的层数效果：

| `layer_effect.type` | 作用 |
| --- | --- |
| `AttackPercentForSynergy` | 按层数提高拥有该羁绊的战场棋子攻击力 |
| `HealthPercentForAll` | 按层数提高战场全体棋子生命值 |
| `AttackAndHealthPercentForSynergy` | 按层数提高拥有该羁绊的战场棋子攻击力和生命值 |
| `AttackAndHealthPercentForAll` | 固定提高战场全体棋子攻击力和生命值，当前用于【君王】 |
| `ShopPurchaseDiscount` | 已激活时按层数降低商店购买价格，当前用于【学院】 |
| `StormWhirlwind` | 【风暴】角色攻击成功发出时，按层数概率向正面发射旋风，对沿途敌人造成伤害 |
| `MagicSword` | 【魔力】角色每隔一段时间生成辉剑，对最近敌人造成伤害 |

【学院】折扣规则示例：

```lua
layer_effect = {
    type = "ShopPurchaseDiscount",
    thresholds = {
        { layers_above = 50, discount = 1 },
        { layers_above = 100, discount = 2 },
    },
},
```

含义是：学院已激活且层数大于 50 时购买棋子费用 -1；层数大于 100 时费用 -2。最终购买价格最低为 0。

风暴和魔力会额外产生 `EnemyDamageRequested` 事件。为了让普通攻击投射物和羁绊投射物分开配置，羁绊事件会带独立的 `projectile_id`：

| 羁绊 | `DefaultProjectileCatalog.asset` 中使用的 ID |
| --- | --- |
| 风暴 | `Storm` |
| 魔力 | `MagicPower` |

也就是说，普通远程棋子仍然用棋子的 `piece_id` 绑定投射物；风暴旋风、魔力辉剑则分别用 `Storm` 和 `MagicPower` 绑定投射物 prefab。当前魔力辉剑使用 `MagicSword_0_Projectile`，不要误绑到普通法球 `Magic_0_Projectile`。

### 棋子特性描述文案

棋子信息面板里的“特性描述”来自 `Assets/Game/Lua/Config/Pieces.lua.txt` 中的 `feature_description`：

```lua
feature_description = "获得时，自身所属羁绊层数 +2。",
```

这段文字只负责 UI 展示，不会自动改变规则。真正的规则仍然由同一个棋子配置中的 `traits` 决定。

商店中首次点击商品时，也会用同一套 `feature_description` 和一级基础属性预览该棋子；再次点击同一商品才会购买。
