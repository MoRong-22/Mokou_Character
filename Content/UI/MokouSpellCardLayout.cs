using System;
using Microsoft.Xna.Framework;

namespace Mokou_Character.Content.UI;

/// <summary>四张符卡共用的布局与旋转命中；位置、旋转和绘制顺序只在这里定义。</summary>
internal static class MokouSpellCardLayout
{
    internal static readonly Vector2 BarSize = new(344f, 184f);
    internal static readonly Vector2 CardSize = new(72f, 116f);
    internal static readonly Vector2 KeyCenter = new(0f, -73f);
    internal static readonly Vector2 KeySize = new(46f, 20f);
    // 焦边短符：保持图形和标题完整露出，每张轻微错开高度与角度。
    private static readonly float[] Angles = { -0.10472f, 0.05236f, -0.05236f, 0.10472f };
    private static readonly float[] Heights = { 98f, 90f, 99f, 89f };

    internal static float Rotation(int slot) => Angles[slot];
    internal static Vector2 Center(Rectangle bar, int slot)
        => new(bar.X + 49f + slot * 82f, bar.Y + Heights[slot]);

    internal static Vector2 Rotate(Vector2 point, float rotation)
    {
        float c = MathF.Cos(rotation), s = MathF.Sin(rotation);
        return new Vector2(point.X * c - point.Y * s, point.X * s + point.Y * c);
    }

    internal static Vector2 ToUI(Rectangle bar, int slot, Vector2 local)
        => Center(bar, slot) + Rotate(local, Rotation(slot));

    internal static int HitTest(Rectangle bar, Vector2 mouse)
    {
        // 绘制 0→3，命中必须 3→0，不能让被遮盖的符卡收到点击。
        for (int slot = 3; slot >= 0; slot--)
        {
            Vector2 local = Rotate(mouse - Center(bar, slot), -Rotation(slot));
            if (Math.Abs(local.X) <= CardSize.X * 0.5f && Math.Abs(local.Y) <= CardSize.Y * 0.5f)
                return slot;
            // 上方按键牌也属于此槽位，悬停介绍与右键装配使用同一命中规则。
            Vector2 key = local - KeyCenter;
            if (Math.Abs(key.X) <= KeySize.X * 0.5f && Math.Abs(key.Y) <= KeySize.Y * 0.5f)
                return slot;
        }
        return -1;
    }
}
