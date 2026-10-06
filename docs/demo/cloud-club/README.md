# 云朵俱乐部 · Preview 24

用户已确认短袖上衣＋膝上短裤并授权扩展全部角色。运动服为原生第四套服装，覆盖 DeepSeek、GPT、Claude、Gemini、Grok、Qwen、GLM、Kimi 的 Q版和3D真人，共16套；加上原装、泳装、婚纱，共64套外观。

## 入口与更新

- 运行页：`D:\VibeCoding\Character\Release\win-x64\Demo\CloudClub\index.html`。
- 桌面保留唯一 `DeepSeek-動作Demo.lnk`；程序、画稿、HTML和帧图均在D盘。
- 本页运动服区域可切换八位角色，并列查看Q版和3D真人的实际安装画稿。
- 「查看动作」进入 `../DeepSeek-demo.html?outfit=sports`，长期动作页展示DeepSeek的两种风格、四套服装。全八位角色可在桌面程序切换；不把静态衣柜当作全动作播放。
- TaskbarLift页面也支持这八套DeepSeek外观，普通松手停住，Shift松手下落，落地后散步。

刷新原生帧后安装：`powershell -File tools/install-cloud-club-demo.ps1`。安装脚本生成当前素材索引，不依赖C盘缓存。源码的 `sports-roster.js` 由 `tools/build-sports-roster.py` 生成，浏览器直接取安装图集的裁框，不复制16份立绘。

## 已确认互动

| 互动 | 人物与道具 |
| --- | --- |
| 梳头 | 真实手握梳子，举到发侧、梳理、收手；使用对应服装的专用画稿 |
| 擦脸 | 双手拿毛巾，送至面颊、轻擦、放下；不再在待机身体上叠加漂浮毛巾 |
| 伸懒腰 | 抬臂、伸展、回落，保持同一身体比例 |
| 吹泡泡 | 取棒、送嘴、吹气、看泡泡、收手；只有吹气阶段从图中环口产生泡泡 |
| 数星星 | 抬头、伸手指点、依次计数1→5，星星逐个高亮 |
| 捉蝴蝶 | 走近引导点、停下、蝴蝶落到掌心 |

人物使用原始画稿与连续姿势过渡。举手、坐下、弯膝时采用固定身体平面，不能按每帧外轮廓拉伸人物。吃饭、积木、梳头、擦脸、泡泡棒、轻敲优先使用画在手中的道具，避免重复道具。行走距离由当前服装的步幅和周期计算。

本页六套旧服装的梳头、擦脸取原生导出的连续帧；伸展、泡泡、数星星使用已批准的专用姿势与光流。全套运动服动作在长期原生动作页。两者的素材来源和适用范围分别展示，不用待机人物替代新增手部动作。

## 会员与菜单

测试阶段访客及五档会员均可体验聊天与适用互动，实际账号等级不变。注册为本机账号，尚未接入账号服务器或收费授权。

| 等级 | 配色 |
| --- | --- |
| 黑金 | 曜石黑＋香槟金 |
| 白金 | 已确认的浅玉金属绿 `#E0EFE7 / #93B6A6 / #587E6C` |
| 黄金 | 柔光香槟金 |
| 白银 | 月光冷银 |
| 黄铜 | 暖砂黄铜 |

圆盘背景透明，柔和线形图标；一级入口为「互动」，聊天单列。右键仅开关菜单，动作时间和移动继续；躲藏合并为一个入口并随机选择左右边缘，左键触发找到。聊天输入在角色上方，默认回复和API回复前均保留至少1秒思考。

## 素材与验证

画稿使用内置image_gen。运动服来源、提示词、服装规范和校准在 `artwork/sports-shorts/`，旧服装梳头擦脸在 `artwork/care-contact/`。每张选用PNG均在D盘；原始像素不经Python编辑，工具只读取并生成测量元数据。

验证命令支持暂存发布目录，先检查再替换运行版：

- `python tools/install-sports-shorts.py --check`
- `node tools/verify-cloud-club.cjs <CloudClub/index.html>`
- `node tools/verify-club-export.cjs <DeepSeek-demo.html>`
- `node tools/verify-taskbar-lift-demo.cjs <Demo目录>`
- 原生 `--verify-ui --verify-sports --verify-club --verify-scale --verify-floor-contact --data-dir <独立D盘目录>`。

当前验收证据与范围以 `docs/verification-preview22.md` 为准。历史浏览器记录保留在 `.artifacts/cloud-club-review*`，不拿旧版48套外观结果替代本次64套检查。
