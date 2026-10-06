# Preview 22 · 全角色短袖短裤与真实照顾动作

状态：**已完成本地发布，Preview 22 已启动**（2026-10-03）。原生检查、最终浏览器验收、资源解码与路径审计均通过；原角色、服装、打卡与已有账号文件保持不变。

八位角色的 Q 版和 3D 真人均加入短袖上衣、膝上短裤、短袜和运动鞋，作为第四套服装；共 16 项角色、64 套外观。原装、泳装、婚纱继续保留。

## 本轮修复

- 运动服补齐人物互动、进食、积木、照顾、走路、数星星、吹泡泡和伸展图集；3D 真人另有舞蹈准备图与关节测量。缺少独立动作时保留当前服装的语义备用姿态，避免待机插值覆盖。
- Q 版采用 12 个步行关键姿势，3D 真人采用 8 个；连续姿势过渡与移动速度依据各套服装的步幅、周期计算。
- 吃饭、积木、梳头、擦脸、泡泡棒和轻敲使用画稿内的手与道具，消除重复道具。手掌、泡泡环心及舞蹈关节按实际图像测量。
- 3D 真人轻敲重绘三姿势，保留腿长和抬槌空间；Claude Q 版的接触、玩耍、行走图补齐眼镜。
- 旧三套服装新增真实梳发、擦脸画稿，排除错误镜像头饰帧；Qwen 婚纱与 Kimi 原装的擦脸节奏已修正。
- GPT Q 版泳装照顾姿势恢复裸足；DeepSeek Q 版婚纱梳发消除第三只手；Gemini、Grok 3D 真人旧三套服装重绘独立照顾图，使头身比例与待机一致。
- 修复透明窗口偶发失去连续刷新回调时的停顿：刷新回调仍驱动正常画面；超过 50 ms 未推进时，Dispatcher 定时器补一次更新，并共用上次更新时间，避免重复累计步行距离。GPT 运动服跳跃复测记录到 47 次呈现、约 1405 ms 返回待机（本次最大呈现间隔约 85 ms，不是 60 fps 保证）。

## 已通过的检查

证据路径均相对项目根目录 `D:\VibeCoding\Character`。表中数值为各报告的通过项数；不同检查存在重叠，不相加作为功能数量。

| 检查 | 通过项数 | 证据 |
| --- | ---: | --- |
| 核心行为 | 54 | `tests/DesktopPet.Tests`，本轮测试命令输出 |
| 32 套 3D 真人动作过渡与聊天 | 1,354 | `.artifacts/preview22-final-poses/choreography-check.txt` |
| 32 套 3D 真人喂食、碰球与反弹 | 1,058 | `.artifacts/preview22-final-contacts/contact-check.txt`（其中 866 项在任何截图前完成） |
| UI、角色与四套服装 | 10,600 | `.artifacts/preview22-interface-final/ui-check.txt` |
| 清晰度、裁切与连续呈现 | 3,859 | `.artifacts/preview22-polish/polish-check.txt` |
| Q 版动作完整性 | 7,369 | `.artifacts/preview22-chibi/chibi-check.txt` |
| 提起与摇晃 | 1,056 | `.artifacts/preview22-shake/shake-check.txt` |
| 步态与呈现时序 | 3,847 | `.artifacts/preview22-final-walk/walk-check.txt` |
| 落地后散步 | 960 | `.artifacts/preview22-final-walk/floor-walk-check.txt` |
| 舞蹈 | 14,660 | `.artifacts/preview22-release2-dance/dance-check.txt` |
| 会员 | 156 | `.artifacts/preview22-native/membership-check.txt` |
| 16 套运动服 | 8,576 | `.artifacts/preview22-native/sports-check.txt` |
| 64 套外观新增互动 | 2,643 | `.artifacts/preview22-native/club-check.txt` |
| 动作尺寸 | 4,568 | `.artifacts/preview22-native/scale-check.txt` |
| 照顾 | 3,040 | `.artifacts/preview22-native/care-check.txt` |
| 任务栏实际接触 | 290 | `.artifacts/preview22-release2-floor-contact/taskbar-contact-check.txt` |
| 菜单与躲藏 | 664 | `.artifacts/preview22-native/interaction-check.txt` |
| 互动细节 | 249 | `.artifacts/preview22-native/detail-check.txt` |
| 放置与内嵌聊天 | 969 | `.artifacts/preview22-release2-placement/placement-check.txt` |
| 修复后新增互动定向复测 | 347 | `.artifacts/preview22-club-render-final/club-check.txt` |
| GPT Q 版照顾修复复测 | 183 | `.artifacts/q-care-gpt-final/club-check.txt` |
| DeepSeek Q 版婚纱照顾复测 | 60 | `.artifacts/q-care-whale-final/club-check.txt` |
| Gemini 3D 真人照顾修复复测 | 164 | `.artifacts/r-care-gemini-verified/club-check.txt` |
| Grok 3D 真人照顾修复复测 | 164 | `.artifacts/r-care-grok-verified/club-check.txt` |

