# 本项目的持续约定

- 2026-10-07 已完成 Preview27 原生鼠标修复并运行：petInput 使用 1/255 alpha 的人物输入面，排除脚下留白；DesktopHost 只对命中该输入面的本窗口鼠标消息捕获、拖动和松开。普通松手、Shift下落、捕获取消、落地散步和右键菜单规则不变。全64套/两方向 Windows 命中1152次0失败、手势1024项，候选288命中/64手势，旧行为和五项互动回归通过；旧躲藏验收改向实际 InputSurface 发事件。正式程序仍 Release/win-x64/DesktopPet.exe，926素材不变，用户state安装前后哈希一致。新的同事包为 Release/DesktopPet-v1.2.0-preview.27-win-x64-portable.zip，945文件/约1.43GB，已逐文件解压比对并隔离正常启动。详见docs/verification-preview27.md和.artifacts/preview27-release-record.json。新换装仍只在待审 W1 Demo：Release/win-x64/Demo/WardrobeIdentity/index.html，常态CloudClub顶部链接；DeepSeek Q泳装六姿势与12帧走路、3D真人短袖短裤单张，用原装/泳装/婚纱并排叠图核对，未接入原生、未拓展其他角色。禁止把该Demo当作已批准。Gitee仍缺有效接口授权，尚未上传；发布准备脚本改为Preview27及其SHA256。继续沿用D盘工程与桌面原两个快捷方式，不恢复已删除的舞蹈。

- 2026-10-06 用户告知即将断网，当前暂停等待继续。本轮待办：任务栏行走仍难以提起；Q版泳装与3D真人运动服须按同一角色换衣，先重做DeepSeek HTML Demo。桌面「云朵伙伴」快捷方式已核实指向 Release/win-x64/DesktopPet.exe --settings。GitHub main 与 codex/cloud-companions 已完成推送2407dea；Gitee接口授权未通过，尚未上传，不再盲目要求重复网页登录。工作区新增 NativePointerVerification 与 DesktopHost/PetWindow 本窗口原生鼠标消息处理、微透明角色点击区域，仍待完整回归和发布，正式程序仍Preview26。旧版原生窗口命中检查发现144次采样中9次Windows穿透而WPF命中，证据.artifacts/native-pointer-before；后续结果看.artifacts/native-pointer-after。新换装图仅位于 docs/demo/wardrobe-identity/art（真人运动服一张、Q泳装六姿势与12帧行走各一张），以原画作衣服替换，提示词见provenance.json；HTML尚未完成且未获批准，禁止把新图直接接入原生或宣称全量修正。先完成同尺寸并排/叠图/逐帧Demo并目视验证，随后等用户拍板。不得因本条恢复已删除的舞蹈。Release旧13目录与3个PDB已清理，保留win-x64及Preview26 ZIP/校验文件。

- 2026-10-06 Preview26已发布并启动：修复行走时左键无法停步、点击或提起。PetWindow 使用独立 petInput 透明输入层，覆盖原图/三维绘制并保持鼠标捕获；不得再只把鼠标事件挂在可能隐藏的 sprite 上。按下清除 roaming、exploring 和后续回调，屏幕坐标取系统光标，无左键的 MouseMove 不进入提起。右键只开关菜单，普通松手停在半空、Shift下落及任务栏落地散步规则不变。64外观两方向912项新检查及旧行为回归通过。`Release/DesktopPet-v1.2.0-preview.26-win-x64-portable.zip` 为同事运行包，945文件约1.43GB，自带运行时；保留Assets/Studio，不含大型Demo、个人数据。解压到中文空格路径后1857图片定义、912中断、10600 WPF检查和全新存档正常启动通过。日常数据完整备份且安装前后字节相同，正式进程路径仍为Release/win-x64。常态Demo指针为artifacts/demo-preview26-final，沿用Preview25原渲染帧，桌面同一快捷方式不变。详见docs/verification-preview26.md和.artifacts/preview26-release-record.json。以后打同事包用tools/package.ps1 -RuntimeOnly，版本从exe读取；Windows自定义DOTNET_BUNDLE_EXTRACT_BASE_DIR须先创建并使用规范反斜杠绝对路径。

