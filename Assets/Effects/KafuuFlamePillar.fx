// 富士火山：火柱、计时火环、强化爆开和飞弹共用深浅焰色，无额外 RT。
float uTime;
float uDuration = 2.0;
float uEmpowered;
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
    float2 cell = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(Hash(cell), Hash(cell + float2(1.0, 0.0)), f.x),
                lerp(Hash(cell + float2(0.0, 1.0)), Hash(cell + 1.0), f.x), f.y);
}
float3 FlameColor(float heat)
{
    float3 color = lerp(uDarkColor, uLightColor, smoothstep(0.06, 0.65, heat));
    return lerp(color, lerp(uLightColor, float3(1, 1, 1), 0.72), smoothstep(0.70, 1.0, heat));
}
float4 PillarPS(float4 tint : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float x = uv.x * 2.0 - 1.0, height = 1.0 - uv.y;
    float t = max(0.0, uTime);
    float coarse = Noise(float2(x * 3.3 + 7.1, height * 4.2 - t * 2.8));
    float warp = x + (coarse - 0.5) * (0.15 + height * 0.18);
    float strands = Noise(float2(warp * 8.0, height * 5.5 - t * 4.3));
    float fine = Noise(float2(warp * 17.0 + 9.0, height * 14.0 - t * 7.5));
    float flame = strands * 0.72 + fine * 0.28;
    // 外焰宽度与内部流纹分别变化，亮芯被拉长的暗纹切开，不再是一块白色矩形。
    float width = 0.65 - height * 0.12 + (coarse - 0.5) * 0.18 + (flame - 0.5) * 0.17;
    float d = abs(warp);
    float body = 1.0 - smoothstep(width - 0.11, width + 0.045, d);
    float tip = 0.78 + strands * 0.20 - d * 0.05;
    float head = 1.0 - smoothstep(tip - 0.12, tip, height);
    float depth = saturate(1.0 - d / max(width, 0.1));
    float channels = smoothstep(0.22, 0.72, flame + depth * 0.14);
    float heat = saturate(depth * 0.51 + channels * 0.54 + (1.0 - height) * 0.06);
    float3 color = FlameColor(heat);
    float rise = saturate(t / 0.20) * 1.16;
    float mask = (1.0 - smoothstep(rise - 0.10, rise, height + (1.0 - flame) * 0.05))
               * smoothstep(0.0, 0.04, t) * smoothstep(0.0, 0.016, height);
    // 普通版断火上飘；强化版保持燃烧直到爆开，收尾由 Burst 技法接手。
    float release = saturate((t - (uDuration - 0.32)) / 0.32);
    float normalEnd = smoothstep(release * 1.25 - 0.13, release * 1.25 + 0.03,
                                height + (flame - 0.5) * 0.09)
                    * (1.0 - smoothstep(0.7, 1.0, release));
    mask *= lerp(normalEnd, 1.0, uEmpowered) * (1.0 - step(uDuration, t));
    float glow = exp(-max(0.0, d - width) * 20.0) * (1.0 - smoothstep(0.78, 0.98, abs(x))) * 0.08;
    float alpha = saturate(body * (0.78 + channels * 0.22) + glow * 0.3) * head * mask;
    return float4(color * alpha + uLightColor * glow * head * mask, alpha) * tint;
}

struct RingVertex { float4 Position : POSITION0; float4 Color : COLOR0; float2 UV : TEXCOORD0; };
RingVertex RingVS(RingVertex input)
{
    RingVertex output;
    output.Position = mul(float4(input.Position.xyz, 1.0), uTransform);
    output.Color = input.Color;
    output.UV = input.UV;
    return output;
}
float4 RingPS(RingVertex input) : COLOR0
{
    float a = input.UV.x * 6.2831853 - uTime * 8.0;
    float stream = Noise(float2(cos(a), sin(a)) * 4.0 + float2(6.7, 3.1));
    float v = abs(input.UV.y * 2.0 - 1.0);
    float body = 1.0 - smoothstep(0.42 + stream * 0.30, 0.94, v);
    float core = 1.0 - smoothstep(0.12, 0.42, v);
    float alpha = body * input.Color.a * (0.70 + stream * 0.30);
    float3 color = FlameColor(saturate(0.34 + core * 0.64 + stream * 0.10));
    return float4(color * alpha * input.Color.rgb, alpha);
}

float4 BurstPS(float4 tint : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float progress = saturate(uTime / uDuration);
    float2 p = uv * 2.0 - 1.0;
    float r = length(p);
    float2 axis = p / max(r, 0.001);
    float n = Noise(axis * 4.0 + float2(7.3, uTime * 6.0));
    float expansion = 1.0 - pow(1.0 - progress, 3.0);
    float radius = 0.08 + expansion * 0.68;
    float width = lerp(0.20, 0.045, progress);
    float distance = abs(r - radius - (n - 0.5) * 0.12);
    float wave = 1.0 - smoothstep(width * 0.2, width, distance);
    float flash = exp(-r * 7.0) * (1.0 - smoothstep(0.05, 0.40, progress));
    float fade = 1.0 - smoothstep(0.15, 1.0, progress);
    float alpha = saturate(wave + flash) * fade * (1.0 - smoothstep(0.90, 1.0, r));
    return float4(FlameColor(saturate(0.45 + wave * 0.45 + flash)) * alpha, alpha) * tint;
}

// +X 为飞行方向；焰尾在左侧，由纹理上色与时间流动产生，不额外生成散粒子。
float4 MissilePS(float4 tint : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float x = uv.x, y = uv.y * 2.0 - 1.0;
    float n = Noise(float2(x * 6.0 + uTime * 14.0, y * 2.8));
    float width = smoothstep(0.0, 0.68, x) * 0.56 * (1.0 - smoothstep(0.72, 1.0, x));
    float d = abs(y + (n - 0.5) * (1.0 - x) * 0.35);
    float alpha = (1.0 - smoothstep(width * 0.7, width + 0.08, d))
                * smoothstep(0.01, 0.18, x) * (1.0 - smoothstep(0.94, 1.0, x));
    float heat = saturate(1.0 - d / max(width, 0.01) + x * 0.12);
    return float4(FlameColor(heat) * alpha, alpha) * tint;
}
technique FlamePillar { pass DrawPillar { PixelShader = compile ps_3_0 PillarPS(); } }
technique FlameRing { pass DrawRing { VertexShader = compile vs_3_0 RingVS(); PixelShader = compile ps_3_0 RingPS(); } }
technique FlameBurst { pass DrawBurst { PixelShader = compile ps_3_0 BurstPS(); } }
technique FlameMissile { pass DrawMissile { PixelShader = compile ps_3_0 MissilePS(); } }