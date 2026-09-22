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

- 一份不依赖 Unity 的换算内核，把三种 `ScaleMode` 的换算链实现成纯函数。
- 与引擎对账：`ScaleWithScreenSize` 下的缩放系数、像素比和画布尺寸，与真实 Canvas 的读数逐位一致。
- 一个编辑器窗口，逐档列出裁切量、留白量、实际画布尺寸和差值。
- 跨档结论：安全设计区、边缘被裁掉多少、推荐的 `match` 值。
- 几个导出：换算证据包、当前视图 CSV，以及一份交给美术的设计约束单。

## 二、安装

需要 Unity 6000.3 或更新版本，除了自带的 `com.unity.ugui` 不依赖任何东西。

```jsonc
// Packages/manifest.json
"com.wayward.scalecalc": "https://github.com/UniversalWall/com.wayward.scalecalc.git#v0.4.0"
```

也可以按 `file:` 路径挂本地目录，或者把整个包目录拷进工程的 `Packages/`。改完 manifest 要开一次编辑器才会解析。装好以后在 Package Manager 里点 **Import**，可以拿到两个示例场景。

## 三、怎么用

```
菜单：Wayward/ScaleCalc/打开选型台
```

打开以后先看最上面那行结论，它会把当前勾选的屏幕档位概括成"会不会裁、最坏裁多少、安全区多大"。想细看就往下翻，中间每种屏幕一行读数，旁边那张小图画的就是安全区。

不想开窗口也可以直接调内核：

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

界面上的每个按钮、每列数字怎么读，开发仓库的《ScaleCalc 设计要点》和《实现文档》里有完整说明。

## 四、与我有关

MIT 许可证，见包内的 `LICENSE`，版权行是 `Copyright (c) 2026 UniversalWall`。