- 2026-10-06 I3全量已发布为 Preview25：8位角色×Q版/3D真人×四服装共64套接入击掌、猜拳、拆礼物、共读、合照；故事读完收藏与次数持久化。真人维持成年全身比例，运动服短袖膝上短裤，29套Q版掌心按原PNG再次校正；不局部变形或随姿势包围盒缩放。原生候选包5902项/64套、1857图片定义、核心56项、旧行为11组回归、完整网页22组、64套网页实际交互及32套Q版最终接掌检查通过。常态Demo40动作/320回放/14612独立帧/2560覆盖行，缺失和近似0。运行目录仍是D盘Release/win-x64，桌面同一个CloudClub快捷方式；旧包及日常存档备份在D盘.artifacts，正式替换时日常state字节不变。详见docs/verification-preview25.md及.artifacts/preview25-release-record.json。此条覆盖下方历史未接入/未发布阶段状态，舞蹈继续删除，未来新设计仍先Demo拍板。

- 2026-10-06 拓展途中用户指出后排腿缩短：不能只对齐总高度，必须同时保持原立绘的头身比、腰线、膝盖与脚踝位置，确保躯干/腿长协调；20格真人试产 `artwork/interaction-five/deepseek-adult-{swim,sports}-v1.png` 已目视发现截腿/缩腿，不可选用或发布，改回已批准的四张完整全身分组。

- 2026-10-06 用户对I3明确「同意拓展」：3D真人比例修订和五项互动已批准，直接完成两种风格、8个角色、4套服装的原生接入、故事收藏和长期Demo同步。此授权合并此前Q版批准，覆盖历史真人仅Demo/等待拍板限制；以I3成年比例为准，原装/泳装/婚纱/短袖短裤运动服不得串装。尚未完成全量验证和发布前不得宣称全部接入。

- 2026-10-04 已完成3D真人比例修订 Demo I3，入口 `Release/win-x64/Demo/InteractionFive/index.html?review=I3&style=realistic`，默认进入3D真人。新20姿势仅以原生DeepSeek真人立绘为身份与成年体型参考，改五组4×1图集；使用每组固定比例、脚底/手心/礼盒/书坐标，不做形体拉伸。页面增加原立绘同高对照、真人更大显示区，桌面与手机对照比例相同。三篇故事新增读完收藏、次数、刷新恢复与重读，沿用Demo存储。完整互动22组与2倍像素密度/真人窄屏/实际高度/接触检查通过，证据 `.artifacts/interaction-five-review-I3`。原生exe和正常用户state哈希与Preview24一致。Q版全角色接入授权仍有效但尚未完成，Core的FiveInteraction/StoryLibrary/PetState为未发布工作；此轮用户要求先完成真人Demo，不把该Demo的验证当作Q版全量发布。3D真人尚未获扩展批准。

- 2026-10-04 最新授权：用户同意「Q版拓展与收录故事」。五项互动允许扩展到8位Q版角色及现有服装，读完故事后收藏并可重读；3D真人仍只完善Demo，尚未批准扩展。随后用户指出3D Demo再次矮化，当前先完成该部分：以原生 `deepseek-adult/portrait.png` 为唯一身份、成年头身比例和腿长参考，重做专用互动图，禁止沿用矮化图集作为形体参考；逐项验证完整身体、脚底、手心接触和道具位置。以上覆盖旧I1/I2的Q版待审限制，3D真人接入仍须用户拍板。Q版全量接入尚在进行，不能据此宣称已发布。

