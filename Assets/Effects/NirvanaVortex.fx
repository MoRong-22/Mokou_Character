// 携带火焰的风暴：脚下汇聚 → 窄风柱向上成型 → 扩大加速 → 膨胀炸散。
// 前后表面各画一次；成型主体遮住玩家，起止阶段由焰流自然聚散。
// uTime/uDuration 使用秒；XNB 编译须指定 D3DCOMPILE_NO_PRESHADER (256)。
float uTime = 0.0;
float uDuration = 6.0;
float2 uSize = float2(1.0, 1.0);
// xy 为带留白画布到原绘制区域的比例，zw 为偏移，默认不留白。
float4 uCanvas = float4(1.0, 1.0, 0.0, 0.0);
float3 uDarkColor = float3(0.65, 0.06, 0.01);
float3 uLightColor = float3(1.0, 0.72, 0.20);
float uIntensity = 1.0;
float uSwirlSpeed = 1.0;
float uLayer = 1.0;

float VortexHash(float2 p)
{
    float3 q = frac(float3(p.x, p.y, p.x) * float3(0.1031, 0.1030, 0.0973));
    q += dot(q, q.yzx + 33.33);
    return frac((q.x + q.y) * q.z);
}

float VortexNoise(float2 p, float period)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float left = cell.x - floor(cell.x / period) * period;
    float right = left + 1.0;
    right -= floor(right / period) * period;
    return lerp(lerp(VortexHash(float2(left, cell.y)), VortexHash(float2(right, cell.y)), f.x),
                lerp(VortexHash(float2(left, cell.y + 1.0)), VortexHash(float2(right, cell.y + 1.0)), f.x), f.y);
}

// 螺旋坐标沿圆周转一圈时，两个分量同时回绕，避免背面出现接缝。
float WindNoise(float2 p, float2 period)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float2 a = cell - floor(cell / period) * period;
    float2 b = a + 1.0;
    b -= floor(b / period) * period;
    return lerp(lerp(VortexHash(a), VortexHash(float2(b.x, a.y)), f.x),
                lerp(VortexHash(float2(a.x, b.y)), VortexHash(b), f.x), f.y);
}

// 同一条母线连接根部、腰部与顶部。下端向外铺开后向上收拢，不再拼接独立风丝。
float WindRadius(float height)
{
    float waist = 0.32 + 0.20 * pow(saturate(abs(height - 0.46) / 0.54), 1.4);
    float root = 0.10 * (1.0 - smoothstep(0.0, 0.24, height));
    return waist + root;
}

// 统一沿程坐标：根部弯折段分配更多纹理长度，避免纹理在宽底部被横向拉长。
float WindPath(float height)
{
    return height + 0.18 * smoothstep(-0.02, 0.24, height);
}

// 返回圆周角、高度、横向位置与命中标记；前/后层使用同一曲面、同一投影。
float4 ProjectRibbon(float2 uv, float radiusScale, float sheet, float spin)
{
    float side = uLayer > 0.0 ? 1.0 : -1.0;
    float height = uv.y;
    float localX = 0.0;
    // 求出轻微俯视下的实际高度，根部也遵循这套映射。
    [unroll]
    for (int i = 0; i < 3; i++) {
        float sway = sin(height * 7.0 - spin * 1.5) * 0.024 * smoothstep(0.0, 0.8, height);
        float radius = WindRadius(height) * radiusScale * sheet;
        localX = (uv.x - sway) / max(radius, 0.001);
        float depth = side * sqrt(saturate(1.0 - localX * localX));
        height = uv.y + depth * radius * 0.13;
    }
    if (abs(localX) >= 1.0 || height <= -0.10 || height >= 1.10)
        return float4(0.0, 0.0, 0.0, 0.0);
    float turn = atan2(localX, side * sqrt(saturate(1.0 - localX * localX))) * 0.159154943;
    return float4(turn, height, localX, 1.0);
}

