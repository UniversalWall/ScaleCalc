# ScaleCalc · 缩放模式换算与选型台

做 Unity UI 的时候，设计稿在 1920×1080 上看着挺好，换台手机就发现两边的按钮被切掉了，或者界面被拉得很开、边上空出一大片。这些毛病多半出在 UGUI 的 `CanvasScaler` 上，而引擎从来不告诉你它到底算了什么。

ScaleCalc 就是回答这个问题的。它把 `CanvasScaler` 的换算逻辑用纯 C# 重写了一遍，拿真实 Canvas 的读数去核对，再把你打算支持的每种屏幕列出来，告诉你会裁掉多少、会留白多少。算到最后它会给出一个可以直接用的数——**安全设计区**，也就是画面正中一块所有设备都看得见的区域，重要的东西放进去就不会被裁。

它给数字，不给布局方案：元素该锚在哪里不归它管。

> **English** — ScaleCalc re-implements the UGUI `CanvasScaler` math as pure functions, checks it against the
> values Unity actually writes, and reports how much each match mode crops or pads across the screen sizes you
> care about — including the safe design area that stays visible on every device.
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
