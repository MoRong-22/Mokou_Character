// 焦边残符：固定纸形、轻微撕裂和焦痕；只在技能释放期间让边缘向上燃烧。
float uTime;
float uSeed;
float uActive;
float uHovered;
float uCooldown;
float2 uCardSize;
float uPadding;
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
    return lerp(lerp(Hash(c), Hash(c + float2(1,0)), f.x),
        lerp(Hash(c + float2(0,1)), Hash(c + 1.0), f.x), f.y);
}
float Box(float2 p, float2 b)
{
    float2 q = abs(p) - b;
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);
}
float PaperDistance(float2 p, float2 b)
{
    // seed 只改变各张纸的缺口，纸形不随时间抖动，也不随冷却缩小。
    float2 chip = float2(Noise(float2(p.y * 0.21, uSeed * 3.7 + 1.0)),
        Noise(float2(p.x * 0.27, uSeed * 5.1 + 8.0)));
    return Box(p, b - 0.8 - chip * 2.6);
}
float4 CardPS(float4 tint : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 canvas = uCardSize + uPadding * 2.0;
    float2 p = (uv - 0.5) * canvas, b = uCardSize * 0.5;
    float d = PaperDistance(p, b);
    float paper = 1.0 - smoothstep(-0.6, 0.6, d);
    float grain = Noise(p * float2(0.75, 1.3) + uSeed * 9.0);
    float vignette = saturate(length(p / b) * 0.78);
    float3 paperColor = lerp(float3(0.93, 0.85, 0.70), float3(0.73, 0.58, 0.39), vignette * vignette);
    paperColor *= 0.97 + grain * 0.045;
    float scorch = smoothstep(-6.0, 0.7, d) * (0.6 + grain * 0.4);
    paperColor = lerp(paperColor, float3(0.23, 0.14, 0.09), scorch * 0.85);
    float innerLine = 1.0 - smoothstep(0.3, 0.8, abs(Box(p, b - 7.0)));
    paperColor = lerp(paperColor, float3(0.37, 0.21, 0.14), innerLine * 0.27);
    paperColor += uHovered * float3(0.045, 0.035, 0.020);
    float cool = step((1.0 - uCooldown) * uCardSize.y, p.y + b.y)
        * step(0.0001, uCooldown) * (1.0 - uActive);
    paperColor = lerp(paperColor, paperColor * float3(0.45, 0.49, 0.51), cool * 0.70);

    // 流场沿屏幕上方运动，顶部火舌稍长，两侧只保留细小的沿边火焰。
    float2 flow = p * float2(0.16, 0.14) + float2(uSeed * 4.1, uTime * 3.3);
    flow.x += (Noise(flow * 0.57) - 0.5) * 1.4;
    float n = Noise(flow) * 0.67 + Noise(flow * 2.07 + 3.7) * 0.33;
    float top = 1.0 - smoothstep(-b.y + 1.0, -b.y + 12.0, p.y);
    float flameHeight = 2.0 + (5.0 + top * 20.0) * smoothstep(0.24, 0.78, n);
    float outer = max(d, 0.0);
    float tongues = (1.0 - smoothstep(flameHeight * 0.3, flameHeight, outer))
        * smoothstep(-2.0, 0.8, d);
    float core = exp(-abs(d + 0.7) * 1.1);
    float heat = saturate(1.0 - outer / max(flameHeight, 1.0));
    float3 fire = lerp(uDarkColor, uLightColor, smoothstep(0.05, 0.9, heat));
    fire = lerp(fire, lerp(uLightColor, float3(1,1,1), 0.48), core * 0.65);
    float fireAlpha = saturate(tongues * 0.90 + core * 0.78) * uActive;
    float shadow = (1.0 - smoothstep(-1.0, 5.0, Box(p - float2(2,3), b - 1.0))) * 0.40;
    float alpha = paper + shadow * (1.0 - paper);
    float3 rgb = paperColor * paper + float3(0.025,0.018,0.014) * shadow * (1.0 - paper);
    rgb = fire * fireAlpha + rgb * (1.0 - fireAlpha);
    alpha = fireAlpha + alpha * (1.0 - fireAlpha);
    float clip = smoothstep(0.0, 2.0, min(min(uv.x, 1.0 - uv.x) * canvas.x,
        min(uv.y, 1.0 - uv.y) * canvas.y));
    return float4(rgb, alpha) * clip * tint;
}
technique SpellCard { pass AshAndFlame { PixelShader = compile ps_3_0 CardPS(); } }