- 2026-10-04 五个新互动继续仅迭代 HTML，最新待审为 `Release/win-x64/Demo/InteractionFive/index.html?review=I2`（覆盖I1入口）。用户要求击掌有双方互动、猜拳有双方摇拳/出手、拆礼物随机收藏、共读有限短故事：已做可拖动用户手、真实掌心接触后计数、双方三轮摇拳再同时亮手、20种与原生球/散步一致的随机物品和专用 Demo 收藏存储、3篇原创7–8句故事逐句阅读/暂停/结束；合照保留。DeepSeek 原装 Q版/3D真人各24张原PNG关键姿势，补充图使用同来源固定比例、脚底/手心/空礼盒测量坐标；随机SVG物品在空礼盒内并由原图前壁/手遮挡。真实鼠标、键盘、Chrome file页面22组检查通过，证据 `.artifacts/interaction-five-review-I2/report.json`；原生exe和正常用户存档字节不变。修复拖动手之后键盘击掌被旧点击标记吞掉的问题。桌面同一个快捷方式仍指向CloudClub，其顶部进入I2；部署脚本同步items.js和两版新图稿。不将待审 Demo 接入原生或扩展角色/服装，继续等用户明确拍板。

- 2026-10-04 用户要求「继续新增互动，再选5个跟使用者交互的做成Demo」：新增待审 HTML `docs/demo/interaction-five`，运行页 `Release/win-x64/Demo/InteractionFive/index.html?review=I1`；五项为击掌、猜拳、接礼物、翻书共读、合照（用户云朵头像＋昵称）。先用 DeepSeek 原装的 Q版/3D真人，各12张专用原始PNG姿势；同一风格固定尺寸、脚底对齐，礼物和书在人物手中。用真实点击/拖动/按键验证，15组检查通过，证据 `.artifacts/interaction-five-review/report.json`，图稿/提示词记录在该 Demo 的 `art` 和 README。此轮还未获拓展授权，不能接入原生、扩展其他角色或服装；只在 CloudClub 顶部增加新 Demo 链接，桌面同一个快捷方式保持指向 CloudClub。原生 Preview24 exe 与正常用户存档哈希一致。安装用 `tools/install-interaction-five-demo.ps1`；CloudClub 安装器一并部署此待审页。不得重新加入已经删除的功能。

- 2026-10-04 用户明确「请把所有舞蹈的都删除」：删除舞蹈入口、原生代码、专用美术、动作数据、HTML 试映与开发产物；此指令取代此前封存或恢复计划，不再保留舞蹈供恢复。继续维护其他互动及16个角色、64套外观，保留用户存档与桌面同一个 CloudClub 快捷方式。以后未经新授权不要重新加入该功能。
- 清理已发布为 Preview24，当前程序仍在 D 盘 `Release/win-x64/DesktopPet.exe`。17 项原生验证报告、51 项核心检查及 CloudClub/完整动作页浏览器检查通过；现行 DeepSeek Demo 为35种动作、280段回放、13997张独立帧、2240行全角色覆盖。481份保留动作图稿与清理前字节一致，用户日常存档未变。已删除的 A/B/比例试映页、旧下载与旧包不再作为入口或后续开发依据；只有 `CloudClub`、完整动作页与 `TaskbarLift` 保留。记录见 `docs/verification-preview24.md` 和 `.artifacts/feature-removal-20261004`。

- 2026-10-03 已完成本地 Preview 22 发布并启动：全部 8 位角色 × Q版/3D真人的短袖上衣＋膝上短裤运动服接入，保留原装/泳装/婚纱，共 16 项角色、64 套外观。运行程序仍为 `D:\VibeCoding\Character\Release\win-x64\DesktopPet.exe`，桌面仅保留同一个 `DeepSeek-動作Demo.lnk` 指向包内 `Demo\CloudClub\index.html`；原用户角色、服装、打卡和账号文件已比对保留。本条覆盖下方旧「仅 Demo／待完成／保持 Preview21」阶段状态，后续未经拍板的新设计仍先 HTML Demo。完整验证记录见 `docs/verification-preview22.md`，最终 Demo 为 8 套 DeepSeek 外观、284 段动作、14801 张独立帧、2304 行全角色覆盖。活动动画仍由 Rendering 驱动，50ms 未推进时用同一 lastTick 的 DispatcherTimer 兜底，防止透明窗口刷新中断卡住；隐藏、关闭和 Preview 必须停用。实时验证用可见 `--ui-test` 串行执行，所有实时动作结束后再生成回放截图，等待真实呈现，不用固定延时两帧比较冒充动画证据。旧版备份在 `.artifacts/release-before-preview22-20261003-040536`，不得把旧包再次当作最新版。

