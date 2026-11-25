using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Mokou_Character.SkillVisuals;

/// <summary>凯风快晴专用月牙：脚尖附近宽厚，旧路径收细并消散，中心留空。</summary>
internal sealed class KafuuCrescentMesh
{
    internal VertexPositionColorTexture[] Vertices { get; }
    internal KafuuCrescentMesh(int capacity) => Vertices = new VertexPositionColorTexture[capacity * 2];

    internal int Build(Vector2[] positions, Vector2[] normals, float[] times, float[] distances,
        int start, int count, float now, float lifetime, float halfWidth, Vector2 screenPosition)
    {
        int capacity = positions.Length, first = 0;
        while (first < count && now - times[(start + first) % capacity] >= lifetime) first++;
        if (count - first < 2) return 0;
        float oldest = distances[(start + first) % capacity];
        float newest = distances[(start + count - 1) % capacity];
        float length = newest - oldest;
        if (length < 0.1f) return 0;
        float growing = Math.Clamp(length / 70f, 0f, 1f);
        int written = 0;
        for (int i = first; i < count; i++)
        {
            int index = (start + i) % capacity;
            float life = Math.Clamp(1f - (now - times[index]) / lifetime, 0f, 1f);
            float t = (distances[index] - oldest) / length;
            // 宽度向脚尖累积；最后少量路程收圆，避免三角形带出现平直截口。
            float cap = MathF.Sqrt(Math.Clamp((newest - distances[index]) / 13f, 0f, 1f));
            float width = halfWidth * MathF.Pow(t, 0.68f) * cap * growing;
            Vector2 normal = normals[index];
            normal = normal.LengthSquared() > 0.0001f ? Vector2.Normalize(normal) : Vector2.UnitY;
            Vector2 point = positions[index] - screenPosition;
            Color color = new(1f, 1f, 1f, life * Math.Clamp(t * 6f, 0f, 1f));
            float u = distances[index] / 96f;
            // 内缘靠近脚、外缘向外翻卷；半径不随尾迹年龄变化。
            Vertices[written++] = new(new Vector3(point - normal * width * 0.90f, 0), color, new Vector2(u, 0));
            Vertices[written++] = new(new Vector3(point + normal * width * 1.10f, 0), color, new Vector2(u, 1));
        }
        return written;
    }
}