// 基础纹、高光、暗纹均从同一螺旋坐标生成。
// 每转一圈，圆周坐标变化为整数周期，接缝两侧的纹理和速度一致。
float4 RibbonTexture(float turn, float height, float spin)
{
    float path = WindPath(height);
    float movingTurn = turn - spin * 0.38;
    float movingPath = path - spin * 0.28;
    float spiral = movingPath * 2.2 + movingTurn;
    float warp = WindNoise(float2(movingTurn * 3.0, spiral * 3.0), float2(3.0, 3.0));
    float across = spiral * 2.0 + (warp - 0.5) * 0.45;
    float grain = WindNoise(float2(movingTurn * 8.0, across * 23.0), float2(8.0, 46.0));
    float breakup = WindNoise(float2(movingTurn * 5.0 + 4.7, spiral * 4.0), float2(5.0, 4.0));
    float bandPosition = frac(across);
    // 每条风带由长短错开的笔触组成，避免完整绕圈形成封闭的螺纹壳。
    // 圆周变化一圈时 floor(across) 增加 2，半周期错位仍然无缝。
    float along = frac(movingTurn * 3.0 + floor(across) * 0.5);
    float brush = smoothstep(0.025, 0.18, along) * (1.0 - smoothstep(0.55, 0.98, along));
    float width = 0.24 + brush * 0.24 + breakup * 0.12;
    float band = smoothstep(0.035, 0.14, bandPosition)
               * (1.0 - smoothstep(width, width + 0.13, bandPosition));
    // 宽风带留出断口；边缘侵蚀沿风向拉伸，不用细碎噪声铺满主体。
    float tear = smoothstep(0.25, 0.62, breakup * 0.72 + grain * 0.28);
    float highlight = (1.0 - smoothstep(0.02, 0.085, abs(bandPosition - width)))
                    * smoothstep(0.30, 0.70, breakup) * brush * (0.30 + grain * 0.70);
    float contrast = smoothstep(width + 0.02, width + 0.12, bandPosition)
                   * (1.0 - smoothstep(0.90, 0.99, bandPosition));
    return float4(band * brush * (0.35 + tear * 0.65), highlight,
                  contrast * smoothstep(0.26, 0.68, breakup) * brush, grain);
}

// 大小两层基础风带，亮边和暗部依附风带本身；大小层之间只做半径错开。
float4 WindRibbon(float2 uv, float radiusScale, float spin, float burst, float sheet)
{
    float4 surface = ProjectRibbon(uv, radiusScale, sheet, spin);
    if (surface.w == 0.0) return float4(0.0, 0.0, 0.0, 0.0);
    float4 tex = RibbonTexture(surface.x, surface.y, spin);
    float edge = 1.0 - smoothstep(0.56 + tex.x * 0.32, 0.82 + tex.x * 0.17, abs(surface.z));
    float root = smoothstep(0.0, 0.045, surface.y);
    float tip = 0.88 + tex.w * 0.18;
    float ends = root * (1.0 - smoothstep(tip - 0.12, tip, surface.y));
    float fragments = (1.0 - smoothstep(tex.w - 0.16, tex.w + 0.16, burst * 1.25 - 0.16))
                    * (1.0 - smoothstep(0.15, 1.0, burst));
    float outer = sheet > 1.0 ? 1.0 : 0.0;
    float baseAlpha = tex.x * lerp(0.95, 0.90, outer) * (0.55 + tex.w * 0.45);
    float darkAlpha = tex.z * lerp(0.30, 0.80, outer);
    float brightAlpha = (tex.y + tex.x * smoothstep(0.60, 0.90, tex.w) * 0.22) * lerp(0.45, 0.85, outer);
    float3 dark = saturate(uDarkColor);
    float3 light = saturate(uLightColor);
    float3 baseColor = lerp(dark, light, lerp(0.72, 0.45, outer) + tex.w * 0.24);
    float4 color = float4(baseColor * baseAlpha, baseAlpha);
    color = float4(dark * 0.36 * darkAlpha, darkAlpha) + color * (1.0 - darkAlpha);
    color = float4(light * brightAlpha, brightAlpha) + color * (1.0 - brightAlpha);
    color.rgb *= uLayer > 0.0 ? 1.0 : 0.65;
    color.rgb = saturate(color.rgb / max(color.a, 0.001) * max(uIntensity, 0.0)) * color.a;
    return color * edge * ends * fragments;
}

