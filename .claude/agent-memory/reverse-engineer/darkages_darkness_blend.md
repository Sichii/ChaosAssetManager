---
name: Dark Ages Darkness/Light Overlay Blending Algorithm
description: Complete reverse-engineered details of how Darkages.exe combines darkness layers with HEA per-pixel light data to produce the final overlay, including the exact per-pixel blend formula
type: project
---

## Dark Ages Darkness/Light Overlay - Reverse Engineering Results

### Key Binary Addresses (Darkages.exe)
- `FUN_00604030` (0x604030) - Per-pixel blend function (16-bit 565 mode)
- `FUN_006040c0` (0x6040c0) - Per-pixel blend function (16-bit 555 mode)
- `FUN_005ce350` (0x5ce350) - Main darkness draw function, calls blend per-pixel
- `FUN_005c8760` (0x5c8760) - Alpha plane fill from HEA data
- `FUN_005c8540` (0x5c8540) - Writes per-pixel alpha from HEA RLE data with floor clamping
- `FUN_005ef360` (0x5ef360) - Server packet handler that sets up darkness params
- `FUN_006036b0` (0x6036b0) - LightBitmap max-blit (light source stamp)
- `FUN_006037f0` (0x6037f0) - Opaque alpha blit (565, when darkness color=0)

### Blend Formula (565 mode, at 0x604030)
```
blend(screenPixel, darknessColor, alphaValue):
    invAlpha = 0x20 - alphaValue    // (32 - alpha)

    // Split channels using 565 masks
    RB_screen = screenPixel & 0xF81F    // Red(5) + Blue(5)
    G_screen  = screenPixel & 0x07E0    // Green(6)
    RB_dark   = darknessColor & 0xF81F
    G_dark    = darknessColor & 0x07E0

    // Linear interpolation with >>5 (divide by 32)
    RB_out = ((alphaValue * RB_screen + invAlpha * RB_dark) >> 5) & 0xF81F
    G_out  = ((alphaValue * G_screen  + invAlpha * G_dark)  >> 5) & 0x07E0

    return RB_out | G_out
```

### Alpha Value Semantics
- Alpha 32 (0x20) = fully lit (100% screen pixel, 0% darkness)
- Alpha 0 = fully dark (0% screen pixel, 100% darkness color)
- This is LINEAR INTERPOLATION: `result = lerp(darknessColor, screenPixel, alpha/32)`

### HEA Data Flow
1. Server sends ambient light params: (alpha, R, G, B, loadHEA_flag)
2. `FUN_005ef360` processes: if alpha < 32 and loadHEA flag set, loads `%06d.hea` file
3. R,G,B converted to 16-bit color via function pointer, stored at object+0x202
4. Alpha byte stored at object+0x200

### Per-Pixel Alpha Computation (FUN_005c8540)
The HEA data is RLE-encoded per row. Each entry is a ushort: `[run_length:high_byte][alpha:low_byte]`.
For each pixel:
```
if heaPixelAlpha <= darknessAlpha:
    effectiveAlpha = darknessAlpha  // floor clamp
else:
    effectiveAlpha = heaPixelAlpha  // use HEA value as-is
```
This is equivalent to: `effectiveAlpha = max(darknessAlpha, heaPixelAlpha)`

### Light Source Stamps (LightBitmap via FUN_006036b0)
Light sources (torches, spells) are composited onto the alpha plane via a MAX operation:
```
for each pixel:
    if lightBitmapAlpha > alphaPlaneAlpha:
        alphaPlaneAlpha = lightBitmapAlpha
```
This is pure `max()` - light sources can only increase brightness, never decrease it.

### Full Pipeline Summary
1. AlphaPlane buffer allocated (byte per pixel, values 0-32)
2. If no HEA data: fill entire buffer with `darknessAlpha` (object+0x200)
3. If HEA data exists: decode RLE, write per-pixel alpha, clamped to `max(heaValue, darknessAlpha)`
4. Light source bitmaps (mask1XX.epf) are stamped onto alpha plane via `max()`
5. Final composite: for each screen pixel, `blend(screenPixel, darknessColor16, alphaPlaneValue)`
6. Blend uses linear interpolation: `result = (alpha * screen + (32-alpha) * darkColor) >> 5`

**Why:** Understanding the exact blend algorithm is needed for ChaosAssetManager's HeaEditor to accurately preview darkness overlays.
**How to apply:** When rendering darkness preview, use this exact formula. The key insight is that alpha=32 means fully lit and alpha=0 means fully dark (showing the darkness color), and light grid values act as per-pixel alpha that is floor-clamped to the ambient darkness alpha.