- 2026-10-02 最新用户明确「同意拓展到全部」：修订05的短袖上衣＋膝上短裤款式已获扩展授权，直接完成全部8角色×Q版/3D真人的运动服与动作接入、验证和发布，不再等待同一款式或已确认动作的额外拍板。覆盖下方历史「仅Demo/待确认/保持Preview21」的阶段限制；长裤素材仍不可发布。沿用D盘项目、桌面常态Demo入口，保留用户角色/服装/账号存档。图稿须逐帧确认短袖、短裤、身份和真实手部接触；全动作、走路、UI与常态Demo一起验证，未完成不得宣称覆盖完成。

- 2026-10-02 用户最新明确「动作看了同意」，动作设计已批准，不再重复请求动作拍板。运动服要求再次确认为短袖上衣＋短裤（含3D真人）。修订05将源码/运行Demo的 school-chibi.png、school-realistic.png 历史公开原图路径也替换为短裤版，旧长裤概念存 artwork/sports/superseded-long-pants/concepts；原生运动服全动作与梳头/擦脸真实接触仍待完成，保持 Preview21，不能把服装静态样稿冒称为完整接入。

- 2026-10-02 最新更正：用户明确阻止长袖/长裤运动服方案，要求所有角色的 Q版与3D真人都穿「短袖上衣＋短裤」，袖口在上臂、裤脚在膝盖以上。此更正覆盖此前误记的长运动裤要求。已经生成的长裤版本未获批准，不发布或继续拓展；先以 DeepSeek 两版重做 HTML Demo 样稿，确认后扩展。新增动作的实际图片接触与连续性检查仍待完成，不能宣称本轮已完成。
- 短袖＋短裤待审图为 `docs/demo/cloud-club/art/school-shorts-{chibi,realistic}.png`，提示词与来源见 `shorts-provenance.json`，常态 Demo 已同步到 D 盘运行目录的 CloudClub 页。已逐张查看袖口、膝上裤脚并检查桌面/窄屏显示，证据在 `.artifacts/cloud-club-shorts-review`。旧长裤批次 94 条素材记录撤销选用，16 个已安装运动服素材目录移至 `artwork/sports/superseded-long-pants`；角色清单已恢复无运动服的原值，服装选择保持原装/泳装/婚纱。当前原生动作代码仍是未完工 WIP，发布程序维持 Preview 21，不可直接发布这轮工作区。

- 2026-10-02 用户进一步同意「拓展其他有运动服」：运动服最终扩展至全部 8 位角色 × Q版/3D真人，作为 `sports` 第四套服装接入，保持角色身份与运动鞋，动作不得回退到其他服装。此前本条误写「长运动裤」已撤销，服装须遵守上方最新的短袖＋短裤要求。用户同时要求仔细检查新增动作的连续性与实际图片互动，特别修正梳头/擦脸只有道具、人物仍待机的问题；验证必须观看实际播放并检查手部接触，不以动作键存在代替画面验证。其余未批准的新设计仍遵循先 Demo 规则。

- 2026-10-02 用户已明确「先同意与拓展。学校运动衫很好但要短袖」：CloudClub 第二轮已拍板，授权接入原生桌面程序并扩展到全部角色和现有两类风格/三套服装；测试阶段所有等级与聊天开放，实际账号等级仍保持原样。采用浅玉金属绿白金、右键只切换菜单、一级互动、随机侧躲藏，以及梳头/擦脸/伸懒腰/吹泡泡/数星星/捉蝴蝶。运动衫保留已批准款式和配色，改短袖，先完善 DeepSeek 两版。此条覆盖下方这两轮「仍仅 Demo / 未批准」状态记录；新的其他设计仍先 Demo 后拍板。

- 2026-10-02 CloudClub 第二轮仍仅做 Demo：白金已选定浅玉金属绿，去掉深翡翠选择；接星星按数星星制作，需要人物指点/抬头/计数，吹泡泡需要举棒、送嘴、吹气、收手专用姿势，不能只有道具产生；陪伴呼吸替换为待审伸懒腰。学校运动衫先做 DeepSeek Q版/3D真人服装样稿，未批准全动作接入，不覆盖现有服装。新样稿与提示词保存在 `docs/demo/cloud-club/art`，素材留在 D 盘，桌面仍仅保留同一个 Demo 快捷方式。