// 小范围暖光承托基础风带，暗带从前方掠过时会遮住这层光。
float4 RibbonCore(float2 uv, float radiusScale, float spin, float burst)
{
    float radius = WindRadius(uv.y) * radiusScale * 0.65;
    float x = uv.x / max(radius, 0.001);
    if (abs(x) >= 1.6) return float4(0.0, 0.0, 0.0, 0.0);
    float4 tex = RibbonTexture(0.0, uv.y, spin);
    float alpha = exp2(-x * x * 2.0) * (0.45 + tex.x * 0.35)
        * smoothstep(-0.01, 0.05, uv.y) * (1.0 - smoothstep(0.70, 1.0, uv.y))
        * (1.0 - smoothstep(0.0, 0.60, burst));
    float3 color = lerp(saturate(uDarkColor), saturate(uLightColor), 0.90);
    color *= uLayer > 0.0 ? 1.0 : 0.65;
    return float4(saturate(color * max(uIntensity, 0.0)) * alpha, alpha);
}

// 显现只沿高度向上推进，已经形成的风带不会被竖直拉伸。
float RibbonFormation(float2 uv, float formation, float spin)
{
    if (formation <= 0.0) return 0.0;
    if (formation >= 1.0) return 1.0;
    float front = lerp(0.015, 1.10, formation);
    if (uv.y >= front) return 0.0;
    if (uv.y <= front - 0.10) return 1.0;
    float noise = VortexNoise(float2(uv.x * 8.0 + spin, uv.y * 8.0 - spin), 64.0);
    float edge = (front - uv.y) / 0.10;
    return smoothstep(0.12 + noise * 0.4, 0.35 + noise * 0.5, edge);
}

float4 VortexBody(float2 uv, float spin, float formation, float radiusScale, float burst)
{
    float mask = RibbonFormation(uv, formation, spin);
    if (mask <= 0.0) return float4(0.0, 0.0, 0.0, 0.0);
    float travel = 1.0 - (1.0 - burst) * (1.0 - burst);
    float2 model = float2(uv.x / (1.0 + travel * 0.65), (uv.y - 0.43) / (1.0 + travel * 0.10) + 0.43);
    float4 small = WindRibbon(model, radiusScale, spin, burst, 0.78);
    float4 large = WindRibbon(model, radiusScale, spin, burst, 1.10);
    float4 core = RibbonCore(model, radiusScale, spin, burst);
    float4 result;
    if (uLayer > 0.0) {
        result = small + core * (1.0 - small.a);
        result = large + result * (1.0 - large.a);
    } else {
        result = small + large * (1.0 - small.a);
        result = core + result * (1.0 - core.a);
    }
    // 下部风眼仍需遮住角色，覆盖范围随同一母线缩放，不影响外围风带间隙。
    float cover = (1.0 - smoothstep(0.45, 1.0, abs(uv.x) / ((0.18 + max(uv.y, 0.0) * 0.40) * radiusScale)))
        * (1.0 - smoothstep(0.24, 0.44, uv.y)) * smoothstep(-0.015, 0.0, uv.y)
        * (1.0 - smoothstep(0.0, 0.60, burst));
    if (uLayer > 0.0) {
        // 给覆盖核心自己的颜色，避免把几乎透明的颜色除以 Alpha 后放大成硬边块。
        float4 coreTex = RibbonTexture(0.0, uv.y, spin);
        float3 coverColor = lerp(saturate(uDarkColor), saturate(uLightColor), 0.50 + coreTex.x * 0.30);
        float4 coverLayer = float4(saturate(coverColor * max(uIntensity, 0.0)) * cover, cover);
        result += coverLayer * (1.0 - result.a);
    }
    return result * mask;
}


