using System;
using Microsoft.Xna.Framework;

namespace Mokou_Character.Skill;

/// <summary>传送技能的时序与数值；时间以游戏帧计，地图位置以物块计。</summary>
internal static class ResurrectionRules
{
    internal const int CooldownTicks = 30 * 60;
    internal const int BurnTicks = 42;
    internal const int AppearTicks = 30;
    internal const int NetworkTimeoutTicks = 5 * 60;

    /// <summary>当前实际累计层数的二分之一，舍去小数；不额外触发满层技能的 100 点奖励。</summary>
    internal static int Healing(float stacks)
        => float.IsFinite(stacks) ? (int)(Math.Clamp(stacks, 0f, int.MaxValue / 2f) / 2f) : 0;

    /// <summary>全屏地图使用屏幕原始像素和单位矩阵，不叠加 UI 或世界缩放。</summary>
    internal static Vector2 MapToWorld(Vector2 mouse, Vector2 screenSize, Vector2 mapCenter, float mapScale)
        => (mapCenter + (mouse - screenSize * 0.5f) / mapScale) * 16f;

    /// <summary>先烧黑并消失；抵达后从火中恢复，首尾严格回到 0/1。</summary>
    internal static float BodyOpacity(float ticks, bool arriving)
        => arriving ? MathHelper.SmoothStep(0f, 1f, Math.Clamp(ticks / AppearTicks, 0f, 1f))
            : 1f - MathHelper.SmoothStep(0f, 1f, Math.Clamp((ticks - 8f) / (BurnTicks - 14f), 0f, 1f));
}
