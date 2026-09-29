# DeepSeek 新动作样例 · 2026-09-29

使用内置 `image_gen` 工具生成与修订透明 PNG；没有调用付费 API/CLI。角色参考均来自本项目 `deepseek-adult/motions/dance-base-atlas.png` 的原装列。角色为现有虚构成年形象，不采用女团成员的真人外貌。

舞蹈参考：[TWICE 官方《TT》练习视频](https://www.youtube.com/watch?v=9uypQGzzhns)、[JYP 官方视频目录](https://twice.jype.com/video)。检索核实了官方来源；未声称完成视频逐帧观看或动作捕捉。Demo 只以脸旁向下的手势为灵感制作简化片段，未嵌入原曲、视频或团体 Logo。比心与招手为原创短动作。

本轮新增参考：[aespa 官方《Next Level》练习](https://www.youtube.com/watch?v=IMpXNQ-MLT4)、[IVE 官方《LOVE DIVE》练习幕后](https://www.youtube.com/watch?v=JICPGlEkD4A)。核实官方来源，折臂与镜面手势为简化演绎；没有声称逐帧复刻完整编舞。

| 最终图集 | 内容 | 最终提示词 |
|---|---|---|
| [adult-idle.png](../../docs/demo/motion-study/assets/adult-idle.png) | 5 个站立姿势，另加伸懒腰、掩嘴打哈欠、小幅招手 | [idle](prompts/adult-idle.txt) |
| [adult-idle-extra.png](../../docs/demo/motion-study/assets/adult-idle-extra.png) | 新增托腮、抱臂、搭腰、理袖、抬望；第六格为参考站姿 | [新增待机](prompts/idle-extra.txt) |
| [adult-dance-tt.png](../../docs/demo/motion-study/assets/adult-dance-tt.png) | 8 个完整 TT 手势关键姿势 | [TT 大图](prompts/adult-dance-tt-large.txt) |
| [adult-dance-step.png](../../docs/demo/motion-study/assets/adult-dance-step.png) | 8 个比心舞姿势 | [比心](prompts/adult-dance-heart.txt) |
| [adult-dance-wave.png](../../docs/demo/motion-study/assets/adult-dance-wave.png) | 8 个侧步招手姿势，播放顺序按左右手连续性重排 | [招手](prompts/adult-dance-wave.txt) |
| [adult-tickle.png](../../docs/demo/motion-study/assets/adult-tickle.png) | 8 个挠痒反应姿势 | [挠痒](prompts/adult-tickle.txt) |
| [adult-walk-parts.png](../../docs/demo/motion-study/assets/adult-walk-parts.png) | 侧面身体、腿、手臂与尾部的独立部件 | [分层行走](prompts/adult-walk-parts.txt)、[肩部修订](prompts/adult-walk-parts-cap.txt) |
| [adult-dance-next-level.png](../../docs/demo/motion-study/assets/adult-dance-next-level.png) | 折臂舞蹈八张参考姿势 | [折臂](prompts/dance-next-level.txt) |
| [adult-dance-next-level-inbetweens.png](../../docs/demo/motion-study/assets/adult-dance-next-level-inbetweens.png) | 八张补画过渡与收回姿势 | [折臂过渡](prompts/dance-next-level-inbetweens.txt) |
| [adult-dance-love-dive.png](../../docs/demo/motion-study/assets/adult-dance-love-dive.png) | 镜面舞蹈八张参考姿势 | [镜面](prompts/dance-love-dive.txt) |
| [adult-dance-love-dive-inbetweens.png](../../docs/demo/motion-study/assets/adult-dance-love-dive-inbetweens.png) | 八张补画过渡与收回姿势 | [镜面过渡](prompts/dance-love-dive-inbetweens.txt) |
| [adult-dance-front-parts.png](../../docs/demo/motion-study/assets/adult-dance-front-parts.png) | 正面身体、手臂、腿、张开手掌；用于五款舞蹈的实时分层关节动画 | [正面分层](prompts/adult-dance-front-parts.txt) |
| [adult-dance-hand-gestures.png](../../docs/demo/motion-study/assets/adult-dance-hand-gestures.png) | TT 左右向下指尖、完整双手比心、Next Level 向上指尖 | [专用手势](prompts/dance-hand-gestures.txt) |

原始 PNG 直接复制到 D 盘，没有用程序重画或修改原图。`tools/prepare-motion-study.py` 读取透明轮廓，输出裁帧布局与位移估计数据；浏览器用原 PNG 纹理实时渲染过渡。下摆以下使用轮廓对应关系，避免将白色围裙/蕾丝误识别成靴口并向膝盖拉扯。源图哈希记录在 [manifest.json](../../docs/demo/motion-study/manifest.json)。

五款舞蹈的手部参考坐标在 [hand-landmarks.json](hand-landmarks.json)，脚部及头部坐标从原图轮廓和颜色区域测量；比心的两处交叉脚无法分为两个透明连通块，补记交替提跟的位置。16 张折臂参考、15 张镜面参考构成各 8 秒的连续轨迹；镜面样例跳过伸手幅度过大的原始第 1 格。五款均直接驱动正面分层 PNG 的肩、肘、腕、掌心、头部倾斜及腿，不用整图位移叠加；关闭「连贯动画」可对照原始姿势。TT、比心与向上指尖使用专用手势图。去掉每次换姿势的 13% 首尾停顿，用真实拍点时长计算共享的关节速度，转向处自然减速。

未采用的候选：16 格 TT 图集（底排截断、修订后比例偏短）；8 格行走图集（支撑脚与摆动腿仍不连贯）。保留这些尝试的提示词用于追溯，最终播放不引用候选图。

范围：先交 DeepSeek 真人原装浏览器 Demo。没有宣称这些新姿势已扩展到 Q 版、3D、其他角色或泳装/婚纱；原桌面程序仍运行 preview.13。