// smoothstep 速度曲线的积分，改变转速时保持相位连续，避免突然换方向或跳帧。
float SpeedIntegral(float progress, float start, float end)
{
    float span = max(end - start, 0.001);
    float x = saturate((progress - start) / span);
    return span * (x * x * x - 0.5 * x * x * x * x) + max(progress - end, 0.0);
}

float4 VortexBurst(float2 uv, float burst, float spin)
{
    if (burst <= 0.0 || burst >= 1.0 || uLayer < 0.0)
        return float4(0.0, 0.0, 0.0, 0.0);
    // 炸散主要向两侧抛出，避免超出绘制画布后在脚下出现平直截断。
    float2 q = float2(uv.x, (uv.y - 0.44) * 2.40);
    float angle = atan2(q.y, q.x);
    float turn = angle * 0.159154943;
    float radius = length(q);
    float travel = 1.0 - pow(1.0 - burst, 2.0);
    float wind = VortexNoise(float2((turn - spin * 0.07) * 18.0, burst * 3.0), 18.0);
    float fine = VortexNoise(float2((turn - spin * 0.12) * 36.0, radius * 7.0 - burst * 8.0), 36.0);
    float frontRadius = 0.22 + travel * 1.15 + (wind - 0.5) * 0.14;
    float band = 1.0 - smoothstep(0.015, 0.16, abs(radius - frontRadius));
    float shards = smoothstep(0.35 + burst * 0.22, 0.78, wind * 0.65 + fine * 0.35);
    float alpha = band * shards * smoothstep(0.0, 0.16, burst)
                * (1.0 - smoothstep(0.30, 1.0, burst)) * 0.90;
    float3 color = lerp(saturate(uDarkColor), saturate(uLightColor), saturate(fine * 1.3));
    return float4(saturate(color * max(uIntensity, 0.0)) * alpha, alpha);
}

float4 NirvanaVortexPS(float2 texCoord : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    if (uDuration <= 0.0 || uTime <= 0.0 || uTime >= uDuration)
        return float4(0.0, 0.0, 0.0, 0.0);
    float progress = saturate(uTime / uDuration);
    // 6 秒：0～1.2 秒脚下汇聚，约 0.7～4 秒窄风柱向上成型；
    // 4～5.15 秒略微扩大并加速，5.15～5.7 秒再膨胀，最后 0.3 秒炸散。
    // 其他总时长（包括 3 秒吟唱）使用同一组阶段比例。
    float formationEnd = 2.0 / 3.0;
    float swellStart = 5.15 / 6.0;
    float burstStart = 0.95;
    float formation = smoothstep(0.0, formationEnd, progress);
    float burst = saturate((progress - burstStart) / (1.0 - burstStart));
    float widen = smoothstep(formationEnd, swellStart, progress);
    float swell = smoothstep(swellStart, burstStart, progress);
    float radiusScale = 0.56 + 0.22 * widen + 0.32 * swell;
    float spin = uSwirlSpeed * uDuration * (0.22 * progress
        + 0.38 * SpeedIntegral(progress, 0.20, formationEnd)
        + 0.85 * SpeedIntegral(progress, formationEnd, swellStart)
        + 0.65 * SpeedIntegral(progress, swellStart, burstStart));
    float2 baseUV = texCoord * uCanvas.xy + uCanvas.zw;
    float2 uv = float2((baseUV.x - 0.5) * 2.0, 1.0 - baseUV.y) / max(uSize, 0.001);

    float4 body = VortexBody(uv, spin, formation, radiusScale, burst);

    float4 shards = VortexBurst(uv, burst, spin);
    float4 result = shards + body * (1.0 - shards.a);
    return float4(result.rgb * vertexColor.rgb * vertexColor.a, result.a * vertexColor.a);
}

technique NirvanaVortex
{
    pass NirvanaVortexPass
    {
        PixelShader = compile ps_3_0 NirvanaVortexPS();
    }
}
