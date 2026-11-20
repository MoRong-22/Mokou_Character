// RT 场景合成：局部上升热浪 + 全屏燃烧消散，两个效果在一个 pass 内完成。
// s0 是当前场景，输出写入另一张 RT。保留场景 Alpha，不把屏幕挖成透明黑块。
// uTime / uDuration 使用秒，时间为 0 或结束时严格返回原图。
// 编译 XNB 时使用 D3DCOMPILE_NO_PRESHADER (256)，避免 FNA 对旧式预计算表达式的兼容问题。
sampler uImage0 : register(s0);
float uTime;
float uDuration = 5.0;
float2 uViewportSize;
float2 uSourceSize;
float2 uCenterPixels;
float2 uHalfSizePixels;
float uStrengthPixels = 4.0;
float uBurnStrength = 1.0;
float uBurnSeed = 1.0;
float3 uDarkColor = float3(0.65, 0.06, 0.01);
float3 uLightColor = float3(1.0, 0.72, 0.20);

float BurnHash(float2 p)
{
    float3 q = frac(float3(p.x, p.y, p.x) * float3(0.1031, 0.1030, 0.0973));
    q += dot(q, q.yzx + 33.33);
    return frac((q.x + q.y) * q.z);
}

float BurnNoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(BurnHash(cell), BurnHash(cell + float2(1.0, 0.0)), f.x),
                lerp(BurnHash(cell + float2(0.0, 1.0)), BurnHash(cell + 1.0), f.x), f.y);
}

float4 NirvanaHeatWavePS(float2 uv : TEXCOORD0) : COLOR0
{
    float4 original = tex2D(uImage0, uv);
    if (uDuration <= 0.0) return original;
    if (uTime <= 0.0) return original;
    if (uTime >= uDuration) return original;

    float progress = saturate(uTime / uDuration);
    float2 viewport = max(uViewportSize, 1.0);
    float2 pixels = uv * viewport;
    float2 local = (pixels - uCenterPixels) / max(uHalfSizePixels, 1.0);
    float radiusSquared = dot(local, local);
    if (radiusSquared >= 1.0 && uBurnStrength <= 0.0)
        return original;

    float life = smoothstep(0.0, 0.14, progress) * (1.0 - smoothstep(0.86, 1.0, progress));
    float heatMask = 1.0 - smoothstep(0.30, 1.0, radiusSquared);
    // 不规则上升热流与龙卷共享阵风节奏，让周围画面出现吹动、翻卷的折射。
    float windHeight = saturate((1.0 - local.y) * 0.5);
    float gust = sin(windHeight * 7.5 - uTime * 4.4)
               + sin(windHeight * 16.0 - uTime * 7.1) * 0.35;
    float turbulence = BurnNoise(local * float2(4.0, 7.0)
        + float2(gust * 0.3, uTime * 3.0)) * 2.0 - 1.0;
    float curl = sin(local.y * 12.0 + uTime * 6.0 + turbulence * 3.0 + local.x * 4.0);
    float2 offsetPixels = float2(gust * 0.65 + turbulence * 0.70 + curl * 0.35,
        sin(local.x * 8.0 + uTime * 4.0 + turbulence * 2.0) * 0.30 - abs(curl) * 0.20)
        * max(uStrengthPixels, 0.0) * heatMask * life;

    float coverage = 0.0;
    float edge = 0.0;
    float grain = 0.0;
    // 烧屏集中在结束前 0.6 秒，3 秒吟唱和 5 秒重生使用相同的快速过渡。
    float burnDuration = min(0.60, uDuration);
    float burnTime = uTime - (uDuration - burnDuration);
    if (uBurnStrength > 0.0 && burnTime >= 0.0)
    {
        // 固定的多层深度噪声：阈值抬升时，多个低点同时烧开，再汇合。
        // 不含角色中心或径向距离；同一次播放不移动噪声，烧开的地方不会重新盖回去。
        float2 seedOffset = float2(BurnHash(float2(uBurnSeed, 17.3)),
                                  BurnHash(float2(31.7, uBurnSeed))) * 64.0;
        float2 noiseUV = float2(uv.x * viewport.x / viewport.y, uv.y) * 3.0 + seedOffset;
        float coarse = BurnNoise(noiseUV);
        float detail = BurnNoise(noiseUV * 2.07 + float2(13.1, 7.4));
        grain = BurnNoise(noiseUV * 5.13 + float2(2.8, 19.6));
        float field = coarse * 0.70 + detail * 0.23 + grain * 0.07;
        // 0.16 秒盖满，停留 0.04 秒，随后 0.40 秒多点烧开。
        float coverFront = lerp(-0.08, 1.08, saturate(burnTime / (burnDuration * 0.2666667)));
        float clearFront = lerp(-0.08, 1.08,
            saturate((burnTime - burnDuration / 3.0) / (burnDuration * 0.6666667)));
        float cover = 1.0 - smoothstep(coverFront - 0.018, coverFront + 0.018, field);
        float cleared = 1.0 - smoothstep(clearFront - 0.018, clearFront + 0.018, field);
        // 覆盖区完全遮挡场景；用烧入/烧出的边界控制消融，不整体半透明淡入淡出。
        coverage = cover * (1.0 - cleared) * saturate(uBurnStrength);
        float coverEdge = (1.0 - smoothstep(0.007, 0.038, abs(field - coverFront))) * (1.0 - cleared);
        float clearEdge = (1.0 - smoothstep(0.007, 0.038, abs(field - clearFront))) * cover;
        edge = max(coverEdge, clearEdge) * saturate(uBurnStrength);
        offsetPixels += float2(sin(pixels.y * 0.07 + uTime * 11.0),
                              cos(pixels.x * 0.06 + uTime * 8.0) * 0.35) * edge * 3.5;
    }

    float2 halfPixel = 0.5 / max(uSourceSize, 1.0);
    float4 scene = tex2D(uImage0, clamp(uv + offsetPixels / viewport, halfPixel, 1.0 - halfPixel));
    float3 ember = saturate(uDarkColor) * (0.06 + grain * 0.12);
    // 焦燃幕覆盖到的位置完全不透出场景，烧开后才露出实时画面。
    float3 color = scene.rgb * (1.0 - coverage) + ember * scene.a * coverage;
    float3 edgeColor = lerp(saturate(uDarkColor), saturate(uLightColor), saturate(edge * 1.3));
    color += edgeColor * edge * 0.85 * scene.a;
    return float4(min(saturate(color), scene.a), scene.a);
}

technique NirvanaHeatWave
{
    pass NirvanaHeatWavePass
    {
        PixelShader = compile ps_3_0 NirvanaHeatWavePS();
    }
}
