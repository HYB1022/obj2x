<p align="center">
  <img src="docs/images/logo.png" width="112" alt="Bus Depot 标志">
</p>

<h1 align="center">Bus Depot</h1>

<p align="center">
  按文件夹批量完成从 Blender 导出的 <b>obj → x → o3d</b> 转换，<br>还能制作 OMSI 物件和样条线、查看和编辑巴士（.bus）、检查地图的 OMSI 2 工具
</p>

<p align="center">
  <a href="https://github.com/HYB1022/obj2x/releases/latest"><img src="https://img.shields.io/github/v/release/HYB1022/obj2x?style=flat-square&label=%E6%9C%80%E6%96%B0%E7%89%88%E6%9C%AC&color=5046e5" alt="最新版本"></a>
  <a href="https://github.com/HYB1022/obj2x/releases"><img src="https://img.shields.io/github/downloads/HYB1022/obj2x/total?style=flat-square&label=%E4%B8%8B%E8%BD%BD%E9%87%8F&color=0a84ff" alt="下载量"></a>
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078d4?style=flat-square" alt="Windows 10 | 11">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-GPL--3.0-34c759?style=flat-square" alt="GPL-3.0"></a>
</p>

<p align="center">
  <a href="https://github.com/HYB1022/obj2x/releases/latest/download/BusDepot.zip"><b>⬇ 下载 BusDepot.zip</b></a>
  &nbsp;·&nbsp;
  <a href="#使用方法">使用方法</a>
  &nbsp;·&nbsp;
  <a href="#常见问题">常见问题</a>
</p>

<p align="center">
  <a href="README.md">한국어</a> · <a href="README.en.md">English</a> · <a href="README.de.md">Deutsch</a> · <a href="README.ja.md">日本語</a> · <b>中文</b>
</p>

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/images/zh/welcome-dark.webp">
    <img src="docs/images/zh/welcome-light.webp" width="860" alt="首次启动的欢迎界面：流动的渐变背景上轮流出现多种语言的问候语">
  </picture>
</p>

> [!NOTE]
> **从 v2.7.3 起，名称由 obj2x 改为 Bus Depot。** 程序为 `BusDepot.exe`，设置文件为 `BusDepot.ini`。旧设置（`obj2x.ini`）会在首次启动时自动迁移，旧版本的自动更新也照常可用。残留的旧文件会被清理，只保留 `obj2x.exe` 启动器，以便旧的快捷方式继续可用。

以前要一个个打开文件、设置选项、点击 Convert 的工作，现在**只需指定文件夹**即可自动完成。

- **批量转换**：用 Blender 2.79b 把文件夹中的 `.obj` 转成 `.x`，再用 OmsiXConv 转成 `.o3d`。OmsiXConv 的复选框（`X-File: z up` 等）会自动设置成你预先选好的状态。
- **并行处理**：同时启动多个 OmsiXConv，并把它们的窗口移到屏幕外，只显示本工具的窗口。
- **快速转换（实验性）**：不需要 Blender 和 OmsiXConv，直接从 obj 生成 `.o3d`，每个文件约 0.1 秒。
- **结果表**：一目了然地显示每个文件的顶点、三角形、材质数、耗时和状态（OK / 注意 / 失败）。
- **OMSI 制作**：用 o3d 模型制作场景物件（`.sco`），用车道和人行道参数制作道路样条线（`.sli`），直接放进 OMSI 文件夹。
- **巴士**：打开 `.bus`，连同涂装以 3D 显示模型，播放动画（`[newanim]`）并修改枢轴、旋转和 factor，编辑乘客路径（`paths.cfg`）和座位（`passengercabin.cfg`）并保存。也可以保存为 Blender 文件（`.blend`）或 OBJ。
- **地图**：显示地图信息以及地块、样条线、物件的分布图，并找出缺失的物件、样条线、AI 车辆和人物。也可以查看 HOF 文件。

