// Resurrection 专用局部焚身与重现。输出预乘透明，无贴图、无屏幕滤镜。
float uTime;
float uProgress;
float uArriving;
float3 uDarkColor = float3(0.65, 0.06, 0.01);
float3 uLightColor = float3(1.0, 0.72, 0.20);

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
    return Noise(p) * 0.57 + Noise(p * 2.03 + 2.7) * 0.29 + Noise(p * 4.11 + 8.1) * 0.14;
}
float4 BurnPS(float4 tint : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float progress = saturate(uProgress);
    float x = (uv.x - 0.5) * 2.0;
    float h = 1.0 - uv.y;
    // 火纹往上输送，尾端产生细碎火舌；不用改变整个矩形的大小。
    float2 flow = float2(x * 3.6, h * 4.8 - uTime * 4.8);
    float warp = Fbm(flow * 0.72 + 2.3);
    float n = Fbm(flow + float2(warp * 1.2, warp * 0.35));
    float width = 0.56 * (1.0 - h * 0.72);
    float bend = (warp - 0.5) * (0.10 + h * 0.42);
    float edge = width - abs(x + bend) + (n - 0.45) * (0.22 + h * 0.32);
    float body = smoothstep(-0.035, 0.065, edge);
    float tongues = 1.0 - smoothstep(0.50 + n * 0.30, 0.70 + n * 0.27, h);
    float base = smoothstep(0.025, 0.12, h);
    float rise = smoothstep(0.0, 0.16, progress);
    // 出场为包裹的火焰向上散开；入场先从脚底蔓延到全身。
    float ignition = lerp(smoothstep(h - 0.10, h + 0.10, progress * 2.6), 1.0, uArriving);
    float dissolve = smoothstep(0.48, 1.0, progress);
    float remain = smoothstep(dissolve - 0.10, dissolve + 0.12, n * 0.65 + h * 0.35);
    float alpha = body * tongues * base * ignition * rise * remain;
    float heat = saturate(0.38 + (1.0 - abs(x) / max(width, 0.04)) * 0.40 + n * 0.20 - h * 0.15);
    float3 color = lerp(uDarkColor, uLightColor, smoothstep(0.18, 0.72, heat));
    color = lerp(color, lerp(uLightColor, float3(1, 1, 1), 0.65), smoothstep(0.78, 1.0, heat));

    // 重现时的一圈短促火光，与火体共用深浅颜色，随后散成碎焰。
    float2 ringP = float2(x, (h - 0.38) * 1.28);
    float r = length(ringP);
    float radius = 0.14 + progress * 0.68;
    float ring = (1.0 - smoothstep(0.025, 0.075, abs(r - radius + (n - 0.5) * 0.09)))
        * uArriving * rise * (1.0 - smoothstep(0.30, 0.72, progress)) * 0.65;
    float total = saturate(alpha + ring);
    float3 rgb = color * alpha + uLightColor * ring;
    float border = smoothstep(0.0, 0.06, uv.x) * (1.0 - smoothstep(0.94, 1.0, uv.x))
        * smoothstep(0.0, 0.045, uv.y) * (1.0 - smoothstep(0.96, 1.0, uv.y));
    float end = 1.0 - smoothstep(0.88, 1.0, progress);
    return float4(rgb, total) * (border * end) * tint;
}
technique Resurrection { pass Fire { PixelShader = compile ps_3_0 BurnPS(); } }
