# 运动服换衣复核进度（未全量发布）

更新至2026-10-09。W2 拓展授权有效，现行日常程序为 v0.4。日常 exe SHA256 为 `B0F270274AD8BDB43098DDA0EB3F91D5D93C553E60F4C367AFC7C0FC201FDDE0`；本轮仅在 D 盘隔离目录验证，没有替换日常角色资产、修改用户存档或制作正式安装包。

## 10月9日续作进度（覆盖下方历史待办）

DeepSeek、GPT、Claude、Gemini、Grok、Qwen两版和GLM Q版、Kimi真人共14套通过隔离原生检查并查看实际动作图。Claude Q采用care-v10，旧袖口和脚下相邻发丝问题已解决；有效证据是`sports-native-claude-care10b-20261008`，不是提前运行的care10目录。

| 新增原生复核 | 结果 | 本地证据 |
|---|---:|---|
| Grok Q / 3D真人 | 543 / 547 | `.artifacts/sports-native-grok-20261009`、`sports-native-grok-adult-20261008` |
| Qwen Q / 3D真人 | 543 / 547 | `.artifacts/sports-native-qwen-20261009`、`sports-native-qwen-adult-20261009` |
| GLM Q | 543 | `.artifacts/sports-native-zhipu-20261009` |

Kimi真人的猜拳腕袖已在rps-v3修正，touch-v2恢复原画布，bonk-v2槌子完整，已通过547项原生检查并查看18动作总览（`.artifacts/sports-native-kimi-adult-20261009`）。剩余GLM真人泡泡第15帧圈棒和Kimi Q探头、伸展局部尚在修订。静态候选发生丢失道具、多手、整个人物变大时明确退回，不以自动检查通过选用。每条结果记录保留源哈希、逐帧对照及选用理由；掌心、圈棒使用人工测量坐标并放大检查实际接触。完整16套后的全量矩阵、长期实际帧Demo、发布与版本更新仍未完成。

新增`tools/render-sports-native.cjs`只将原生截图排成检查页，不修改角色图片。下方10月8日矩阵是当时8套新运动服的历史结果，不能代表本次全量接入完成。

## 10月8日隔离进度

| 角色 / 检查 | 结果 | 本地证据 |
|---|---:|---|
| GPT Q / 3D真人运动服 | 543 / 547 | `.artifacts/sports-native-gpt-20261008`、`sports-native-gpt-adult-20261008` |
| Claude 3D真人运动服 | 547 | `.artifacts/sports-native-claude-adult-20261008` |
| Claude Q运动服 | 543；美术尚未通过 | `.artifacts/sports-native-claude-care7-20261008` |
| Gemini Q / 3D真人运动服 | 543 / 547 | `.artifacts/sports-native-gemini-final-20261008`、`sports-native-gemini-adult-final-20261008` |
| 最新尺寸 | 4556 | `.artifacts/sports-matrix-gemini-20261008/scale-check.txt` |
| 最新画面规则 | 3974 | 同目录 `polish-check.txt` |
| 行走 / 未打卡地面 | 3847 / 960 | 同目录 `walk-check.txt`、`floor-walk-check.txt` |
| 最新五项互动 | 5902 / 64套 | `.artifacts/sports-five-gemini-20261008/five-check.txt` |

当前8套新运动服属于DeepSeek、GPT、Claude、Gemini；其余56套外观仍是旧图。Grok真人组的积木、梳头/擦脸、24帧玩耍、击掌、接礼和读书已静态通过；接球、猜拳在清除旧长袖，情绪与Q版其余图生成中。其余三家完整组尚待补齐。原生截图已查看GPT吃饭/接球与真人积木/跳跃/泡泡，Claude真人吃饭/摸头/槌子/泡泡，Gemini Q吃饭/梳头/跳跃与真人接球/泡泡/摸头。实际原生尚未全量推广。

### Claude Q梳头阻断

care-v3/v4/v5虽然通过静态对照及自动动作检查，原生脚下仍带有下一行发丝；不能选用发布。新鞋底偏低，缩小了两行间距。已回到原图修订鞋底正面朝向及位置，而不是拉长腿或截掉角色自己的发环。care-v7在543项原生检查后的梳头、擦脸截图中已无脚下残影，但抬手仍留旧白袖口，已撤销选用并继续局部修订；以最终原生画面为准。

`ArtCache`现在按alpha层从实心人物恢复较密的发丝，再扩展较淡边缘；新增近不透明内核、低透明连接与行间发环样例。`.artifacts/sports-alpha-fixture-20261008`3947项通过，后续3974项包含同一组样例。此规则保留每个像素原alpha和原PNG，但不能修复源图里重叠的人物，必须同时检查画稿。

源画布服务可能有1–2像素边缘取整：只允许一个统一缩放因子且另一边误差不超过2像素，禁止分别拉伸横纵比或按角色包围盒造比例。`frameScaleFactors`保留对应旧原图的明示校正。掌心与泡泡圈测量记录、标记审阅见各`results/*.json`的`measurementPixels`和`contactReview`。

## DeepSeek 隔离样本

| 检查 | 结果 | 本地证据 |
|---|---:|---|
| Q版运动服动作 | 543项通过 | `.artifacts/sports-native-whale/sports-check.txt` |
| 3D真人运动服动作 | 547项通过 | `.artifacts/sports-native-deepseek-adult/sports-check.txt` |
| 五项互动 | 5902项通过 | `.artifacts/sports-native-five-pilot/five-check.txt` |
| 尺寸与原像素校正 | 4541项通过 | `.artifacts/sports-native-scale-walk-calibration/scale-check.txt` |
| 行走 | 3847项通过 | `.artifacts/sports-native-scale-walk-calibration/walk-check.txt` |
| 未打卡地面散步 | 960项通过 | `.artifacts/sports-native-scale-walk-calibration/floor-walk-check.txt` |

后三类和五项互动覆盖64套外观，但仅 DeepSeek 的 Q版、3D真人运动服是本轮新图。检查证明已验证组合的行为，不证明其他角色的新稿已接入。

已目视查看两种风格的实际动作图、掌心接触、道具、落脚与尺寸总览。真人旧动作底图本身含不同绘制风格，换衣对照保持对应原姿态，不把自动检查结果写成所有解剖细节完全一致。普通拖动和 Windows 实际输入本轮没有重跑系统 SendInput 矩阵，沿用 v0.4 / Preview28 既有证据。

## 尺寸检查修订

旧 Q版原动作已有明确的 `frameScaleFactors` 来校正历史小帧。首次隔离尺寸检查发现侧车沿用这些倍率、定义却遗漏了它们，导致验证无法区分原校正和错误自动缩放。暂存器现同时保留该元数据；`ScaleVerification` 对照明示的原倍率检查源像素比例稳定。没有按本轮候选图的身体包围盒生成新倍率，PNG 像素也未改动。

失败的初次结果保存在 `.artifacts/sports-native-scale-walk-pilot`；有效复跑结果是上表的 `sports-native-scale-walk-calibration`，两者不能混报。

## 未完成项

- Claude Q梳头最终修订、Grok全组及Qwen/GLM/Kimi完整动作图的逐帧目视复核。
- 后续新图的手部及道具接触点核对和原生验收。
- 全量替换、长期动作 Demo 实际渲染帧刷新、版本推进与运行更新。

源码和中间审阅资料推送 GitHub。Gitee 暂停，桌面入口维持原快捷方式。
