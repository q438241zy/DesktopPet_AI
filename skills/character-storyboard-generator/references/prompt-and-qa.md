# Prompt and QA rules

## Prompt assembly

Assemble each manifest prompt in this order:

1. input role and character identity lock (reference ONLY the single approved identity image);
2. target medium: preserved animation design or approved chibi design from a photo;
3. art style lock: Japanese chibi anime style (Himouto! Umaru-chan vein — 2-3 heads tall, large expressive eyes, thin clean line art, flat cel colors);
4. exact pose prompt;
5. exact expression prompt;
6. full-body composition and floor-contact requirements;
7. transparent/chroma-key background instruction;
8. watermark-free requirement;
9. invariants and avoid list.

Never attach more than one reference image to a generation call. Multiple references cause style and identity drift.

Do not rely on expression words alone. Include the observable eyes, eyebrows, mouth, blush, tears, or sweat cues from the expression catalog.

## Calibration set

Use poses `P01`, `P06`, `P16`, `P17`, `P20`, and `P22` with expressions `E01`, `E02`, `E03`, `E05`, `E22`, and `E21`. Inspect identity drift before expanding the batch.

## Visual QA checklist

- one character only;
- identity, hairstyle, outfit, palette, accessories, and proportions match the approved reference;
- requested pose is anatomically readable;
- requested expression matches eyes, brows, and mouth;
- no extra or fused limbs, hands, feet, or fingers;
- no clipped body parts; at least 5% visual padding around the silhouette;
- floor poses include the full floor-contact silhouette without drawing a floor scene;
- no text, watermark, logo, signature, speech bubble, panel border, or unrequested prop — including any watermark carried over from the source image;
- art style matches the fixed Japanese chibi anime style (Umaru-chan vein) and is consistent with every previously accepted image in the set;
- transparent output has an alpha channel, transparent corners, and no chroma fringe;
- dimensions match project settings;
- filename and location match the manifest exactly.
- for a minor or age-unknown subject: no sexualized proportions, revealing clothing, seductive gaze, fetish styling, provocative camera angle, body emphasis, or suggestive framing.

## Retry guidance

Retry one failure at a time. Preserve the source and all invariants, then name the retry with the next variant. Record the rejected reason in the manifest or audit notes.

Common targeted fixes:

- style drift: check that the generation call attached only the single approved identity reference (extra reference images are the top cause), then restate the chibi style lock;
- watermark remnants: re-isolate the character from the source excluding the watermark region, then regenerate;
- identity drift: strengthen hairstyle, face, clothing, and palette invariants;
- pose ambiguity: describe limb placement and weight support explicitly;
- expression ambiguity: specify brows, gaze, eyelids, mouth corners, tears, or blush;
- clipped silhouette: request more padding and a slightly wider framing;
- dirty alpha: regenerate on a flat key color and repeat matte/despill validation.
