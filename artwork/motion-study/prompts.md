# DeepSeek 新动作样例 · 2026-09-29

使用内置 `image_gen` 工具生成与修订透明 PNG；没有调用付费 API/CLI。角色参考均来自本项目 `deepseek-adult/motions/dance-base-atlas.png` 的原装列。角色为现有虚构成年形象，不采用女团成员的真人外貌。

舞蹈参考：[TWICE 官方《TT》练习视频](https://www.youtube.com/watch?v=9uypQGzzhns)、[JYP 官方视频目录](https://twice.jype.com/video)。检索核实了官方来源；未声称完成视频逐帧观看或动作捕捉。Demo 只以脸旁向下的手势为灵感制作简化片段，未嵌入原曲、视频或团体 Logo。比心与招手为原创短动作。

| 最终图集 | 内容 | 最终提示词 |
|---|---|---|
| [adult-idle.png](../../docs/demo/motion-study/assets/adult-idle.png) | 5 个站立姿势，另加伸懒腰、掩嘴打哈欠、小幅招手 | [idle](prompts/adult-idle.txt) |
| [adult-dance-tt.png](../../docs/demo/motion-study/assets/adult-dance-tt.png) | 8 个完整 TT 手势关键姿势 | [TT 大图](prompts/adult-dance-tt-large.txt) |
| [adult-dance-step.png](../../docs/demo/motion-study/assets/adult-dance-step.png) | 8 个比心舞姿势 | [比心](prompts/adult-dance-heart.txt) |
| [adult-dance-wave.png](../../docs/demo/motion-study/assets/adult-dance-wave.png) | 8 个侧步招手姿势，播放顺序按左右手连续性重排 | [招手](prompts/adult-dance-wave.txt) |
| [adult-tickle.png](../../docs/demo/motion-study/assets/adult-tickle.png) | 8 个挠痒反应姿势 | [挠痒](prompts/adult-tickle.txt) |
| [adult-walk-parts.png](../../docs/demo/motion-study/assets/adult-walk-parts.png) | 侧面身体、腿、手臂与尾部的独立部件 | [分层行走](prompts/adult-walk-parts.txt)、[肩部修订](prompts/adult-walk-parts-cap.txt) |

原始 PNG 直接复制到 D 盘，没有用程序重画或修改原图。`tools/prepare-motion-study.py` 读取透明轮廓，输出裁帧布局与位移估计数据；浏览器用原 PNG 纹理实时渲染过渡。下摆以下使用轮廓对应关系，避免将白色围裙/蕾丝误识别成靴口并向膝盖拉扯。源图哈希记录在 [manifest.json](../../docs/demo/motion-study/manifest.json)。

未采用的候选：16 格 TT 图集（底排截断、修订后比例偏短）；8 格行走图集（支撑脚与摆动腿仍不连贯）。保留这些尝试的提示词用于追溯，最终播放不引用候选图。

范围：先交 DeepSeek 真人原装浏览器 Demo。没有宣称这些新姿势已扩展到 Q 版、3D、其他角色或泳装/婚纱；原桌面程序仍运行 preview.13。