`.o3d` 会生成在与 `.x` 相同的文件夹中，文件名与 obj 相同（`face.obj` → `face.x` → `face.o3d`）。

## 界面

<table>
  <tr>
    <td width="50%"><img src="docs/images/zh/dashboard-light.png" alt="仪表板：用快速转换转换 9 个文件后的结果表"></td>
    <td width="50%"><img src="docs/images/zh/wizard-light.png" alt="转换向导：选择转换方式"></td>
  </tr>
  <tr>
    <td align="center"><b>仪表板</b><br><sub>用结果表显示每个文件的顶点、三角形、材质数和状态</sub></td>
    <td align="center"><b>转换向导</b><br><sub>方式 → 路径 → 选项 → 完成，四步开始转换</sub></td>
  </tr>
  <tr>
    <td width="50%"><img src="docs/images/zh/about-light.png" alt="关于页：新版本更新提示"></td>
    <td width="50%"><img src="docs/images/zh/about-dark.png" alt="深色主题的关于页"></td>
  </tr>
  <tr>
    <td align="center"><b>更新提示</b><br><sub>有新版本时会显示在 <code>关于</code> 菜单中，一键安装</sub></td>
    <td align="center"><b>深色主题</b><br><sub>在 <code>设置</code> 中可选择主题（系统 / 浅色 / 深色）、强调色（靛蓝 / 青绿 / 玫红）、语言（한국어 / English / Deutsch / 日本語 / 中文）和字体</sub></td>
  </tr>
</table>

## 所需环境

