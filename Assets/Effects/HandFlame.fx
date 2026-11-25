// 前手试作：连续流动的火舌 + 独立热折射 pass。输出使用预乘 Alpha。
// uTime 为秒；深浅焰色沿用角色配置。无需噪声贴图、屏幕像素化或额外 RT。
float uTime;
float uOpacity = 1.0;
float3 uDarkColor = float3(0.65, 0.06, 0.01);
float3 uLightColor = float3(1.0, 0.72, 0.20);

// Heat 技术采样当前场景，写入另一张场景 RT。
sampler uImage0 : register(s0);
float2 uViewportSize;
float2 uCenterPixels;
float2 uHalfSizePixels;
float uStrengthPixels = 1.25;
// 屏幕空间中从手掌指向火尖的单位向量。
float2 uHeatAxis = float2(0.0, -1.0);
float uReveal = 1.0;
float uReverse = 0.0;

float Hash(float2 p)
{
    float3 q = frac(float3(p.x, p.y, p.x) * float3(0.1031, 0.1030, 0.0973));
    q += dot(q, q.yzx + 33.33);
    return frac((q.x + q.y) * q.z);
}

float Noise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    return lerp(lerp(Hash(cell), Hash(cell + float2(1.0, 0.0)), f.x),
                lerp(Hash(cell + float2(0.0, 1.0)), Hash(cell + 1.0), f.x), f.y);
}

float FlameNoise(float2 p)
{
    return Noise(p) * 0.57
        + Noise(p * 2.03 + float2(7.3, 2.8)) * 0.29
        + Noise(p * 4.09 + float2(1.9, 11.7)) * 0.14;
}

float4 FlamePS(float2 uv : TEXCOORD0, float4 tint : COLOR0) : COLOR0
{
    float x = (uv.x - 0.5) * 2.0;
    // 根部锚点 UV=(0.5,0.9)，四周留空，避免矩形裁切。
    float height = (0.9 - uv.y) / 0.83;
    float h = saturate(height);
    float2 flow = float2(x * 2.7, height * 3.2 - uTime * 2.6);
    // 大尺度横摆带动细火舌，上升越高越容易分叉。
    float bend = Noise(float2(x * 1.2 + 4.6, height * 2.0 - uTime * 1.4)) - 0.5;
    float curl = Noise(flow * 0.75 + float2(9.2, 1.5)) - 0.5;
    float n = FlameNoise(flow + float2(bend * 1.7, curl * 0.65));
    float curvedX = x + bend * (0.12 + h * 0.65);
    float width = 0.64 * pow(max(1.0 - h, 0.001), 0.72);
    float density = width - abs(curvedX) + (n - 0.5) * (0.16 + h * 0.85) - h * 0.10;

    float root = smoothstep(-0.055, 0.12, height - x * x * 0.16);
    float tip = 1.0 - smoothstep(0.86, 1.02, height);
    float flame = smoothstep(-0.045, 0.085, density) * root * tip;
    float heat = saturate(density / max(width, 0.10));
    heat *= 0.78 + n * 0.22;
    float3 color = lerp(uDarkColor, uLightColor, smoothstep(0.04, 0.85, heat));
    // 外围有微弱光晕，主体保留暗焰色和内部层次，不全涂成白色。
    float glow = exp(-abs(curvedX) * 5.5 - h * 3.0) * root * tip * 0.12;
    float alpha = saturate(flame * 0.94 + glow) * saturate(uOpacity) * tint.a;
    float3 rgb = (color * flame * 0.94 + uLightColor * glow) * saturate(uOpacity);
    return float4(rgb * tint.rgb * tint.a, alpha);
}

float4 HeatPS(float2 uv : TEXCOORD0) : COLOR0
{
    float2 viewport = max(uViewportSize, 1.0);
    float2 delta = uv * viewport - uCenterPixels;
    float2 local = float2(dot(delta, float2(-uHeatAxis.y, uHeatAxis.x)), -dot(delta, uHeatAxis))
        / max(uHalfSizePixels, 1.0);
    float radius = dot(local, local);
    if (radius >= 1.0 || uOpacity <= 0.0) return tex2D(uImage0, uv);

    float mask = (1.0 - smoothstep(0.05, 1.0, radius)) * saturate(uOpacity);
    // UV 向下为正，加时间使噪声向上流。仅移动采样坐标，不改变画面颜色。
    float2 flow = local * float2(2.1, 3.3) + float2(0.0, uTime * 3.0);
    float nx = Noise(flow) - 0.5;
    float ny = Noise(flow + float2(13.5, 4.2)) - 0.5;
    float2 offset = float2(nx, ny * 0.35) * uStrengthPixels * 2.0 * mask / viewport;
    float2 inset = 0.5 / viewport;
    return tex2D(uImage0, clamp(uv + offset, inset, 1.0 - inset));
}

