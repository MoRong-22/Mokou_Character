using System;
using Microsoft.Xna.Framework;

namespace Mokou_Character.SkillVisuals;

/// <summary>旋风的弯曲轴线与外轮廓，对应 NirvanaVortex.fx；供碰撞使用，不分配绘制资源。</summary>
internal static class NirvanaVortexShape
{
    private static float Smooth(float a, float b, float value)
    {
        float t = MathHelper.Clamp((value - a) / (b - a), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float SpeedIntegral(float p, float a, float b)
    {
        float x = MathHelper.Clamp((p - a) / (b - a), 0f, 1f);
        return (b - a) * (x * x * x - 0.5f * x * x * x * x) + Math.Max(p - b, 0f);
    }

    internal static float Spin(float progress, float duration)
        => duration * (0.22f * progress
            + 0.38f * SpeedIntegral(progress, 0.20f, 2f / 3f)
            + 0.85f * SpeedIntegral(progress, 2f / 3f, 5.15f / 6f)
            + 0.65f * SpeedIntegral(progress, 5.15f / 6f, 0.95f));

    internal static float Center(float height, float spin)
    {
        float h = MathHelper.Clamp(height, 0f, 1f), phase = spin * 2.4f;
        return Smooth(0.02f, 0.24f, h) * (0.16f * h * MathF.Sin(phase * 0.40f + 0.8f)
            + (0.055f + h * 0.15f) * MathF.Sin(h * 8.2f - phase)
            + 0.07f * h * MathF.Sin(h * 17f + phase * 0.65f));
    }

    internal static float Radius(float height, float spin)
    {
        float h = MathHelper.Clamp(height, 0f, 1f);
        float waist = 0.32f + 0.20f * MathF.Pow(MathHelper.Clamp(MathF.Abs(h - 0.46f) / 0.54f, 0f, 1f), 1.4f);
        float root = 0.10f * (1f - Smooth(0f, 0.24f, h));
        float fold = 1f + 0.24f * MathF.Sin(h * 15.5f - spin * 3.48f)
            + 0.10f * MathF.Sin(h * 29f + spin * 2.16f);
        return (waist + root) * MathHelper.Lerp(1f, fold, Smooth(0f, 0.18f, h));
    }

    /// <summary>取指定屏幕高度的投影包络。薄小的焰带缺口仍属于风场的连续伤害范围。</summary>
    internal static void CrossSection(float height, float progress, float duration, float halfWidth,
        out float center, out float radius)
    {
        float scale = 0.56f + 0.22f * Smooth(2f / 3f, 5.15f / 6f, progress)
            + 0.32f * Smooth(5.15f / 6f, 0.95f, progress);
        float burst = MathHelper.Clamp((progress - 0.95f) / 0.05f, 0f, 1f);
        float travel = 1f - (1f - burst) * (1f - burst);
        float h = (height - 0.43f) / (1f + travel * 0.10f) + 0.43f;
        float spin = Spin(progress, duration);
        float expand = halfWidth * (1f + travel * 0.65f);
        center = Center(h, spin) * expand;
        radius = 0f;
        // 前后圆周的透视会将相邻高度投到此截面，使用与 shader 相同的三次迭代。
        for (int sample = -3; sample <= 3; sample++)
        {
            float depth = sample / 4f, surfaceHeight = h;
            for (int i = 0; i < 3; i++)
                surfaceHeight = h + depth * Radius(surfaceHeight, spin) * scale * 1.32f * 0.13f;
            float width = Radius(surfaceHeight, spin) * scale * 1.32f * MathF.Sqrt(1f - depth * depth);
            radius = Math.Max(radius, width * expand);
        }
    }
}
