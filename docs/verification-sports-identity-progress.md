# 运动服换衣复核进度（未全量发布）

2026-10-07。W2 拓展授权有效，现行日常程序为 v0.4。日常 exe SHA256 为 `B0F270274AD8BDB43098DDA0EB3F91D5D93C553E60F4C367AFC7C0FC201FDDE0`；本轮仅在 D 盘隔离目录验证，没有替换日常角色资产、修改用户存档或制作正式安装包。

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

- GPT 修订稿、其余六家完整动作图的逐帧目视复核。
- 所有新图的手部及道具接触点核对和原生验收。
- 全量替换、长期动作 Demo 实际渲染帧刷新、版本推进与运行更新。

源码和中间审阅资料推送 GitHub。Gitee 暂停，桌面入口维持原快捷方式。
