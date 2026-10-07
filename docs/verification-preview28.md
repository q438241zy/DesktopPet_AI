# Preview 28：移动窗口的鼠标输入

行走时，透明窗口逐帧移动，Windows 鼠标消息可能延迟或漏交给 WPF。原来的 SendMessage 检查绕过了实际输入队列，不能证明真实点击可用。

DesktopHost 现在仅为命中本窗口人物区域的新按下补齐输入，并使用系统当前屏幕坐标。捕获同时登记到 WPF，防止后续事件清掉仅在 Win32 登记的捕获。对补齐过的消息去重；旧移动事件和旧松开事件不能取消新的提起。不抢占从其他窗口按住拖入的鼠标，显式鼠标穿透仍生效。右键只开关菜单，不取消当前动作。

验证使用独立存档，不写入日常账号与衣橱：

| 检查 | 结果 | 证据目录（项目根目录下） |
| --- | --- | --- |
| 实际 SendInput：64 套外观、两方向，按下停步、点击互动、拖动提起、普通松手停留 | 1,152 项通过 | `.artifacts/pointer-system-wpf-capture` |
| 实际 Shift 下落、落地散步、右键、穿透、按住移入、捕获中断 | 95 项通过 | `.artifacts/preview28-final-system-boundaries` |
| Windows 命中几何 | 1,152 次采样，0 漏中；256 项边界检查通过 | `.artifacts/preview28-final-geometry` |
| WPF 行走中断 | 912 项通过 | `.artifacts/preview28-final-interrupt` |
| 任务栏接触／互动／放置／头晕／行走／自动散步 | 290 / 601 / 969 / 1,056 / 3,847 / 960 项通过 | `.artifacts/preview28-final-behavior` |
| Core | 56 项通过 | `dotnet run --project tests/DesktopPet.Tests -c Release` |
| 独立执行档素材解码 | 16 角色、1,857 图片定义通过 | `.artifacts/preview28-stage-assets` |

独立执行档的补充鼠标验证受到外部光标移动与按下干扰，未计为通过；系统输入测试已增加光标／按键干扰检测，遇到干扰立即停止。应用鼠标实现与上方通过全量输入验证的实现一致。诊断新增的坐标记录仅在指定 motion log 时启用。

仅更新日常运行目录，不生成新的正式安装包，不上传 Gitee。运动服 W3 对照已扩展八位角色的 Q版六姿势、十二帧行走与3D真人立绘；配套动作仍在核验，未替换原生运动服。换衣素材的内置 image_gen 提示词及来源存于 `artwork/sports-identity/results`，不可把此输入修复版本当作运动服全动作拓展完成。
