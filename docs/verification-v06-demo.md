# v0.6：站坐待机与多人合照 Demo

日期：2026-10-09。入口为 `Release/win-x64/Demo/CompanionV01/index.html?review=v06`，桌面原有 DeepSeek Demo 的 CloudClub 页也可进入。

本轮只修改 HTML Demo，等待用户审阅。`Version.props` 推进为 0.6；已运行的原生程序仍为 v0.5，没有制作正式安装包，Gitee 保持暂停。

## 改动

- 解决角色卡统一读取坐姿首帧的问题。默认站坐交错，增加每角色独立保存的随心、站立、坐下模式；随心约每 12–18 秒切换。暂停、减少动态、页面隐藏和模态框打开时停止自动切换，当前互动不会被待机覆盖。
- 覆盖 8 位伙伴、Q版/3D真人、四套服装共 64 套外观。保留原有位图；Q版优先使用正面中立姿态，原中立图仍为坐姿的外观使用侧面近足支撑帧，保持静止。真人站姿沿用立绘，坐姿采用清醒抱膝帧。站坐共用显示空间与地面基线，不对身体局部拉伸。
- 修正旧图集坐姿与真人合照的相邻发丝残片：只输出透明像素的所属角色裁切元数据，不覆盖 PNG/WebP。
- 合照改为 2–8 位勾选，当前伙伴固定入镜，可全选或清空其他。保留当前画风与每位各自的服装。2 人 1200×1400，3–4 人 1800×1350，5–8 人分两排 1800×1760。
- 所有人物共同等比缩放，根据发丝、尾巴及裙摆的可见边界分配位置。最多 60 字，暖云/浅玉/奶油相框。更换内容后禁止保存旧图，过期异步加载不会覆盖新选择，空格勾选保留键盘焦点。
- 浏览器保存选择器默认桌面；不支持时真实下载 PNG 并说明结果。没有声称网页能绕过浏览器权限自动写入桌面。

## 复现

```powershell
./tools/install-companion-demo.ps1
node tools/verify-companion-posture-photo.cjs
$env:COMPANION_REVIEW_DIRECTORY = '.artifacts/companion-v06-calendar'
node tools/verify-companion-calendar.cjs
```

测试使用隔离 Chromium 上下文。不会授予日常账号测试权限或改写用户的原生存档。Demo 的素材编码与截图存放在项目内的生成目录，不提交重复的大型编码文件。

## 验证结果

- `verify-companion-posture-photo.cjs` 最终通过 5 组：64 套外观的两种姿态、自动/手动模式及刷新保留、两种风格与四套服装下的 56 种 2–8 人布局、八角色混搭服装、键盘选择、过期内容禁存、桌面选择器参数及实际 PNG 下载、手机布局。
- `verify-companion-calendar.cjs` 通过 11 组：月/周历、补签与旧存档、八角色说明、原图服装卡及窄屏；预期图片改为当前选定姿态。
- 目视查看四套服装的 128 张站坐截图总览、八套八人合照、混搭合照及手机界面。移除了旧真人合照中的邻格发丝，Q版迈步展开帧改用更靠拢的支撑姿势；部分 Q版服装使用侧面站姿。站坐为静态状态切换，不冒称新增了起身或落座的连续动画。
- 最终报告和截图：`.artifacts/companion-v06-review/posture-group-report.json`、`gallery-*.png`、`group-*.png`；月周历报告 `.artifacts/companion-v06-calendar/calendar-report.json`。
- 保存选择器用内存替身验证 `startIn: desktop`，同时实际下载并读取 PNG；没有静默写入用户桌面。脚本语法及 `git diff --check` 通过。没有重跑与本次无关的原生动作矩阵或真实模型网络连通测试。

## 原生边界

本轮没有修改或重建桌面程序。v0.5 产品版本及 SHA256 保持：

`5B462FA6F5A13F54431E241A29B5007B37C7490D55043DB2531D265068C289C8`

运行路径继续为 `D:\VibeCoding\Character\Release\win-x64\DesktopPet.exe`，没有移动用户存档、重设角色或创建新的桌面项目目录。本轮网页结果不能代替原生姿态/合照接入验收，也不代表用户已批准新 Demo。
