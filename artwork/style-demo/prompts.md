# DeepSeek 风格 Demo

生成方式：内置 image_gen。身份参考为本项目 DeepSeek Q 版图集。两张图均为原创成年角色风格样张，未使用真人肖像或复制网络角色。原始输出保留在 Codex generated_images，项目图片位于各自角色目录。

网络风格参考：[MetaHuman 数字人](https://www.metahuman.com/en-US/create)、[Character Creator 3D 表情展示](https://www.reallusion.com/character-creator/hd-animation.html)。仅参考写实材质与三维塑形方式。

最终图片：

- `src/DesktopPet.App/Assets/Characters/deepseek-3d/portrait.png`
- `src/DesktopPet.App/Assets/Characters/deepseek-adult/portrait.png`

二者均为 1024×1536 RGBA 透明 PNG，保留生成器的 alpha 通道。项目图在下述首轮提示词后，使用内置工具清理了背景；未使用 Python 改图。

背景清理要求：只清除角色周围的背景光晕、背景颜色和投影，保留同一角色的五官、发型、服装、身体比例、姿势与构图，输出真正的透明背景。

## deepseek-3d

Use case: stylized-concept. Asset type: transparent full-body desktop companion style demo. Input image 1 is only an identity/color reference of the existing fictional DeepSeek whale-themed mascot, NOT a pose, proportions or grid reference. Create ONE clearly ADULT fictional woman aged about 28, normal adult body proportions, full body head to shoes visible, standing naturally in a relaxed three-quarter front view, slight friendly smile, right hand raised in a small friendly wave, left hand relaxed. Preserve recognizable cobalt-blue long wavy hair with cyan tips, ocean-blue eyes, white pleated hairband with a small cyan bow, subtle blue whale-fin hair accessories, and a tasteful small whale-shaped ornament. Her clothing is an elegant modest navy blue knee-length A-line dress with white rounded collar, long puff sleeves, fitted waist, small white apron panel with a discreet whale motif, opaque navy tights, and navy ankle boots. Adult facial bone structure and adult proportions, tasteful friendly desktop assistant. Only one subject, no panels, no duplicate poses, no text, no UI, no labels, no watermark, no props, no board held overhead. Entire silhouette unobstructed with transparent margins and genuine alpha background. Full-length vertical composition, soft studio illumination, sharp clean edges suitable for a desktop cutout. Style/medium: premium stylized 3D animated-feature character render. Approximately 6.8 heads tall, clearly mature adult, slightly simplified elegant facial forms and expressive eyes, carefully groomed chunky hair locks, satin fabric and soft physically based skin shading, appealing dimensional form. This is an adult animation character, not chibi, not a toddler, not a vinyl toy, not an anime drawing. No photographic skin pores; sophisticated sculpted 3D aesthetic.

## deepseek-adult

Use case: stylized-concept. Asset type: transparent full-body desktop companion style demo. Input image 1 is only an identity/color reference of the existing fictional DeepSeek whale-themed mascot, NOT a pose, proportions or grid reference. Create ONE clearly ADULT fictional woman aged about 28, normal adult body proportions, full body head to shoes visible, standing naturally in a relaxed three-quarter front view, slight friendly smile, right hand raised in a small friendly wave, left hand relaxed. Preserve recognizable cobalt-blue long wavy hair with cyan tips, ocean-blue eyes, white pleated hairband with a small cyan bow, subtle blue whale-fin hair accessories, and a tasteful small whale-shaped ornament. Her clothing is an elegant modest navy blue knee-length A-line dress with white rounded collar, long puff sleeves, fitted waist, small white apron panel with a discreet whale motif, opaque navy tights, and navy ankle boots. Adult facial bone structure and adult proportions, tasteful friendly desktop assistant. Only one subject, no panels, no duplicate poses, no text, no UI, no labels, no watermark, no props, no board held overhead. Entire silhouette unobstructed with transparent margins and genuine alpha background. Full-length vertical composition, soft studio illumination, sharp clean edges suitable for a desktop cutout. Style/medium: high-end PHOTOREALISTIC 3D DIGITAL HUMAN, like a carefully lit realistic game cinematic. Approximately 7.3 heads tall, convincingly adult age 28, realistic facial anatomy, natural-sized eyes, subtle pores and skin variation, individually visible groomed hair strands, plausible woven fabric stitching and folds, realistic hands and shoes. Original fictional face, not a real person's likeness. Clear distinction from stylized animation: realistic eyes, nose, lips, anatomy and material details. No cartoon outlines, not chibi, not doll-like, no oversized eyes.
