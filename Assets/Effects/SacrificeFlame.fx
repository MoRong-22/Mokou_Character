// 舍身击专用：一秒收拢蓄力、同向火弹和固定世界路径的焰尾。
float uTime;
float uProgress;
float3 uDarkColor = float3(0.65, 0.06, 0.01);
float3 uLightColor = float3(1.0, 0.72, 0.20);
float4x4 uTransform;
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
    return lerp(lerp(Hash(c), Hash(c + float2(1.0, 0.0)), f.x),
                lerp(Hash(c + float2(0.0, 1.0)), Hash(c + 1.0), f.x), f.y);
}
float3 FlameColor(float heat)
{
    float3 color = lerp(uDarkColor, uLightColor, smoothstep(0.06, 0.66, heat));
    return lerp(color, lerp(uLightColor, float3(1, 1, 1), 0.70), smoothstep(0.70, 1.0, heat));
}
float4 ChargePS(float4 tint : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 p = (uv * 2.0 - 1.0) * float2(1.0, 0.92);
    float r = length(p);
    float a = atan2(p.y, p.x);
    float progress = saturate(uProgress);
    float n = Noise(p * 5.0 + float2(uTime * 2.0, -uTime * 3.0));
    // 两段相对的火弧向身体收拢，开口绕着玩家旋转，中心留空不遮住动作。
    float radius = lerp(0.86, 0.51, progress * progress);
    float wave = 0.5 + 0.5 * sin(a * 2.0 - uTime * 9.0 + n * 0.6);
    float arc = smoothstep(0.18, 0.78, wave);
    float width = 0.025 + arc * 0.045 + progress * 0.015;
    float d = abs(r - radius - (n - 0.5) * 0.045);
    float ring = 1.0 - smoothstep(width * 0.2, width, d);
    float corona = exp(-abs(r - radius) * 28.0) * arc * 0.12;
    float grow = smoothstep(0.0, 0.12, progress);
    float alpha = (ring * arc * 0.86 + corona) * grow;
    return float4(FlameColor(saturate(0.34 + ring * 0.57 + progress * 0.09)) * alpha, alpha) * tint;
}
float4 BoltPS(float4 tint : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    // +X 指向冲刺方向；长尾朝后，亮芯与外焰分别流动。
    float x = uv.x, y = uv.y * 2.0 - 1.0;
    float stream = Noise(float2(x * 6.0 + uTime * 13.0, y * 2.0 + 4.7));
    float fine = Noise(float2(x * 12.0 + uTime * 21.0, y * 5.0));
    float width = smoothstep(0.0, 0.72, x) * (1.0 - smoothstep(0.75, 1.0, x));
    width *= 0.54 + stream * 0.18;
    float d = abs(y + (stream - 0.5) * (1.0 - x) * 0.35);
    float body = 1.0 - smoothstep(width - 0.10, width + 0.10, d);
    float cap = smoothstep(0.01, 0.18, x) * (1.0 - smoothstep(0.93, 1.0, x));
    float heat = saturate(1.0 - d / max(0.05, width) + fine * 0.12);
    float alpha = body * cap;
    return float4(FlameColor(heat) * alpha, alpha) * tint;
}
struct RibbonVertex { float4 Position : POSITION0; float4 Color : COLOR0; float2 UV : TEXCOORD0; };
RibbonVertex RibbonVS(RibbonVertex input)
{
    RibbonVertex output;
    output.Position = mul(float4(input.Position.xyz, 1.0), uTransform);
    output.Color = input.Color;
    output.UV = input.UV;
    return output;
}
float4 RibbonPS(RibbonVertex input) : COLOR0
{
    float y = input.UV.y * 2.0 - 1.0;
    float stream = Noise(float2(input.UV.x * 3.5 + uTime * 7.0, y * 3.2));
    float fine = Noise(float2(input.UV.x * 9.0 + uTime * 12.0, y * 7.0));
    float life = input.Color.a;
    float d = abs(y + (fine - 0.5) * 0.10);
    float width = 0.23 + stream * 0.56;
    float body = 1.0 - smoothstep(width - 0.10, width + 0.10, d);
    float tears = smoothstep((1.0 - life) * 0.95 - 0.12, (1.0 - life) * 0.95 + 0.12, stream);
    float heat = saturate((1.0 - d) * 0.62 + fine * 0.30);
    float alpha = body * life * tears * 0.72;
    return float4(FlameColor(heat) * alpha, alpha);
}
technique Charge { pass DrawCharge { PixelShader = compile ps_3_0 ChargePS(); } }
technique Firebolt { pass DrawFirebolt { PixelShader = compile ps_3_0 BoltPS(); } }
technique DashRibbon { pass DrawRibbon { VertexShader = compile vs_3_0 RibbonVS(); PixelShader = compile ps_3_0 RibbonPS(); } }