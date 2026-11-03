# CapooDesk 文档

CapooDesk（咖波桌宠）是仿照官方 Capoo Pals 做的桌面宠物。和官方版的区别在于偏陪伴而不是偏游戏：没有扭蛋、合成解锁、成就和账号，126 只咖波全部直接可以召唤。

| 文档 | 给谁看 | 内容 |
|---|---|---|
| [用户手册](用户手册.md) | 使用者 | 安装运行、怎么和咖波玩、魔法书、托盘菜单、全部设置、常见问题 |
| [架构](架构.md) | 开发者 | 模块划分、窗口与点击穿透、物理、状态机、Spine 渲染、点击判定、魔法书、存档 |
| [开发指南](开发指南.md) | 开发者 | 环境、构建运行、导出、加素材/角色/动作、重新生成点击形状、代码约定、已知坑 |
| [测试](测试.md) | 开发者 | 自动化自测的各个模式、测试隔离、覆盖范围、需要人工确认的部分 |
| [更新记录](更新记录.md) | 所有人 | 每一轮改了什么 |
| [问题清单 2026-10-28](issues-2026-10-28.md) | 开发者 | 第一次试用后的检查记录、修复与复查 |

同样的内容也有一份在线版（带模块结构图和性能图）：https://claude.ai/artifact/MscXQ6eoDGJVYKBWArrAMQ

## 一句话了解项目

- 引擎：带 spine-godot 4.1 模块的定制 Godot 4.2.1（mono / C#）。素材是 Spine 4.1 格式，没有源工程，引擎不能升级。详见[开发指南](开发指南.md#为什么不能升级引擎)。
- 窗口：一个铺满工作区的透明覆盖窗口。鼠标不在咖波或界面上时，点击直接穿透到桌面。
- 咖波：每只由一个 Spine 身体加最多 16 个部件层拼成，带物理（站在任务栏和窗口顶上）和一个状态机。
- 素材（Spine 动画、界面图片、音效）来自官方 CapooPals，仅供个人学习使用。

## 目录结构

```
CapooDesk/
  scenes/main.tscn        主场景
  scripts/                全部 C# 代码（见 架构.md）
  assets/                 官方素材在 assets 分支，克隆后运行 sh tools/fetch_assets.sh 取回
    json/                 角色表、动作表、参数表（main 只跟踪自己的 Capoo_Action_Extra.json）
    spine/                Spine 骨架（.spine-json + .atlas + .png + 生成的 .res）
    images/               界面图片（来自原版）
    sounds/               音效（来自原版）
    icons/capoo.ico       程序、窗口与托盘图标（16–256 多尺寸，tools/make_ico.py 生成）
    data/hit_shapes.bin   点击形状（由 tools/gen_hit_shapes.py 生成）
  tools/gen_hit_shapes.py 生成点击形状的脚本
  tools/fetch_assets.sh   从 assets 分支取回素材
  tools/make_ico.py       生成多尺寸程序图标
  docs/                   本文档
  README.md               简要说明
  .claude/skills/         给 Claude 的专题笔记（Spine、物理、行为、测试、C# 约定）
```
