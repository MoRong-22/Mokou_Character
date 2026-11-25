// 技能栏外圈火焰。距离按 UI 像素计算，中心完全透明，输出预乘 Alpha。
float uTime;
float uProgress;
float2 uBarSize;
float2 uPadding;
float3 uDarkColor;
float3 uLightColor;

float Hash(float2 p)
{
    float3 q = frac(float3(p.x, p.y, p.x) * float3(0.1031, 0.1030, 0.0973));
    q += dot(q, q.yzx + 33.33);
    return frac((q.x + q.y) * q.z);
}
float Noise(float2 p)
{
    float2 c = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(Hash(c), Hash(c + float2(1, 0)), f.x),
                lerp(Hash(c + float2(0, 1)), Hash(c + 1.0), f.x), f.y);
}
float Fbm(float2 p)
{
    return Noise(p) * 0.58 + Noise(p * 2.03 + 3.7) * 0.28 + Noise(p * 4.07 + 8.1) * 0.14;
}
float RoundedBox(float2 p, float2 halfSize, float radius)
{
    float2 q = abs(p) - halfSize + radius;
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
}

// 从下边中央开始沿左侧、上边、右侧绕一周；空进度保留很暗的完整框线。
float Perimeter(float2 p, float2 b)
{
    float2 c = clamp(p, -b, b);
    float s;
    if (abs(p.x) - b.x > abs(p.y) - b.y)
        s = p.x > 0.0 ? 2.0 * b.x + c.y + b.y : 4.0 * b.x + 2.0 * b.y + b.y - c.y;
    else
        s = p.y < 0.0 ? c.x + b.x : 2.0 * b.x + 2.0 * b.y + b.x - c.x;
    float length = 4.0 * (b.x + b.y);
    return frac((s - (3.0 * b.x + 2.0 * b.y) + length) / length);
}

float4 FlamePS(float4 tint : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 canvas = uBarSize + uPadding * 2.0;
    float2 p = (uv - 0.5) * canvas;
    float2 b = uBarSize * 0.5 + 6.0;
    float d = RoundedBox(p, b, 9.0);
    float progress = saturate(uProgress);
    float arc = Perimeter(p, b);
    float lit = (1.0 - smoothstep(progress - 0.004, progress + 0.004, arc)) * step(0.0001, progress);
    lit = lerp(lit, 1.0, step(0.9999, progress));

    // 噪声向上流动，火舌上长下短，两侧也出现上卷碎焰。
    float top = 1.0 - smoothstep(-b.y, -b.y + 12.0, p.y);
    float side = smoothstep(b.x - 12.0, b.x + 1.0, abs(p.x));
    float bottom = smoothstep(b.y - 4.0, b.y + 2.0, p.y);
    float2 flow = float2(p.x * 0.095, p.y * 0.09 + uTime * 2.7);
    float warp = Noise(flow * 0.7 + 7.1);
    float n = Fbm(flow + float2((warp - 0.5) * 1.5, 0.0));
    float fine = Noise(flow * 2.7 + float2(0.0, uTime * 2.0));
    float plume = (5.0 + top * 18.0 + side * 6.0 - bottom * 2.0) * (0.70 + progress * 0.30);
    float height = 2.0 + plume * smoothstep(0.25, 0.78, n);
    float flame = 1.0 - smoothstep(height * 0.60, height, max(0.0, d));
    flame *= smoothstep(-3.0, -0.5, d);
    float core = exp(-abs(d) * 0.64);
    float heat = saturate(0.92 - max(0.0, d) / max(1.0, height) + (fine - 0.5) * 0.17);
    float3 color = lerp(uDarkColor, uLightColor, smoothstep(0.12, 0.85, heat));
    color = lerp(color, lerp(uLightColor, float3(1, 1, 1), 0.48), core * 0.4);
    float alpha = saturate(flame * 0.92 + core * 0.30) * lit;
    float glow = exp(-max(0.0, d) * 0.20) * smoothstep(-3.0, 0.0, d) * lit * 0.10;

    float dormant = exp(-abs(d) * 1.25) * 0.26;
    float3 rgb = color * alpha + uDarkColor * glow + lerp(float3(0.08, 0.09, 0.12), uDarkColor, 0.15) * dormant * (1.0 - alpha);
    alpha = saturate(alpha + glow + dormant * (1.0 - alpha));
    // 技能内容区域留空，底部不超出技能栏预留的20像素。
    float hole = smoothstep(-1.0, 1.0, RoundedBox(p, uBarSize * 0.5 + 3.0, 5.0));
    float bottomClip = 1.0 - smoothstep(uBarSize.y * 0.5 + 15.0, uBarSize.y * 0.5 + 19.0, p.y);
    float canvasClip = smoothstep(0.0, 2.0, min(min(uv.x, 1.0 - uv.x) * canvas.x, min(uv.y, 1.0 - uv.y) * canvas.y));
    return float4(rgb, alpha) * (hole * bottomClip * canvasClip) * tint;
}
technique BurningBorder { pass Fire { PixelShader = compile ps_3_0 FlamePS(); } }
