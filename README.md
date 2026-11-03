# CapooDesk 素材分支

这个分支只存放 CapooDesk 运行所需的素材，和 `main` 分支的代码分开保存。

| 路径 | 内容 |
|---|---|
| `assets/spine/` | 官方 CapooPals 的 Spine 4.1 骨架，以及 Godot 生成的 `.res` / `.import` |
| `assets/images/` | 界面图片 |
| `assets/sounds/` | 音效 |
| `assets/json/Capoo_Info.json`、`Capoo_Action.json`、`Game_Parameter.json` | 官方角色表、动作表、参数表 |
| `assets/icons/capoo.ico` | 程序图标（用官方图片制作） |
| `assets/data/hit_shapes.bin` | 点击形状（由 `tools/gen_hit_shapes.py` 从上面的骨架生成） |

素材来自官方 CapooPals 桌宠，版权归原权利人所有，仅供个人学习使用，请勿商用或再分发。

## 用法

在 `main` 分支的工作区里运行：

```bash
sh tools/fetch_assets.sh
```

脚本会把这个分支的 `assets/` 解压到工作区。这些路径已经写在 `main` 的 `.gitignore` 里，所以 `git status` 仍然是干净的。之后打开一次 Godot 编辑器，让它导入资源。