| 所需 | 说明 |
| --- | --- |
| Windows 10 / 11 | 运行所需的 .NET Framework 4.x 已内置于 Windows |
| [Blender 2.79b](https://download.blender.org/release/Blender2.79/) | 需要启用自带的 `Import-Export: DirectX X Format` 插件。其他版本（2.8 及以上）不支持用于转换 |
| OmsiXConv (Omsi X to O3D Converter 1.0) | **本仓库不包含。** 请另行获取并指定 `OmsiXConv.exe` 的位置 |

建模用的 Blender 版本不限，只有转换需要 2.79b。如果只用快速转换，则 Blender 和 OmsiXConv 都不需要。

## 使用方法

1. 从 [Releases](https://github.com/HYB1022/obj2x/releases/latest) 下载最新的 `BusDepot.zip`，解压到**可写入的文件夹**。设置保存在同一文件夹的 `BusDepot.ini` 中。
2. 运行 `BusDepot.exe`（或 `BusDepot.bat`）。首次启动需要同意免责声明。
3. 在 `高级` 页中指定 Blender 2.79 的 `blender.exe` 和 `OmsiXConv.exe` 的位置，指定一次即会保存。
4. 在 `转换向导` 中选择转换方式，指定 OBJ 文件夹（输入）和输出文件夹，然后开始转换。把 obj 文件夹（或 obj 文件）拖到窗口或 `BusDepot.exe` 图标上，向导会直接以该文件夹打开；也可以点击最近使用的文件夹来选择。
5. 进度显示在 `仪表板` 的进度条、`日志` / `结果表` 页以及任务栏的 Bus Depot 按钮上。如果转换在你使用其他窗口时完成，任务栏按钮会闪烁。

| 转换方式 | 作用 |
| --- | --- |
| **DirectX → O3D**（推荐） | 标准方式：用 Blender 2.79b 生成 `.x`，再用 OmsiXConv 转为 `.o3d` |
| **仅 Blender .x** | 不使用 OmsiXConv，只生成 `.x` 文件 |
| **FastO3D**（实验性） | 用内置转换器直接从 obj 生成 `.o3d`。请先阅读下方的[快速转换](#快速转换实验性功能) |

> [!NOTE]
> OmsiXConv 转换期间，看不见的 OmsiXConv 窗口正在接收输入，请尽量不要操作鼠标和键盘。

## 功能

| 功能 | 说明 |
| --- | --- |
| 包含子文件夹 | 连同输入文件夹下的子文件夹一起查找并转换 obj。输出文件夹中也会按相同的子文件夹结构生成 `.x`/`.o3d` |
| 仅转换修改过的 obj | 只转换比 `.o3d` 更新的 obj（只生成 `.x` 时与 `.x` 比较）。建模时反复导出很方便 |
| 停止 | 关闭 Blender 和所有 OmsiXConv，并清空队列 |
| 仅重试失败项 | 失败的文件会在结束后显示在 `结果表` 中，`仅重试失败项` 只重新处理这些文件。已生成 `.x` 的文件从 O3D 步骤开始，在 Blender 或快速转换中失败的文件从头开始 |
| 缺失贴图警告 | 转换前检查 `.mtl` 中写的贴图是否存在，缺失时列出并询问是否继续 |
| 并行数量 | 设置同时启动多少个 OmsiXConv。默认是逻辑核心数 − 1。OmsiXConv 只用一个核心，超过核心数反而会变慢 |
| 检查更新 | 启动时在 GitHub 上检查最新版本，有新版本时在 `关于` 菜单上显示 `更新` 标记 |

### 高级 / 设置页

- **OmsiXConv 详细选项**（`高级`）：`z up`、`Separate Files`、`Show Details of X`、`Remove Specular`、`Opt. Materials`、`Opt. Vertices`、`All Normals Up`、`Use Submesh Local Pos. as Global Pos.` 每次转换都会自动设为这里的状态。默认为 `z up` 关闭，`Remove Specular` / `Opt. Materials` / `Opt. Vertices` 开启。
- **Blender .x 导出附加选项**（`高级`）：每行写一个导出插件的 `名称=值`（例如 `ApplyModifiers=True`）。只有日志中 `x exporter options:` 列出的名称会生效，未知名称显示为 `ignored options`。
- **成功后删除 .x**（`设置`）：O3D 转换成功后删除中间文件 `.x`。
- **字体**（`设置`）：可选 NanumSquare Neo（默认）、Noto Sans KR、MiSans 或 Windows 系统字体。NanumSquare Neo 和 Noto Sans KR 在 `fonts` 文件夹中。MiSans 的许可不允许随程序分发，请从[小米 HyperOS 字体页面](https://hyperos.mi.com/font)下载后安装到 Windows 或放进 `fonts` 文件夹。没有时以 NanumSquare Neo 显示。
- **语言文件**（`设置`）：内置 한국어、English、Deutsch、日本語 和 中文。点击 `语言文件` 按钮会在 exe 旁创建 `lang` 文件夹和翻译模板（`_template.json`）。把模板复制为 `<语言代码>.json`（如 `fr.json`）并翻译，它就会出现在语言列表中。用内置语言的文件名保存，则只修改该语言的部分文字。

## OMSI 制作：物件 / 样条线 / 巴士 / 地图

OMSI 2 文件夹会根据 Steam 安装信息自动查找。找不到时，请点击各页面的 `OMSI 2 文件夹` 一行，选择包含 `Omsi.exe` 的文件夹。

- **制作物件**：选择存放 o3d 模型的文件夹（也可以拖到窗口上）即显示模型列表。点击制作后，会在 `Sceneryobjects\<文件夹名>` 中生成 `model\`、`texture\` 和 `.sco`，并出现在地图编辑器的物件列表中。
  - 可以每个模型做一个物件，也可以全部合成一个物件。
  - 可开关碰撞、可在上面行走（桥、站台）、阴影和固定。
  - 带 Alpha 通道的贴图（tga/dds/png）会自动设为透明材质（`[matl_alpha]`），可选镂空（树叶、铁丝网）或半透明（玻璃）。
  - 贴图会在模型文件夹、其旁边以及 `texture` 文件夹中查找并复制。找不到的贴图会在最后列出。
  - **打开 .sco 查看**：选择现有物件的 `.sco`（也可以拖到窗口上），即以带贴图的 3D 显示。要逐个开关部件，请点击 `在巴士页详细查看`。
- **制作样条线**：设定车道数、车道宽度、双向/单向、人行道宽度和路缘高度，预览会立即变化。点击制作后会在 `Splines\<文件夹名>` 中生成 `.sli` 和 `texture\`。
  - 包含车辆路径（每条车道，右侧通行）和行人路径（每条人行道）。双向且车道数为奇数时，可选择多出的一条车道放在左侧（反方向）还是右侧（行驶方向）。
  - 没有选择路面、人行道贴图时，会生成默认的沥青和铺装砖贴图。路面贴图可以在整条道路上铺一次，或每条车道铺一次。
  - **打开 .sli 查看**：选择现有样条线的 `.sli`（也可以拖到窗口上），即作为 40 m 长的直路以带贴图的 3D 显示。车辆路径（正向绿色、反向橙色）和行人路径（蓝色）以箭头叠加显示，并给出宽度、断面、贴图和路径数量。
- **巴士**：选择 `.bus`（或 `model.cfg`、物件 `.sco`）或拖到窗口上，即以带贴图的 3D 显示。拖动旋转，右键拖动平移，滚轮缩放。
  - **动画**：cfg 中 `anim_rot` / `anim_trans` 用到的每个变量都有滑块和 `播放` 按钮，车门、雨刷、仪表指针等会像在 OMSI 中一样运动（枢轴 `origin_trans`、旋转轴 `origin_rot`、`origin_from_mesh`、多个块叠加）。如果与游戏不同，可以切换块的应用顺序和旋转方向。
  - **部件编辑**：在 3D 中点击部件或点击列表中的 `编辑`，每个块都会出现枢轴、旋转和 factor 输入框，修改后立即生效，枢轴和旋转轴也会显示在 3D 中。`保存 cfg` 只改写写有这些值的行，注释和其他内容保持不变，首次保存时会把原文件保留为 `.bak`。
  - **乘客路径 / 座位**：把 `.bus` 中指定的 `paths.cfg` 的点（入口橙色、出口红色）和连线，以及 `passengercabin.cfg` 的座位（司机座粉色，短棒表示朝向）叠加显示在模型上，可以修改坐标、增删点、连线和座位并保存。在 3D 中拖动点或座位即可直接移动（像 Blender 一样平行于屏幕自由移动，按住 `Shift` 以 1 m 为单位）。拖动所选点或座位上的红/绿/蓝箭头只沿 x/y/z 轴移动。打开 `隐藏模型` 可关闭整个模型，只看路径和座位。`Ctrl+Z` 撤销，`Ctrl+Y`（或 `Ctrl+Shift+Z`）重做。
  - 起初只打开最精细 LOD 中从外面能看到的部件。条件部件（`[visible]`）可以在部件列表中逐个开关，也可以用 `外部` / `内部` / `全部` 一次切换。
  - **涂装**：从 `[CTC]` 涂装文件夹中选择 `.cti`，即贴上该涂装的贴图，并按 `.cti` 的 `[setvar]` 选项开关条件部件。一个 `.cti` 中有多个涂装时可以分别选择。车辆脚本（`.osc`）根据涂装值决定的选项（如按年款区分的车门样式）也会通过简单地运行脚本来匹配。未设置的变量与 OMSI 一样视为 0。起初选中第一个涂装。透明只用于带 `[matl_alpha]` 的材质（与 OMSI 相同）。
  - 与 OMSI 一样只绘制正面。法线反转的面与游戏中一样不可见，打开 `显示背面` 则双面绘制。
  - 游戏中通过动画移到位的部件（插入式标牌等）在文件中位于车身外，因此起初是关闭的。
  - **保存为 Blender 文件**：用已安装的最新版 Blender（自动在 Program Files 和 Steam 中查找，也可以自己选择）载入已打开的部件，保存为内嵌贴图的 `.blend`。每个部件成为一个物体，面保持 OMSI 所需的三角形。方向与 OMSI 相同（z 向上，车长方向为 y）。也可以不用 Blender，只保存 `.obj` + `.mtl`。
  - 不打开加密的 o3d（如 OMSI 自带车辆）。
- **地图**：选择（或拖入）`maps` 中的地图文件夹（含 `global.cfg` 的文件夹），即显示地图名称、说明、图片、年份、默认车场、线路/班次数，以及地块、样条线（含曲线）和物件的分布图。拖动平移，滚轮缩放。
  - **缺失文件**：汇总 OMSI 文件夹中缺失的物件和样条线（含使用次数）、AI 车辆（`ailists.cfg`）和人物（`humans.txt`、`drivers.txt`），并在地图上标红。`复制列表` 可放入剪贴板。
  - **AI / 人物**：显示每个 AI 组的车辆种类和数量，以及乘客和司机列表。
  - **HOF 查看器**：选择 OMSI 车辆文件夹中的 `.hof`，查看终点站、站点列表和班次（线路、代码、站点顺序）。多辆车中名称和内容完全相同的 hof 只显示一个。也可以把 `.hof` 拖到窗口上。

> [!NOTE]
> 生成的文件只使用 OMSI 的基本格式（不含夜间贴图、LOD、信号灯、路口等）。如需进一步修改，请用记事本打开生成的 `.sco` / `.sli` 编辑。

### 快速转换（实验性功能）

> [!WARNING]
> **这是实验性功能。** 它是通过分析 OmsiXConv 的输出格式做出来的，不能保证所有模型的结果都相同。重要的模型请先与标准方式的结果比较，有问题时改用标准方式。

在与 OmsiXConv 的 `.o3d` 对比验证过的范围内，三角形、材质和 UV 相同（法线差异在 0.001 以内，顶点数可能相差几个）。以下尚不支持或未验证：

- 分成多个物体的 obj：会合并为一个网格，不保留各物体的原点。尚未确认与 OmsiXConv 结果是否一致
- 顶点或三角形超过 65,535 的模型（显示为失败）
- 不应用 OmsiXConv 详细选项（`z up` 等）和 Blender 附加选项

## 更新

启动时如有新版本，侧边栏 `关于` 菜单右侧和 `关于` 页的 `更新` 一行会出现 `更新` 标记。点击 `更新` 会下载发布的 zip，覆盖到同一文件夹并重新启动。

- 设置（`BusDepot.ini`）保持不变。
- 原来的 `BusDepot.exe`、`BusDepot.bat`、`BusDepot.py` 会保存在 `BusDepot_backup` 文件夹中。
- 从 v2.0（PowerShell 版）也能直接更新到最新版本。
- 如果不想启动时连接 GitHub，请在 `设置` 中关闭 `启动时检查更新`。
- 下载的文件不会另行校验完整性。请仅在信任本仓库发布内容时使用。

## 常见问题

| 现象 | 检查事项 |
| --- | --- |
| Windows 阻止运行或发出警告 | `BusDepot.exe` 没有代码签名，SmartScreen 可能会警告。点击 `更多信息` → `仍要运行`，或查看源码（`src/`）后用 `build.bat` 自行构建 |
| 与 `blender.exe` 有关的错误 | 请确认指定的是 2.79b 的 `blender.exe`（不会自动检测） |
| 偶尔出现 `E/A-Fehler 103` | 这是 OmsiXConv 的错误。会自动重试最多 3 次，若仍持续出现，请减少并行数量 |
| 日志显示 `找不到 OmsiXConv 窗口` | 请确认 OmsiXConv 路径正确，且没有其他程序挡住其窗口 |
| 日志显示 `找不到文件选择窗口` | 请把括号中列出的窗口列表通过 Issue 告诉我们 |
| 模型方向不对 | 请在 `高级` 页确认 OmsiXConv 的 `z up` 状态（默认关闭） |

如有问题，请附上日志通过 [Issue](https://github.com/HYB1022/obj2x/issues) 反馈。本工具是在我的环境中制作的，在其他环境中表现可能不同。较大的 `.x`（数 MB 以上）转换可能需要较长时间。

## 自行构建

运行 `build.bat`，会用 Windows 自带的 C# 编译器（.NET Framework 4）把 `src\Core`（转换逻辑）和 `src\App`（WPF 界面）构建为 `BusDepot.exe`。无需另外安装任何东西。

```bat
build.bat
BusDepot.exe --selftest
```

`--selftest` 会自动检查设置、文字、转换器和界面，并把结果以 `PASS` / `FAIL` 行写入同一文件夹的 `BusDepot_selftest.txt`。

## 免责声明

首次启动时会显示免责声明，同意后才能使用。是否同意保存在 `BusDepot.ini` 中，可在 `关于` 页点击 `免责声明` 再次查看。本工具是个人制作的非官方工具，与 Blender、OmsiXConv、OMSI 无关，对转换结果或文件损坏等不作任何保证，也不承担责任。请备份重要文件并自行检查结果。

本仓库不包含 Blender 和 OmsiXConv。各程序遵循各自的许可。

**致谢**：[HYB1022](https://github.com/HYB1022) · [qbasys08](https://github.com/qbasys08) · [Claude (Anthropic)](https://www.anthropic.com/claude)

## 更新日志

- **v2.7.6** — 物件页可**打开 .sco 查看**（带贴图的 3D），样条线页可**打开 .sli 查看**（作为 40 m 直路以 3D 显示，含车辆/行人路径及方向、宽度、断面和贴图信息）。把 `.sco`/`.sli` 拖到窗口上也能打开
- **v2.7.5** — **巴士** 页：打开 `.bus` 查看模型、涂装和动画（`[newanim]`），编辑枢轴、旋转、factor、乘客路径（`paths.cfg`）和座位（`passengercabin.cfg`）并保存（取代模型查看页）。动画变量范围与 OMSI 一致：车门、雨刷为 0~1，只有转向等为 −1~1。**地图** 页：地图信息，地块、样条线和物件分布图，查找缺失的物件、样条线、AI 车辆和人物，AI 列表，HOF 查看器（相同的 hof 合并为一个）。只绘制可见部分，大地图也不再卡住。从 obj2x 升级后清理残留的旧文件，必需程序警告中的 `设置路径` 直接跳到高级页底部，侧边栏标题合为一个，修复圆角卡片悬停时显示方形高亮的问题，README 英语、德语、日语、中文版
- **v2.7.4** — 模型查看中可**选择涂装（.cti）**：贴上涂装的贴图，并按 `[setvar]` 选项开关条件部件（`[visible]`）。与 OMSI 一样**只绘制正面**（法线反转的面不可见，可用 `显示背面` 打开），保存到 Blender 时也为每个材质开启背面剔除。**.blend 用已安装的最新版 Blender 保存**（修复在 Blender 2.8 及以上贴图不显示、界面布局跑到下方的问题，也可自己选择）。样条线：双向奇数车道时可**选择多出的车道放左侧或右侧**。README 顶部动画更清晰
- **v2.7.3** — 更名为 **Bus Depot**（设置、快捷方式和旧版本的自动更新均可延续），新标志。**模型查看**：以带贴图的 3D 查看巴士 model.cfg / 物件 .sco，并保存为 Blender 文件（.blend）或 OBJ（cfg 位于 `Model\cfg\` 之类子文件夹时也能找到部件）。**制作物件**（.sco + 复制模型/贴图），**制作样条线**（车道、人行道 → .sli，带预览）。可把文件夹/文件拖到窗口或 exe 上，任务栏显示进度和完成提示，仅重新转换失败的文件（含 Blender 和快速转换步骤），转换前缺失贴图警告，无功能的行也有按下水波效果
- **v2.7.2** — 修复 Windows Defender 把 v2.7.1 误报为 `Trojan:Win32/Bearfoos.A!ml` 的问题（开发者选项的自检不再把 exe 复制到临时文件夹运行，并为 exe 加入名称、版本和说明信息）
- **v2.7.1** — 关于页的更新按钮移到列表正上方并放大文字，改为像 HyperOS 那样透出背后渐变的半透明面板
- **v2.7** — 有更新时关于页的标题平滑上移，下方出现 `更新` 按钮（类似 HyperOS），高级、设置页改为 HyperOS 设置风格（鼠标悬停时整行高亮），免责声明中加入致谢，修复切换语言后字体下拉框变空、主题/强调色选中标记错位的问题
- **v2.6** — 语言拆分为文件（`lang/*.json`），用户可自行添加语言，新增日语（日本語）和中文，默认字体改为 NanumSquare Neo 并新增字体选择（NanumSquare Neo / Noto Sans KR / 系统），检查更新的结果以 `更新` 标记滑入显示，从按下位置扩散的水波效果，重新设计首次启动的欢迎界面（用所有语言问候），类似小米平板设置应用的左右分栏布局，支持 MiSans（已安装时），以窗口模式启动
- **v2.5** — 新增德语（Deutsch，Windows 为德语时直接以德语启动），动画与屏幕刷新率（60fps）同步更流畅，欢迎界面的问候语移到标志下方居中
- **v2.4** — 用 WPF 重做界面并分离转换逻辑与界面代码，版本/状态显示移到侧边栏底部使右侧占满窗口高度，并行数量数字框（直接输入、滚轮、长按连续），语言下拉框，主题切换动画，关于页 CPU 占用降到约 1/3（使用其他窗口时暂停），修正文字、图标和输入框的垂直对齐
- **v2.3** — 新版本提示由红点改为 `关于` 菜单右侧的 `更新` 标记，许可证改为 GPL-3.0
- **v2.2** — 新版本提示改为在 `关于` 页显示更新行而不是全屏窗口（用 `更新` 按钮直接安装），侧边栏每个菜单加上图标
- **v2.1** — 从 PowerShell 脚本改为可执行文件（`obj2x.exe`，C#）。原有设置（`obj2x.ini`）和免责声明同意状态均可延续，可通过 v2.0 的自动更新安装

<details>
<summary>旧版本（v1.1 ~ v2.0）</summary>

v2.2 及以前版本的可执行文件已不再分发。请通过自动更新获取最新版本。

- **v2.0** — 界面改版（卡片式区域、进度条、浅色/深色主题：在选项窗口中选择），快速转换结果表，首次启动时同意免责声明
- **v1.9** — 包含子文件夹选项，可把文件夹拖到 OBJ 文件夹栏，快速转换日志中显示注意事项（无 mtl / mtl 中没有的材质 / 合并了多个物体）
- **v1.8** — 新增快速转换（内置，实验性）：不用 Blender/OmsiXConv，直接从 obj 生成 `.o3d`
- **v1.7** — 并行数量默认值按电脑核心数设定（以前固定 10 个，在核心少的电脑上反而可能更慢），显示核心数
- **v1.6** — O3D 转换从大文件开始，缩短总时间
- **v1.5** — 显示转换耗时（Blender 步骤、每个文件的准备/转换时间、O3D 步骤总时间）
- **v1.4** — 修复在选项窗口点击 `确定` 时出错、设置无法保存的问题
- **v1.3** — 仅转换修改过的 obj，停止 / 仅重试失败项，选项窗口（全部 OmsiXConv 复选框、删除 .x、导出附加选项），自动安装更新
- **v1.1** — 首次发布：obj → x → o3d 批量转换，自动关闭 `z up`，并行转换，输出与输入文件夹相同的选项，检查更新

</details>

## 许可证

Copyright (C) 2026 Hyunwoo Bang, Junyoung

[GNU General Public License v3.0 或更高版本](LICENSE)（GPL-3.0-or-later）。可以自由使用、修改和再分发，但分发修改版本时必须以相同的 GPL 许可证一并公开源代码。

v2.2 之前的版本以 MIT 许可证发布。
