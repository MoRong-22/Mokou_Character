// 涅槃外层风暴：中空火焰风眼，围绕角色旋转的伪 3D 螺旋火带。
// 使用白色矩形、完整 UV、BlendState.AlphaBlend；底部中心对齐玩家脚底。
// uLayer=-1 绘制后半圈（角色之前），uLayer=1 绘制前半圈（角色之后）。
// 和内层 NirvanaFlameStorm 共用 uTime、uDuration，单位为秒。
float uTime = 0.0;
float uDuration = 5.0;
float2 uSize = float2(1.0, 1.0);
float3 uFireColor = float3(1.0, 0.25, 0.035);
float uIntensity = 1.0;
float uSwirlSpeed = 1.0;
float uLayer = 1.0;

float VortexHash(float2 p)
{
    float3 q = frac(float3(p.x, p.y, p.x) * float3(0.1031, 0.1030, 0.0973));
    q += dot(q, q.yzx + 33.33);
    return frac((q.x + q.y) * q.z);
}

float VortexNoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(VortexHash(cell), VortexHash(cell + float2(1.0, 0.0)), f.x),
                lerp(VortexHash(cell + float2(0.0, 1.0)), VortexHash(cell + 1.0), f.x), f.y);
}

float VortexFBM(float2 p)
{
    return VortexNoise(p) * 0.67
         + VortexNoise(float2(p.x * 1.6 + p.y * 1.2, -p.x * 1.2 + p.y * 1.6) + 7.3) * 0.33;
}

float VortexRadius(float height)
{
    return 0.48 + 0.24 * smoothstep(0.02, 0.78, height);
}

float4 NirvanaVortexPS(float2 texCoord : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    if (uDuration <= 0.0 || uTime <= 0.0 || uTime >= uDuration)
        return float4(0.0, 0.0, 0.0, 0.0);

    float progress = saturate(uTime / max(uDuration, 0.001));
    float ignition = smoothstep(0.0, 0.18, progress);
    float ending = smoothstep(0.70, 1.0, progress);
    float2 uv = float2((texCoord.x - 0.5) * 2.0, 1.0 - texCoord.y) / max(uSize, 0.001);
    float x = uv.x;
    float y = uv.y;
    float layer = uLayer >= 0.0 ? 1.0 : -1.0;

    // 把圆形截面投影为椭圆：近处火带向下，远处火带向上。
    // 反算柱面高度一次，使前后半圈在左右边缘相接。
    float radius = VortexRadius(y) * lerp(0.82, 1.0, ignition);
    float normalizedX = x / max(radius, 0.01);
    float depth = sqrt(saturate(1.0 - normalizedX * normalizedX));
    float height = y + layer * depth * 0.10;
    radius = VortexRadius(height) * lerp(0.82, 1.0, ignition);
    normalizedX = x / max(radius, 0.01);
    depth = sqrt(saturate(1.0 - normalizedX * normalizedX));
    height = y + layer * depth * 0.10;
    float silhouette = 1.0 - smoothstep(0.97, 1.025, abs(normalizedX));
    if (silhouette <= 0.0 || height <= 0.0 || height >= 1.05)
        return float4(0.0, 0.0, 0.0, 0.0);

    // 柱面角度随时间平移，产生真正绕风眼旋转的明暗和纹理。
    float angle = atan2(clamp(normalizedX, -1.0, 1.0), layer * max(depth, 0.001));
    float turn = angle * 0.159154943;
    float rotation = uTime * uSwirlSpeed * 0.85;
    // 圆周坐标用 sin/cos 保持首尾连续，避免柱面背后的 UV 接缝。
    float flowAngle = angle - rotation * 6.283185307;
    float2 flow = float2(sin(flowAngle) * 2.4 + cos(flowAngle) * 1.3,
                         height * 12.0 - uTime * 3.2);
    float warp = VortexFBM(flow * 0.72 + 3.1);
    float detail = VortexFBM(flow * 1.65 + warp * 1.4);

    // 三圈螺旋火带，边缘被流动噪声撕成火舌，中间保留透明间隙。
    float phase = height * 3.2 - turn + rotation + (warp - 0.5) * 0.20;
    float bandDistance = abs(frac(phase) - 0.5);
    float bandWidth = 0.095 + detail * 0.070;
    float ribbon = 1.0 - smoothstep(bandWidth * 0.28, bandWidth, bandDistance);
    float fringe = (1.0 - smoothstep(bandWidth, bandWidth + 0.085, bandDistance))
                 * smoothstep(0.42, 0.78, detail) * 0.38;
    float rim = smoothstep(0.68, 0.98, abs(normalizedX))
              * smoothstep(0.28, 0.74, detail) * 0.35;

    // 近处更亮、远处更暗。前面透出角色，避免把风眼填成实心火柱。
    float front = step(0.0, uLayer);
    float density = lerp(0.68, 0.55, front);
    float illumination = lerp(0.47, 0.76 + depth * 0.24, front);
    float startMask = 1.0 - smoothstep(ignition * 1.10 - 0.09, ignition * 1.10 + 0.08, height);
    float stopMask = smoothstep(ending * 1.30 - 0.20, ending * 1.30 - 0.04,
                               height + (detail - 0.5) * 0.07);
    float life = ignition * startMask * stopMask * silhouette
               * smoothstep(0.0, 0.09, height)
               * (1.0 - smoothstep(0.83, 1.02, height))
               * (1.0 - smoothstep(0.94, 1.0, progress));

    float heat = saturate(ribbon * (0.38 + detail * 0.72) + fringe * 0.5);
    float3 theme = saturate(uFireColor);
    float3 color = lerp(theme * float3(0.65, 0.38, 0.22), theme, smoothstep(0.0, 0.50, heat));
    color = lerp(color, sqrt(theme), smoothstep(0.58, 1.0, heat));
    color *= illumination * max(uIntensity, 0.0);
    float alpha = saturate((ribbon + fringe) * density + rim) * life * vertexColor.a;
    return float4(saturate(color) * vertexColor.rgb * alpha, alpha);
}

technique NirvanaVortex
{
    pass NirvanaVortexPass
    {
        PixelShader = compile ps_3_0 NirvanaVortexPS();
    }
}
