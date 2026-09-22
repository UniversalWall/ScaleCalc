# ScaleCalc · 缩放模式换算与选型台

ScaleCalc 是一个基于纯 C# 实现的 UGUI 多设备适配量化分析工具。

针对 `CanvasScaler` 底层换算逻辑不透明、跨设备易产生 UI 裁切或留白的问题，该工具通过重写核心算法并核对真实 Canvas 读数，提供以下客观数据支持：

- 多设备数据推演：列出目标屏幕的裁切与留白量
- 安全设计区计算：输出画面正中所有设备均可见的绝对安全区域数值
- 明确边界：仅提供基于数据的量化结果，不干涉具体的锚点布局方案

> **English** — ScaleCalc is a pure-C# quantitative analysis tool for UGUI multi-device adaptation.
> Because `CanvasScaler`'s underlying math is opaque, UI gets cropped or padded differently on every device;
> the tool re-implements that math and checks it against readings from a real Canvas, then gives you:
>
> - **Multi-device projection** — how much each target screen crops or pads.
> - **Safe design area** — the absolute area, centered on screen, that stays visible on every device.
> - **A clear boundary** — it reports numbers only; it does not decide your anchors or layout.
>
> Requires Unity 6000.3+. MIT licensed, no third-party dependencies.

## 一、这是什么

ScaleCalc 是一套脱离 Unity 运行时的 `CanvasScaler` 换算与验证工具，核心包含以下三个模块：

- 独立换算内核：以纯函数形式完整实现三种 `ScaleMode` 的换算链，不依赖引擎环境即可独立调用。
- 引擎对账验证：在 `ScaleWithScreenSize` 模式下，其计算的缩放系数、像素比和画布尺寸，与真实 Canvas 的运行时读数逐位一致，确保逻辑可信。
- 编辑器可视化窗口：支持逐档列出各目标分辨率下的裁切量、留白量、实际画布尺寸及差值，并输出跨档结论（安全设计区、边缘裁切量、推荐 `match` 值）。同时提供三类导出：换算证据包、当前视图 CSV，以及面向美术的设计约束单。

## 二、安装

需要 Unity 6000.3 或更新版本，除了自带的 `com.unity.ugui` 不依赖任何东西。

```jsonc
// Packages/manifest.json
"com.wayward.scalecalc": "https://github.com/UniversalWall/com.wayward.scalecalc.git#v0.4.0"
```

## 三、怎么用

在编辑器里打开选型台：

```
菜单：Wayward/ScaleCalc/打开选型台
```

窗口自上而下给出三类信息：

- 结论区：一句话概括当前工作集的参与档数与会裁档数、最坏裁切量，并给出安全设计区尺寸、危险带宽度、三种匹配方式对比与推荐 `match` 值。
- 档位管理表：维护参与计算的屏幕档位，支持勾选、就地改名、增删自定义档，以及档位清单的导入与导出。
- 选型表：逐档列出裁切量、留白量、实际画布尺寸、缩放系数与差值，并附逐档裁留示意图与对账状态列。

不依赖编辑器窗口时，可直接调用内核（纯函数）：

```csharp
using Wayward.ScaleCalc;

var input = ScaleCalcInput.Default;
input.Mode = ScaleMode.ScaleWithScreenSize;
input.ReferenceResolution = new ScaleSize(1920f, 1080f);
input.ScreenSize = new ScaleSize(1440f, 3120f);
input.MatchWidthOrHeight = 0.5f;

ScaleCalcResult r = ScaleCalc.Evaluate(in input);
// r.ScaleFactor / r.ReferencePixelsPerUnit / r.CanvasSize ...
```

界面各按钮与各列数字的读法，见开发仓库的《ScaleCalc 设计要点》与《实现文档》。

## 四、与我有关

MIT 许可证，见包内的 `LICENSE`，版权行是 `Copyright (c) 2026 UniversalWall`。