- 2026-10-02 最新规则：后续改动一律先做 HTML Demo，用户明确拍板后才接入桌面程序；未拍板持续迭代 Demo。本轮 `docs/demo/cloud-club` 为待审方案：测试期间全部会员等级和聊天开放，白金尝试金属绿色，新增三项照顾与三项玩耍；右键只切换圆盘且不取消、重启或暂停当前动作（躲藏时也如此，左键找到）；一级「动作」改「互动」，左右躲藏合并为随机侧「躲藏」。该新要求优先于下面已发布版本的行为记录。此轮不改原生源代码、账号权限或运行程序，不把 Demo 通过当作用户拍板。
- 本轮待审入口为 `Release/win-x64/Demo/CloudClub/index.html`，桌面原有 `DeepSeek-動作Demo.lnk` 暂指向此页；由 `tools/install-cloud-club-demo.ps1 -DesktopShortcut` 更新。页尾保留现行完整动作 Demo。六项新增为梳头、擦脸、陪伴呼吸、吹泡泡、接星星、捉蝴蝶；目前是现有画稿的交互编排，尚未新绘专用角色姿势。回归使用 `node tools/verify-cloud-club.cjs`，记录与截图在 `.artifacts/cloud-club-review`。

- 最新整合项目位于 `D:\VibeCoding\Character`，日常运行版本为 `Release\win-x64\DesktopPet.exe`。不要依赖或重建 C 槽桌面的旧工程。
- 2026-09-29 分类合并：今后只维护「Q版」(`chibi`) 与「3D真人」(`realistic`)。以已有真人画稿作为统一底稿，8 位角色 × 2 种风格 × 3 套服装，共 16 项、48 套外观。原 `*-adult` 目录和 ID 保留兼容；旧 `*-3d` ID 与服装存档自动迁移，原 3D 画稿归档到 `artwork/retired-3d/characters`，不再生成或打包。旧导入包的 `3d`、`adult` 分类读取为 `realistic`。界面、工坊、Demo、清单及后续提示词都使用新分类；不要重新维护第三套动作矩阵。
- 用户明确要求：DeepSeek 长期动作 Demo 的入口保留在 Windows 桌面。使用 `DeepSeek-動作Demo.lnk` 指向 D 槽运行目录的 `Demo\DeepSeek-demo.html`；HTML 和 `frames` 素材仍在 D 槽。此入口是保持桌面干净的例外，不要清理掉。
- 2026-10-01 用户确认抓起交互：普通松手固定在当前位置；只有松手当下按住 Shift 才下落。不再按起手来源、速度或停留时长判断；从半空起手也适用。距任务栏 24px 内沿用吸附。下落中重抓立即中断，普通松手停住；捕获丢失、取消、失去焦点不得当作 Shift 下落。原生 `PetWindow.ReleaseLift(bool dropOnRelease = false)`、常态 Demo 和独立 `docs/demo/taskbar-lift` 保持相同规则；减少动态时直接落地、恢复待机并保存。独立 Demo 部署到 `Release/win-x64/Demo/TaskbarLift`，由常态 Demo 页进入；`tools/install-taskbar-lift-demo.ps1` 从常态 Demo 抽取两种风格三套服装的现有帧，不复制新画稿。
- 修改互动后同步更新长期 Demo 和动作清单。普通拖动保持提起姿势，只有连续明显来回摇晃才触发头晕；手动放置应停留在选定位置。
- 2026-10-01 用户要求触到任务栏后自动散步。打卡仅记录进度，不再用「今天未打卡」强制睡眠或禁止散步。开启 `State.Wander` 时，启动在地面、落地缓冲完成及地面放置后自动散步；半空手动放置不走、不掉落。尊重主动休息、关闭自动散步和减少动态效果。`--verify-walk` 包含未打卡的 48 套外观回归及真实启动检查，不得仅以提前打卡的测试证明此行为正常。常态 Demo 和 TaskbarLift 同步；独立 Demo 复用每套外观现有的 12 个行走姿势，不新增素材副本。
- 2026-10-02 补全任务栏接触路径：鼠标捕获丢失或窗口失去焦点时，若拖动已到地面，仍要落地缓冲后散步；若在半空则安全停住，不能把中断当作 Shift 请求。主动结束互动和关闭程序不触发落地散步。鼠标事件与检查共用 `BeginPointerGesture`、`MovePointerGesture`、`EndPointerGesture`；`--verify-floor-contact` 使用真实 Dispatcher/Rendering 检查位置和姿势变化，不能只用 `AdvancePreview` 或进程存活证明正常。`--motion-log <D盘文件>` 为可选本地排错记录，默认关闭、最多约 2 MiB，不记录聊天或密钥。
- 圆盘菜单采用透明背景、无可见云朵底板和柔和圆润的线形图标；保留悬停提示及键盘操作。
- 2026-10-02 会员基础：主页加入注册入口和会员中心，五档按黑金、白金、黄金、白银、黄铜排列。黑金全部姿态开放；其他姿态分级等用户通知，目前只对聊天要求黄金及以上。注册使用本机账号提供者 `IAccountService`，新账号黄铜，不能在注册或普通 UI 中自行升档；尚未接入账号服务器，不能宣传云端注册或收费授权已完成。账号与宠物存档分开，密码只保存随机盐与派生摘要，重启需要登录。锁定入口用带笑脸的柔和矢量小锁并显示门槛；原生入口与发送消息也检查权限。退出取消回复并清除当前聊天与金钥。运行 `--verify-membership`，测试账号和模拟等级仅限 D 盘独立目录，普通启动不授予测试黑金。常态 Demo 的会员选择器明确是权益预览，使用 `--export-membership` 与 `tools/refresh-demo-template.py` 同步，不重新生成未改变的动作帧。
- 2026-10-02 照顾与点击：聊天直接位于第一层圆盘；照顾保留早餐、零食、摸头、揉脸、挠痒，新增夸夸、安抚、哄睡。普通点击轮换三种触摸，拖动不消耗次数，未打卡不自动喂早餐。躲藏过程左、右键均使用找到回应，结束边缘裁切且不打开菜单。新照顾通过 `CareRoutine` 编排当前外观的现有姿态，原生和 Demo 共用阶段时序；取消、换装、抓起必须清除后续阶段。运行 `--verify-care` 和 `--verify-interactions`，同步完整 Demo 清单。Q版原装 `eat/feed` 使用 `motions/snack-continuity.png` 八帧饼干图，不再指向旧魔法棒画稿；安装后继续执行尺寸校正及动作回归。
- 菜单图标应直接表达具体姿态或道具；动作入口用小人，跳跃画离地姿态，蜷起画抱膝，躲藏画边缘探头，避免用抽象星星、上传箭头或旋涡代替动作。
- 散步和边缘躲藏使用画面刷新回调；逻辑位置保留小数，窗口整像素位置以画面内偏移补偿。不要重新从取整后的 Window.Left 累积步长；结束或隐藏时释放回调。修改时运行 --verify-walk 并同步 Demo 的转身和时间推进逻辑。
- 验证使用 D 槽独立 `--data-dir`，不要覆盖用户当前角色、服装、打卡或收藏。更新运行版本前保存现有状态；只关闭本项目的进程。
- 2026-10-02 动作尺寸校正：新图集以 `referenceHeightPixels` 固定整套像素比例，不能随着举手、弯膝、道具或衣裙轮廓逐帧缩放。历史 Q版图集中跳跃、思考、提起等帧本身被画小，使用实测 `frameScaleFactors` 修正；不能把睡眠或蜷起的全身高度强制拉到站立高度。Q版新积木画稿已经是坐姿，不再额外乘旧 `.76`。美术重新安装后执行 `tools/calibrate-action-scale.py --install`，审阅实际渲染图，并运行 `--verify-scale`、Q版、互动和行走检查，再更新常态 Demo。测量只写元数据，原图像素保持原样。
