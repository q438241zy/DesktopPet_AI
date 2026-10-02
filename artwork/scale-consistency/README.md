# 动作尺寸测量

`calibration.json` 记录本次固定图集像素比例及历史 Q版源图的缩小校正。没有生成、重采样或修改图片。

新 Q版积木图集已经画成坐姿，旧的 0.76 系数会把整个人物再缩小一次。其参考高度取实际播放的坐姿帧，不包括 DeepSeek 同一张图集中未播放的站立帧。3D真人日常动作以同图集的中性站姿为参考，进食和舞蹈分别使用自己的基准；弯膝、坐卧、悬空、衣裙和道具不再改变像素比例。行走继续使用整套步态的固定比例。

历史 Q版图集有些中间帧把头发、脸和身体一起画小。测量脚本使用 SIFT、RANSAC 相似变换比对待机头部纹理，并对同一段中间姿势使用中位值，减少表情匹配误差。此测量是比例校正，不能修复画稿中不同的人体比例、遮挡或透视，也不能代替渲染图的人工审阅。

```powershell
python tools/calibrate-action-scale.py --install
dotnet build src/DesktopPet.App -c Release
src/DesktopPet.App/bin/Release/net10.0-windows/DesktopPet.exe --verify-scale --data-dir D:/VibeCoding/Character/artifacts/scale-review
```

原生检查输出全部 48 套外观的尺寸记录，以及 DeepSeek、GPT 两种风格三套服装的实际渲染图。比较应关注头部和身体比例；坐卧后整体高度降低、跳跃时弯腿和提起时腿伸直均属于姿态变化。
