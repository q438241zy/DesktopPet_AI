# 五项互动 · 全角色

2026-10-06：按 I3「同意拓展」授权，覆盖 GPT、Claude、Gemini、Grok、DeepSeek、Qwen、GLM、Kimi；每位角色的 Q版和3D真人各有原装、泳装、婚纱、短袖短裤运动服，共64套外观。选择器读取对应图集，不借用其他形象。

运行页：D:/VibeCoding/Character/Release/win-x64/Demo/InteractionFive/index.html?review=I3。桌面仍只保留同一个 DeepSeek-動作Demo.lnk，由 CloudClub 顶部进入此页。

| 互动 | 操作与回应 |
| --- | --- |
| 击掌 | 拖用户手到掌心，或点掌心、按回车；接近、碰掌、收手后再等待。完成接触才计数。 |
| 猜拳 | 选择后双方摇拳三轮，再一起亮手。宠物独立随机选择，揭晓前不显示。 |
| 拆礼物 | 将礼物递到双手，点盒子拆开；20种物品每轮不重复。原图前壁与手遮挡物品，完成才收藏，取消不获奖励。 |
| 翻书共读 | 三篇原创故事，每篇7–8句；选择、暂停、继续、翻页。读完收藏并记录次数，可从书架重读。 |
| 合照 | 云朵头像、昵称、三种相框；三秒准备后离线保存720×880 PNG。 |

右键只开关菜单，互动继续。HTML 收藏使用专用 localStorage，不写日常宠物存档或账号；原生收藏使用原生用户存档。舞蹈保持删除。

## 图稿与比例

- 3D真人保持已批准成年立绘的头身比、腰线、膝盖与脚踝。20张关键姿势拆成五组四张，部分婚纱使用2×2布局留出裙摆空间。Q版保持各自身份和可爱比例。
- 每组固定像素比例和实测脚底；逐组校准手心、礼盒、书页，不随道具包围盒逐帧缩放，不局部拉伸手腕。
- 运动服是短袖上衣和膝上短裤；矮化的真人20格试产和旧GPT运动服图不选用。
- 内置 imagegen 生成PNG保持原字节；提示词、来源、选图和接触审阅在 artwork/interaction-five。selected-expansion.json 为选用清单，installation.json 为安装报告。
- tools/install-five-art.py 只写裁切、脚底和接触元数据，原生 pet.json 和网页 roster.js 共用这些数据。
- packs/*.js 只包装原PNG的base64，每次加载当前外观，支持 file URL 离线合照。本轮是关键姿势与交互阶段编排，不是骨骼动画或逐帧视频。

## 验证与安装

全64套原生五项互动检查5902项通过，核心56项通过。浏览器完整流程由 tools/verify-interaction-five-demo.cjs 检查，逐套实际掌心、猜拳、礼物、阅读和照片由 tools/verify-five-roster.cjs 检查。

证据：.artifacts/five-native-full、.artifacts/five-roster-browser、.artifacts/interaction-five-review。导出 tools/export-five-roster.py，安装 tools/install-interaction-five-demo.ps1。原生发布前在D盘备份运行包和存档，不用日常账号测试。最终发布状态见 docs/verification-preview25.md，图稿数量不代表已经发布。
