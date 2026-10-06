# Preview 24 本地验证

2026-10-04 按用户要求删除全部舞蹈功能。运行程序：`D:\VibeCoding\Character\Release\win-x64\DesktopPet.exe`。常态入口：`Demo/CloudClub/index.html`；桌面同一个快捷方式继续指向这里。

- 删除原生入口、实现、专用绑定、美术、试映页、下载数据、开发工具和旧可执行产物。
- 保留16个角色、64套外观及其他互动。481份保留动作图稿的 SHA-256 与删除前一致。
- 常态 DeepSeek Demo 保留两类风格、四套服装，35种动作、280段回放、13997张独立帧与2240行全角色覆盖。移除专用回放帧并重新映射索引；保留原始非舞蹈像素及其渲染版本来源。
- 51项核心行为检查通过；Release编译无错误或警告。16角色的1664条图像定义解码通过；16套运动服资源检查无缺失。

桌面实时验证使用 D 盘独立存档和可见 WPF 窗口串行执行；不以网页回放代替原生输入检查。

| 检查 | 结果 |
|---|---|
| care-check.txt | 3040 care checks passed. |
| chibi-check.txt | 7369 Q wardrobe action checks passed. |
| choreography-check.txt | 1354 choreography checks passed. |
| contact-check.txt | 1058 drawn contact checks passed. |
| contact-live-check.txt | 866 live contact checks passed before any proof rendering. |
| detail-check.txt | 249 detailed interaction checks passed. |
| floor-walk-check.txt | 960 unchecked floor-walk checks passed across 64 appearances. |
| interaction-check.txt | 549 interaction checks passed. |
| membership-check.txt | PASS 156 membership, actual registration/login, tier access and account isolation checks. |
| placement-check.txt | 969 placement and inline chat checks passed. |
| polish-check.txt | 3859 rendered motion polish checks passed. |
| scale-check.txt | 4536 scale checks passed. |
| shake-check.txt | 1056 shake and recovery checks passed across 64 appearances. |
| sports-check.txt | PASS 8568 checks across 16 sports appearances (full roster). Visual inspection still required for clothing and hand anatomy. |
| taskbar-contact-check.txt | 290 real-time taskbar contact checks passed. |
| ui-check.txt | 10600 WPF integration checks passed. |
| walk-check.txt | 3847 walking checks passed. |

浏览器检查涵盖实际 CloudClub 圆盘、六项保留互动、换装、聊天、任务栏抓起，以及舞蹈入口和数据的缺失。用户日常存档另行比对，测试不使用日常账号目录。

详细本地记录：`.artifacts/feature-removal-20261004`。删除审计保存路径与计数，不保存已删除功能的备份。
