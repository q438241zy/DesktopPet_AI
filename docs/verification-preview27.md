# Preview 27：任务栏鼠标命中与换装审阅

2026-10-07。用户继续报告任务栏行走时不能点中、提起。此次修复原生 Windows 输入，DeepSeek 换装样稿仍只放在 HTML Demo，未替换任何正式角色画稿。

## 修复

WPF 的透明控件可以通过内部命中测试，但透明分层窗口的零 alpha 像素仍可能把鼠标交给下面的窗口。将角色输入面改为 1/255 alpha，限制在人物范围内，排除脚下的透明图片留白，避免挡住任务栏。原图、姿势图和三维绘制共用这个输入面。

`DesktopHost` 接收本 HWND 的左键按下、移动、松开和捕获取消消息。只接管角色输入面，聊天、圆盘菜单、玩具和五项互动控件继续使用自己的输入处理。按下立即停止散步或探索；拖动捕获鼠标；普通松手停住，Shift 松手下落，取消与捕获丢失不当作 Shift。右键规则不变。

新增 `--verify-native-pointer`，通过 Windows `WindowFromPoint` 检查真实桌面窗口目标，再向本窗口投递鼠标消息检查完整手势。`--pointer-all` 扩展至64套外观、两个方向。记录目标进程，避免把其他窗口遮挡误称为自己的程序接收了点击。此次检查使用本窗口消息与 WPF 呈现时钟，未做物理鼠标硬件注入。

## 验证

| 检查 | 结果 |
| --- | --- |
| Release 编译与自包含发布 | 成功，无警告或错误 |
| 核心行为 | 56项 |
| 全外观 Windows 命中 | 1,152次采样，0次未命中 |
| 全外观窗口手势 | 1,024项：点击、停步、提起、普通松手、Shift、取消、任务栏留白、显式穿透 |
| 发布候选原生复核 | 288次命中、64项手势，通过 |
| WPF 行走中断 | 912项 |
| 行走与未打卡落地散步 | 3,847项 + 960项 |
| 真实时钟任务栏接触 | 290项 |
| 放置与角色头顶聊天 | 969项 |
| 菜单、边缘躲藏与找到 | 601项 |
| CloudClub 已有互动 | 2,643项 |
| 击掌、猜拳、礼物、共读、合照 | 5,902项，64套外观 |

修正了一处旧验收代码：躲藏检查仍把鼠标事件发给 `Image`，而正式接收者已是角色输入面。改为真实的输入对象，保留左键找到、右键只开菜单和走回的断言，并补充探头时输入面可见检查。首次全量命中运行另有36次目标落在不同进程的窗口；保留原报告，加入目标进程名称后重跑完整矩阵以及发布候选，均通过，不把那36次算作通过。

证据：`.artifacts/native-pointer-before`、`.artifacts/native-pointer-all`、`.artifacts/native-pointer-all-r2`、`.artifacts/preview27-regression`。所有原生检查串行使用 D 盘独立存档；没有用正常账号授予测试等级。

## 换装 Demo

入口为 `Release/win-x64/Demo/WardrobeIdentity/index.html?review=W1`，从常态 CloudClub 顶部进入。六个 Q版基本姿势、十二帧泳装行走，以及一张3D真人短袖短裤样稿；支持同尺寸并排、叠图、逐帧和原装/泳装/婚纱参考。详见 [Demo 说明](demo/wardrobe-identity/README.md)。九组网页检查通过，含窄屏、2倍像素密度和不联网加载；实际截图已查看。

## 安装与同事包

日常路径仍是 `D:\VibeCoding\Character\Release\win-x64\DesktopPet.exe`，桌面「云朵伙伴」指向此处并带 `--settings`。常态 Demo 沿用 `DeepSeek-動作Demo.lnk`。新版本只替换 exe；926个正式素材文件逐个比对不变，用户数据在 D 盘备份，安装前后 state 文件哈希一致。

程序版本：`1.2.0-preview.27+2407dea7ede68f1156638e87d9ef97932dd9f59b`。后缀是构建基底提交；本轮工作区变更另行提交。exe SHA-256：`7187848B8F663F7F0BB33AA23610C6CD5982D08C6E5598E99BB72218F0F16A1E`。

同事包：`Release/DesktopPet-v1.2.0-preview.27-win-x64-portable.zip`，945文件，1,433,700,268字节。SHA-256：`AF30451B8A9C3E62A3F4DC76C23A29A77D3CBCBFBE25C478BDA37DE104EDB838`。

解压到新的中文及空格目录，每个文件与候选发布内容一致；新的隔离存档正常启动主页、窗口有响应且无错误日志。包含自带运行时、全部现有外观、Studio与许可证，不含个人资料或大型开发 Demo。此兼容性验证限于本机 Windows 环境。记录在 `.artifacts/preview27-package.json`、`.artifacts/preview27-portability.json`、`.artifacts/preview27-release-record.json`。

GitHub 用于源码；Gitee 发布接口授权尚未通过，运行包仍在本地，不能把已生成的 ZIP 说成已上传。
