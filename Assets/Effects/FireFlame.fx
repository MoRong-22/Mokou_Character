float uTime = 0.0;

// 默认橙红色：外围较深，内部较亮。
float3 uDarkColor = float3(0.65, 0.06, 0.01);
float3 uLightColor = float3(1.0, 0.72, 0.20);

float2 uDirection = float2(0.0, -1.0);

// 为每个格点生成固定的伪随机数。
float FireHash(float2 p)
{
    return frac(sin(dot(p, float2(1.1, 1.7))) * 48.53);
}

// 在相邻格点之间平滑插值，得到连续噪声。
float FireNoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);

    f = f * f * (3.0 - 2.0 * f);

    float a = FireHash(cell);
    float b = FireHash(cell + float2(1.0, 0.0));
    float c = FireHash(cell + float2(0.0, 1.0));
    float d = FireHash(cell + float2(1.0, 1.0));

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float4 FirePS(
float2 uv : TEXCOORD0,
float4 vertexColor : COLOR0) : COLOR0
{
   
    float directionLength = length(uDirection);

    float2 direction = directionLength > 0.001
    ? uDirection / directionLength
    : float2(0.0, -1.0);

    float2 sideways = float2(-direction.y, direction.x);
    float2 p = uv - float2(0.5, 0.5);

    float x = dot(p, sideways);

    
    float height = (dot(p, direction) + 0.42) / 0.84;
    float h = saturate(height);

    
    float2 flow = float2(x * 8.0, height * 5.0 - uTime * 2.0);

    float noise = FireNoise(flow) * 0.65;
    noise += FireNoise(flow * 2.03 + float2(3.1, 1.7)) * 0.35;

    
    x += (noise - 0.5) * 0.12 * h;

    
    float halfWidth = lerp(0.28, 0.035, h);
    float body = 1.0 - abs(x) / halfWidth;

   
    float strength = saturate(
    body - h * 0.55 + (noise - 0.5) * 0.85
    );

    float alpha = smoothstep(0.08, 0.25, strength);

    // 根部在绘制区域边缘之前自然淡出，避免火焰被矩形底边硬切掉。
    // 两侧稍微抬高，并沿用流动噪声，使底部呈轻微起伏的圆弧。
    float rootCurve = (x / 0.28) * (x / 0.28) * 0.08;
    float rootFade = smoothstep(-0.06, 0.14,
        height - rootCurve + (noise - 0.5) * 0.04);
    alpha *= rootFade;

    
    float heat = smoothstep(0.15, 0.95, strength);
    float3 color = lerp(uDarkColor, uLightColor, heat);

    
    return float4(color * alpha, alpha) * vertexColor;
}

    technique BasicFire
{
        pass FirePass
    {
        PixelShader = compile ps_3_0 FirePS();
    }
}
