using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Mokou_Character.SkillVisuals;

/// <summary>两道有透视的火焰环。进度只上升一次，不循环回到底部；顶点缓存重复使用。</summary>
internal sealed class FlamePillarRings
{
    internal const int RingCount = 2;
    private const int Segments = 64;
    internal VertexPositionColorTexture[] Vertices { get; } = new VertexPositionColorTexture[RingCount * Segments / 2 * 6];

    /// <summary>先画远半环，再画火柱，最后画近半环。Z 朝观众为正，透视为 focal / (focal - Z)。</summary>
    internal int Build(Vector2 bottom, float width, float height, float progress, bool front)
    {
        progress = Math.Clamp(progress, 0f, 1f);
        if (progress <= 0f || progress >= 1f) return 0;
        float opacity = Smooth(progress / 0.07f) * (1f - Smooth((progress - 0.92f) / 0.08f));
        int count = 0;
        for (int ring = 0; ring < RingCount; ring++)
        {
            // 两环起初隔开，接近燃尽时在顶部收拢，表示剩余时间。
            float lift = MathHelper.Lerp(0.12f + ring * 0.18f, 0.88f, progress);
            Vector2 center = bottom - new Vector2(0f, height * lift);
            float radius = width * (0.86f - 0.14f * progress + ring * 0.04f);
            float tilt = MathF.Sin(progress * MathHelper.TwoPi * 1.35f + ring * 2.2f) * 0.22f;
            int first = front ? 0 : Segments / 2;
            for (int i = first; i < first + Segments / 2; i++)
            {
                var a0 = Vertex(i, -1f);
                var a1 = Vertex(i, 1f);
                var b0 = Vertex(i + 1, -1f);
                var b1 = Vertex(i + 1, 1f);
                Vertices[count++] = a0; Vertices[count++] = a1; Vertices[count++] = b0;
                Vertices[count++] = b0; Vertices[count++] = a1; Vertices[count++] = b1;
            }

            VertexPositionColorTexture Vertex(int index, float side)
            {
                float angle = index * MathHelper.TwoPi / Segments;
                float sin = MathF.Sin(angle), cos = MathF.Cos(angle);
                // 火带宽度沿圆周变化并随时间绕环流动，避免看起来像硬质圆管。
                float ribbon = 3.5f + 2.5f * MathF.Pow(0.5f + 0.5f * MathF.Cos(angle - progress * 13f - ring * 2f), 2f);
                float r = radius + side * ribbon;
                float z = sin * r;
                float perspective = width * 4f / (width * 4f - z);
                float x = cos * r;
                float y = sin * r * 0.30f + cos * r * tilt;
                // 额外的竖向带宽保证正前方也有可读的亮芯。
                y += side * ribbon * 0.65f;
                Vector2 p = center + new Vector2(x, y) * perspective;
                float shade = 0.68f + 0.32f * (sin * 0.5f + 0.5f);
                return new VertexPositionColorTexture(new Vector3(p, 0f), new Color(shade, shade, shade, opacity),
                    new Vector2(index / (float)Segments + ring * 0.37f, (side + 1f) * 0.5f));
            }
        }
        return count;
    }

    private static float Smooth(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