technique Flame
{
    pass DrawFlame { PixelShader = compile ps_3_0 FlamePS(); }
}
technique Heat
{
    pass DrawHeat { PixelShader = compile ps_3_0 HeatPS(); }
}

// 三道火焰撕裂痕：连续亮芯、拉长的碎焰边缘和低强度光晕。
// 与 ClawPoint 共用弧线，噪声只扰动外焰，不让骨骼追踪路径抖动。
float4 ClawFlame(float2 uv, float lane)
{
    float t = (uv.x - 0.055) / 0.89;
    float h = saturate(t);
    float u = h * 2.0 - 1.0;
    float curve = (0.10 + lane * 0.018) * (1.0 - u * u);
    float spread = 0.205 + 0.025 * h;
    float center = (lane - 1.0) * spread + curve;
    float v = uv.y - 0.5 - center;
    float travel = lerp(h, 1.0 - h, uReverse);
    float scroll = travel * 12.0 - uTime * 5.0;
    // 斜切噪声使碎焰拉向拖尾，而不是长成圆团。
    float coarse = Noise(float2(scroll - v * 45.0, v * 34.0 + lane * 9.7));
    float fine = Noise(float2(scroll * 2.1 - v * 20.0, v * 61.0 + lane * 3.2));
    float turbulence = coarse * 0.74 + fine * 0.26;
    float envelope = pow(max(sin(h * 3.14159265), 0.0), 0.95);
    float coreWidth = (lane == 1.0 ? 0.022 : 0.018) * envelope;
    float centerWobble = (fine - 0.5) * 0.008 * envelope;
    float distanceToCore = abs(v + centerWobble);
    float aa = max(fwidth(v) * 0.65, 0.001);
    // 一侧拉得更长，形成火舌；另外一侧保留清晰的斩击边。
    float tongues = pow(saturate(coarse * 1.16), 3.0);
    float outerWidth = (0.030 + tongues * (v > 0.0 ? 0.105 : 0.045)) * envelope;
    float outer = 1.0 - smoothstep(outerWidth - aa, outerWidth + aa, distanceToCore);
    float core = 1.0 - smoothstep(coreWidth - aa, coreWidth + aa, distanceToCore);
    float hotCenter = 1.0 - smoothstep(coreWidth * 0.30, coreWidth + aa, distanceToCore);
    float glow = exp(-distanceToCore * 44.0) * envelope * 0.24;

    float tips = smoothstep(0.0, 0.025, t) * (1.0 - smoothstep(0.975, 1.0, t));
    float reveal = 1.0 - smoothstep(uReveal - 0.025, uReveal + 0.025, travel);
    // 收势时先从碎焰之间烧散，内芯稍晚消失。
    float extinction = (1.0 - saturate(uOpacity)) * 0.85;
    float fragments = smoothstep(extinction - 0.12, extinction + 0.12, turbulence);
    float alive = lerp(fragments, 1.0, core * saturate(uOpacity));
    float mask = tips * reveal * alive * saturate(uOpacity);
    float3 rimColor = lerp(uDarkColor, uLightColor, saturate(turbulence * 0.9));
    float3 hotColor = lerp(uLightColor, float3(1.0, 1.0, 1.0), 0.84);
    float3 bodyColor = lerp(rimColor, hotColor, core * (0.75 + hotCenter * 0.25));
    // 预乘颜色加一层微弱辉光；不需要全屏 Bloom RT。
    float alpha = saturate(outer * 0.94 + glow * 0.32) * mask;
    float3 rgb = (bodyColor * outer + uLightColor * glow * 1.65) * mask;
    return float4(rgb, alpha);
}

float4 ClawPS(float2 uv : TEXCOORD0, float4 tint : COLOR0) : COLOR0
{
    float4 a = ClawFlame(uv, 0.0);
    float4 b = ClawFlame(uv, 1.0);
    float4 c = ClawFlame(uv, 2.0);
    float4 result = float4(max(a.rgb, max(b.rgb, c.rgb)), max(a.a, max(b.a, c.a)));
    return float4(result.rgb * tint.rgb * tint.a, result.a * tint.a);
}

technique Claw
{
    pass DrawClaw { PixelShader = compile ps_3_0 ClawPS(); }
}