连续图集的可见对象是人物网格，透明图片仅作命中和几何代理。相关断言同时检查人物可见与服装保持；行走帧变化观察实际渲染事件，避免延迟采样恰好落在下一个循环的同一帧。

实时验证使用可见测试窗口（`--ui-test`）串行运行。隐藏启动或与同步截图交错时曾出现长间隔渲染，失败记录保留在此前验收目录；不能据此宣称全程 60 fps。Pose、Contact 的实时矩阵结束后才统一生成 `-replay.png`，截图用于核对画面，实际时序以对应呈现记录为准。

GLM 3D 真人运动服的侧卧图经目视确认：猫耳和尾巴使完整轮廓约为站姿高度的 46.3%。睡姿检测由旧稿专用的 44% 改为低于站姿一半，仍要求专用睡姿来源、正确首帧、睡眠状态和无多余特效；没有为满足阈值缩小原图或人体。

## 原生截图目视复核

| 范围 | 检查结论 | 截图索引 |
| --- | --- | --- |
| 32 套 Q 版与 32 套 3D 真人，待机／梳发／擦脸共 192 张 | 已逐套比较头身比例、服装、身份与手部道具接触；发现的问题按下列修复图重新验收 | `.artifacts/preview22-native/club-<id>-<outfit>-idle.png`、`-comb.png`、`-wipe.png` |
| GPT Q 版旧三套服装、DeepSeek Q 版婚纱，共 12 张修复后截图 | 泳装裸足、婚纱两手完整，梳齿贴发、毛巾贴颊，未见明显体型跳变 | `.artifacts/q-care-gpt-final/`、`.artifacts/q-care-whale-final/` |
| Gemini、Grok 3D 真人旧三套服装，共 18 张修复后截图 | 成年头身比例与待机对应，服装及手部接触保持 | `.artifacts/r-care-gemini-verified/`、`.artifacts/r-care-grok-verified/` |

截图目视不能由动作键计数替代。GPT Q 版部分旧照顾图在深色背景仍有约一像素的蓝紫轮廓边缘，未扩大为背景色块；本轮未据此重绘整套角色。

## 发布路径与入口

最终暂存包 `.artifacts/release-preview22` 已通过只读路径与原图哈希审计：16 个角色、64 套外观、16 套运动服、96 张选中运动服原稿与源码一致；原生素材从程序目录的 `Assets/Characters` 读取，CloudClub 与 TaskbarLift 使用包内相对路径或内嵌数据，不依赖 C 盘桌面。35 张废弃长裤原图与发布包无同字节图片；历史 `school-chibi.png`、`school-realistic.png` 已与短裤新稿一致。生成记录中的 C 盘路径仅保留来源说明，不参与运行。证据为 `.artifacts/preview22-final-path-audit.json`。

自包含发布程序的 `--verify-assets` 实际解码通过全部 1,696 个图片定义，记录在 `.artifacts/preview22-release-assets/asset-check.txt`。最终 Demo 导出为 8 套外观、284 段动作、14,801 张独立帧、2,304 行动作覆盖，缺失与近似项均为零；原生逐帧图片目录为 `artifacts/demo-cf4f43e5f0ae4e40a41fae38b4a1851d`。

最终发布目标：`D:\VibeCoding\Character\Release\win-x64\DesktopPet.exe`。

长期 Demo 目标：同目录 `Demo\CloudClub\index.html`。衣柜显示八位角色的两种风格；完整动作页播放 DeepSeek 两种风格、四套服装的原生导出帧，并提供 64 套外观动作清单。其他角色的全部动作在原生程序内切换查看。

桌面仅保留 `DeepSeek-動作Demo.lnk`。账号、角色、服装和桌面位置继续使用原本的用户数据目录，验收使用 D 盘独立数据目录。测试阶段会员全部开放；注册为本机账号，尚未接入账号服务器或收费授权。

## 最终发布验收

- Pose、Contact 及刷新兜底相关行走、任务栏、放置、舞蹈、六项新增互动回归均已通过。
- 最终 `publish.ps1 -RefreshDemo` 已完成，原生导出帧、CloudClub 与 TaskbarLift 已刷新；资源解码和路径审计均通过。
- 最终浏览器 7 项检查全部通过：完整动作页、会员预览、任务栏 500 项规则、六项互动 × 八套外观、CloudClub 衣柜与窄屏菜单、八套外观真实拖放、婚纱两种风格的梳头／擦脸。0 脚本异常、0 缺失资源；一项导航取消的图片请求已独立解码通过。证据：`.artifacts/preview22-browser/final-summary.json` 与其 `final/` 子目录。
- 已将经检验的暂存包完整移至 `Release/win-x64` 并启动。程序与 Demo 的 SHA256 和验收包一致；启动后无错误日志，原角色、服装、打卡及已有账号文件比对一致。启动记录：`.artifacts/preview22-launch-check/launch.json`。
- 旧版完整保留在 `.artifacts/release-before-preview22-20261003-040536`，用于回退；用户数据另有独立备份。桌面同一个 `DeepSeek-動作Demo.lnk` 已确认指向 D 盘新版本的 CloudClub 页面。
