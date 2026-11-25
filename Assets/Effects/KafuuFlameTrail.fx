// 凯风快晴：脚尖月牙与飞踢彗星。颜色跟随本次技能焰色，输出预乘 Alpha。
float4x4 uTransform;
float uTime;
float3 uDarkColor;
float3 uLightColor;
struct TrailVertex { float4 Position:POSITION0; float4 Color:COLOR0; float2 UV:TEXCOORD0; };
struct TrailPixel { float4 Position:POSITION0; float4 Color:COLOR0; float2 UV:TEXCOORD0; };
TrailPixel TrailVS(TrailVertex input)
{
    TrailPixel output;
    output.Position=mul(float4(input.Position.xyz,1),uTransform);
    output.Color=input.Color; output.UV=input.UV;
    return output;
}
float Hash(float2 p)
{
    float3 q=frac(float3(p.x,p.y,p.x)*float3(0.1031,0.1030,0.0973));
    q+=dot(q,q.yzx+33.33);
    return frac((q.x+q.y)*q.z);
}
float Noise(float2 p)
{
    float2 c=floor(p),f=frac(p); f=f*f*(3-2*f);
    return lerp(lerp(Hash(c),Hash(c+float2(1,0)),f.x),lerp(Hash(c+float2(0,1)),Hash(c+1),f.x),f.y);
}
float Fbm(float2 p) { return Noise(p)*0.65+Noise(p*2.07+4.1)*0.25+Noise(p*4.13+7.3)*0.10; }
float3 FireColor(float heat)
{
    float3 c=lerp(uDarkColor,uLightColor,smoothstep(0.06,0.56,heat));
    return lerp(c,lerp(uLightColor,float3(1,1,1),0.90),smoothstep(0.48,0.94,heat));
}
float4 TrailPS(TrailPixel input):COLOR0
{
    float v=input.UV.y, life=saturate(input.Color.a);
    // UV.x 沿尾端到脚尖递增，噪声反向流动，让外焰朝旧路径拉长。
    float along=input.UV.x*2.8+uTime*4.8;
    float bend=Noise(float2(along*0.7,v*2.1))-0.5;
    float coarse=Fbm(float2(along-v*2.7+bend*0.65,v*5.2));
    float fine=Noise(float2(along*2.6-v*5.3,v*13.0));
    float aa=clamp(fwidth(v)*0.7,0.008,0.045);
    float inner=0.12+(coarse-0.5)*0.10;
    float outer=0.60+pow(saturate(coarse*1.25),1.5)*0.38;
    float body=smoothstep(inner-aa,inner+aa*2,v)*(1-smoothstep(outer-aa*2,outer+aa,v));
    // 几条顺着弧线的亮焰丝和外缘缺口，避免形成等宽的光管。
    float warp=v+(coarse-0.5)*0.13;
    float core=(1-smoothstep(0.08,0.20,abs(warp-0.36)));
    float filament=smoothstep(0.42,0.72,Noise(float2(along*1.1-warp*4.2,warp*17+bend*3)));
    float outerBreak=lerp(1.0,smoothstep(0.28,0.51,fine),smoothstep(0.53,0.88,v)*0.85);
    float erosion=smoothstep((1-life)*0.72-0.15,(1-life)*0.72+0.12,coarse+core*0.45);
    float heat=saturate(core*(0.70+coarse*0.40)+filament*0.32+0.16);
    float alpha=body*outerBreak*erosion*smoothstep(0,0.28,life)*0.98;
    float glow=exp(-abs(v-0.36)*9)*(1-smoothstep(0.9,1,v))*life*0.08;
    return float4(FireColor(heat)*alpha+uLightColor*glow, saturate(alpha+glow*0.3));
}
float4 DivePS(float4 tint:COLOR0,float2 uv:TEXCOORD0):COLOR0
{
    // 从左侧细长尾焰，向右侧脚尖火头逐渐扩大，整体随飞踢方向旋转。
    float x=uv.x, y=(uv.y-0.5)*2;
    float flow=x*4.5+uTime*5.8;
    float curl=Noise(float2(flow*0.65,y*2.7))-0.5;
    float n=Fbm(float2(flow+y*1.6+curl*0.7,y*5.2-uTime*0.8));
    float fine=Noise(float2(flow*2.7+y*3.8,y*16+7.1));
    float axis=y+sin(x*12-uTime*4)*0.035*(1-x)+(n-0.5)*0.07;
    float radial=abs(axis);
    float nose=max(0.0,x-0.73)/0.22;
    float envelope=min(smoothstep(0.02,0.72,x),sqrt(saturate(1.0-nose*nose)));
    float width=envelope*(0.43+n*0.51);
    float aa=clamp(fwidth(y)*0.7,0.007,0.03);
    float shell=1-smoothstep(width-aa*2.5,width+aa,radial);
    float head=smoothstep(0.49,0.69,x)*(1-smoothstep(0.88,0.965,x));
    float core=1-smoothstep(0.09,0.30+head*0.10,radial/max(envelope,0.02));
    // 连续亮芯包住脚尖；外焰被气流分成纵向拉长、逐渐熄灭的火舌。
    float ridges=smoothstep(0.43,0.73,Noise(float2(flow*1.3+axis*1.8,axis*15+curl*3.4)));
    float breaks=lerp(1.0,smoothstep(0.27,0.50,fine),smoothstep(0.25,0.75,radial/max(width,0.01))*0.80);
    float tailFade=smoothstep(0.015,0.18,x);
    float heat=saturate(core*(0.61+head*0.27)+ridges*0.30+head*n*0.33);
    float alpha=shell*breaks*tailFade*0.97;
    // 两条细长尾焰与主体并行，间隔留透明，避免成为一根三角形光束。
    float sideAxis=abs(y-(sin(x*7-uTime*3)*0.07))-(0.12+x*0.22);
    float wisps=(1-smoothstep(0.012,0.04,abs(sideAxis)))
        *smoothstep(0.01,0.16,x)*(1-smoothstep(0.42,0.65,x))*smoothstep(0.28,0.57,n);
    float glow=exp(-radial*6)*envelope*0.10;
    float3 rgb=FireColor(heat)*alpha+lerp(uDarkColor,uLightColor,0.75)*wisps*0.65+uLightColor*glow;
    float clip=smoothstep(0,0.025,x)*(1-smoothstep(0.97,1,x))*(1-smoothstep(0.94,1,abs(y)));
    return float4(rgb,saturate(alpha+wisps*0.65+glow*0.3))*clip*tint;
}
technique FlameTrail { pass DrawTrail { VertexShader=compile vs_3_0 TrailVS(); PixelShader=compile ps_3_0 TrailPS(); } }
technique DiveFlame { pass DrawDive { PixelShader=compile ps_3_0 DivePS(); } }
