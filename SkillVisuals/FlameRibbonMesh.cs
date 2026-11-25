using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Mokou_Character.SkillVisuals;

/// <summary>把按时间记录的世界路径转换成连续火焰带；复用顶点数组，不持有 GPU 资源。</summary>
internal sealed class FlameRibbonMesh
{
    internal VertexPositionColorTexture[] Vertices { get; }
    internal FlameRibbonMesh(int capacity) => Vertices = new VertexPositionColorTexture[capacity * 2];

    /// <summary>normals 始终指向轨迹外侧；distance 使用累计路程，淘汰旧点不会重置纹理。</summary>
    internal int Build(Vector2[] positions, Vector2[] normals, float[] times, float[] distance,
        int start, int count, float now, float lifetime, float halfWidth, Vector2 screenPosition)
    {
        if (count < 2 || lifetime <= 0f || halfWidth <= 0f) return 0;
        int capacity = positions.Length;
        int first = 0;
        while (first < count && now - times[(start + first) % capacity] >= lifetime) first++;
        if (count - first < 2) return 0;
        float oldest = distance[(start + first) % capacity];
        float newest = distance[(start + count - 1) % capacity];
        float length = newest - oldest;
        if (length < 0.1f) return 0;
        float taperLength = Math.Min(24f, length * 0.25f);
        int vertexCount = 0;
        for (int i = first; i < count; i++)
        {
            int index = (start + i) % capacity;
            float life = Math.Clamp(1f - (now - times[index]) / lifetime, 0f, 1f);
            float tail = Smooth((distance[index] - oldest) / taperLength);
            float head = Smooth((newest - distance[index]) / taperLength);
            float width = halfWidth * MathF.Sqrt(life) * tail * head;
            Vector2 normal = normals[index];
            float normalLength = normal.LengthSquared();
            normal = normalLength > 0.0001f ? normal / MathF.Sqrt(normalLength) : Vector2.UnitY;
            Vector2 point = positions[index] - screenPosition;
            float capFade = Smooth((distance[index] - oldest) / 8f) * Smooth((newest - distance[index]) / 8f);
            Color color = new(1f, 1f, 1f, life * capFade);
            float u = distance[index] / 96f;
            Vertices[vertexCount++] = new VertexPositionColorTexture(new Vector3(point - normal * width, 0f), color, new Vector2(u, 0f));
            Vertices[vertexCount++] = new VertexPositionColorTexture(new Vector3(point + normal * width, 0f), color, new Vector2(u, 1f));
        }
        return vertexCount;
    }

    private static float Smooth(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
