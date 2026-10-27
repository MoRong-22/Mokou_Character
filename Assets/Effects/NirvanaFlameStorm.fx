// 涅槃 · 烈焰风暴。独立程序化火焰，不需要噪声贴图。
// 使用完整 UV 的矩形绘制；矩形底部中心对齐角色脚下。
// uTime 从 0 递增到 uDuration，单位为秒；结束后完全透明，不自动循环。
// uSize 是火柱在绘制矩形内的宽高倍率；实际像素大小由绘制矩形控制。
// 默认效果约占矩形中间 80% 宽度。放大时请同时留足绘制矩形边界。
// uFireColor 为 0~1 RGB，默认橙红色。uSwirlSpeed 控制旋卷速度和方向。
// 输出为预乘透明度，使用 SpriteBatch + BlendState.AlphaBlend。
float uTime = 0.0;
float uDuration = 5.0;
float2 uSize = float2(1.0, 1.0);
float3 uFireColor = float3(1.0, 0.25, 0.035);
float uIntensity = 1.0;
float uSwirlSpeed = 1.0;

float StormHash(float2 p)
{
    // 无 sin 的散列，减少噪声层的指令开销。
    float3 q = frac(float3(p.x, p.y, p.x) * float3(0.1031, 0.1030, 0.0973));
    q += dot(q, q.yzx + 33.33);
    return frac((q.x + q.y) * q.z);
}

float StormNoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = StormHash(cell);
    float b = StormHash(cell + float2(1.0, 0.0));
    float c = StormHash(cell + float2(0.0, 1.0));
    float d = StormHash(cell + 1.0);
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float StormFBM(float2 p)
{
    float value = 0.0;
    float amplitude = 0.57;
    [unroll]
    for (int octave = 0; octave < 3; octave++)
    {
        value += amplitude * StormNoise(p);
        p = float2(0.8 * p.x + 0.6 * p.y, -0.6 * p.x + 0.8 * p.y) * 2.03 + float2(3.1, 1.7);
        amplitude *= 0.5;
    }
    return value;
}

float4 NirvanaFlameStormPS(float2 texCoord : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    // 开始前、结束后不再运行噪声计算，也不残留光斑。
    if (uDuration <= 0.0 || uTime <= 0.0 || uTime >= uDuration)
        return float4(0.0, 0.0, 0.0, 0.0);

    float progress = saturate(uTime / max(uDuration, 0.001));
    float2 uv = float2((texCoord.x - 0.5) * 2.0, 1.0 - texCoord.y) / max(uSize, 0.001);
    float x = uv.x;
    float height = uv.y;
    float t = uTime;
    float ignition = smoothstep(0.0, 0.18, progress);
    float burst = smoothstep(0.48, 0.61, progress) * (1.0 - smoothstep(0.65, 0.79, progress));

    // 流动的噪声沿高度向上移动，卷动只改变火舌，不移动绘制矩形。
    float sway = sin(height * 9.0 - t * 5.0 * uSwirlSpeed);
    float2 flow = float2(x * 4.8 + sway * 0.32, height * 8.0 - t * 3.6);
    float warp = StormFBM(flow * 0.64 + float2(2.7, -t * 0.35));
    float detail = StormFBM(flow + float2(warp * 1.8, warp * 0.85));

    // 火柱中段包裹角色，上端收束为翻卷的火舌。
    float radius = 0.26 + 0.34 * smoothstep(0.0, 0.40, height)
                        - 0.42 * smoothstep(0.56, 1.0, height);
    radius *= (0.84 + ignition * 0.16) * (1.0 + burst * 0.14);
    float localX = x - sway * 0.025 * height;
    float core = saturate((0.92 - abs(localX) / max(radius, 0.025)) * (0.46 + detail)
                         - height * 0.28 + (detail - 0.45) * 0.34);
    float fire = smoothstep(0.08, 0.62, core);

    // 前后两束螺旋火带绕中心上升，用噪声打散轮廓，避免成为整齐的弹簧。
    float phase = height * 15.5 - t * 8.0 * uSwirlSpeed + (warp - 0.5) * 2.3;
    float ribbonX = sin(phase) * radius * 0.86;
    float width = (0.045 + (1.0 - saturate(height)) * 0.045) * (0.65 + detail * 0.8);
    float front = 1.0 - smoothstep(width * 0.25, width, abs(localX - ribbonX));
    float back = 1.0 - smoothstep(width * 0.25, width * 1.25, abs(localX + ribbonX));
    float depth = 0.5 + 0.5 * cos(phase);
    float ribbons = (front * lerp(0.45, 1.0, depth) + back * lerp(1.0, 0.45, depth))
                  * smoothstep(0.25, 0.65, detail) * (1.0 - smoothstep(0.76, 1.0, height));

    // 起火自下而上；结束阶段底部停止燃烧，残焰继续向上离开。
    float startMask = 1.0 - smoothstep(ignition * 1.03 - 0.10, ignition * 1.03 + 0.08, height);
    float ending = smoothstep(0.70, 1.0, progress);
    float stopMask = smoothstep(ending * 1.25 - 0.20, ending * 1.25 - 0.04,
                               height + (detail - 0.5) * 0.07);
    float topMask = 1.0 - smoothstep(0.76 + detail * 0.10, 0.99, height);
    float sideMask = 1.0 - smoothstep(0.74, 0.98, abs(x));
    float bottomMask = smoothstep(0.0, 0.045, height);
    float life = ignition * startMask * stopMask * topMask * sideMask * bottomMask
               * (1.0 - smoothstep(0.94, 1.0, progress));

    // 火星是着色器内部的亮点，随噪声格上升，无需生成实体粒子。
    float2 sparkUv = float2(x * 11.0 + sway * 0.22, height * 13.0 - t * 6.0);
    float seed = StormHash(floor(sparkUv) + 17.0);
    float2 sparkLocal = frac(sparkUv) - float2(0.5, 0.5);
    float sparkShape = 1.0 - smoothstep(0.025, 0.10,
                        length(sparkLocal * float2(1.0, 0.42)));
    float sparks = smoothstep(0.94, 0.99, seed) * sparkShape
                 * (1.0 - smoothstep(radius + 0.08, radius + 0.30, abs(x)));

    // 火柱内部也保留流动的明暗，避免中心糊成一块纯色。
    float heat = saturate(fire * (0.22 + detail * 1.15) + ribbons * 0.75);
    float3 theme = saturate(uFireColor);
    float3 hotColor = lerp(sqrt(theme), float3(1.0, 1.0, 1.0), 0.10);
    float3 color = lerp(theme * float3(0.48, 0.28, 0.22), theme, smoothstep(0.04, 0.50, heat));
    color = lerp(color, hotColor, smoothstep(0.55, 1.0, heat));
    color = lerp(color, sqrt(theme), saturate(sparks));
    float alpha = saturate(fire * 0.92 + ribbons * 0.85 + sparks) * life * vertexColor.a;
    color *= max(uIntensity, 0.0) * (1.0 + burst * 0.18);
    return float4(saturate(color) * vertexColor.rgb * alpha, alpha);
}

technique NirvanaFlameStorm
{
    pass NirvanaFlameStormPass
    {
        PixelShader = compile ps_3_0 NirvanaFlameStormPS();
    }
}
