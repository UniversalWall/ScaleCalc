# Changelog

本文件记录 `com.wayward.scalecalc` 的版本变更。版本号遵循 [SemVer](https://semver.org/lang/zh-CN/)，格式参照 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)。
版本号与 `package.json` 的 `version` 一致，发布以 git tag 标记（本作品：`scalecalc/v<版本>`）。

> **只记发布之后的变更**：发布之前的开发流水不往这里回填（那是开发仓库《文档变更记录》的事）。
> 写法见《包与发布规范》§七：每个版本 5~10 条、每条一行、只写对使用者的影响。

## [Unreleased]

### 文档

- README 的「安装」补一句：**npm 上也有这个包**（`npm i com.wayward.scalecalc`），同时点明 **Unity 装不了 npm 的包**（UPM 读 `package.json` 与 git，不认 npm registry），并说明 npm 那份不含包内测试。
- README 的要求行由"不依赖任何东西"改为"运行期只用 `com.unity.ugui`，不引入第三方依赖"——原文与 `package.json` 里实际声明的 `com.unity.test-framework` 不符。
- README「怎么用」补一张**选型台界面截图**（托管在 GitHub 附件，公开可访问；npm 页面上同样能显示）。

## [0.4.0] - 2026-09-22

首个公开版本（发布基线）。之后的变更记在 `[Unreleased]` 段，发版时归入对应版本。
