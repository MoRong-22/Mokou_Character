using System;
using CharacterRely.Animation;
using Microsoft.Xna.Framework;

namespace Mokou_Character.SkillVisuals;

/// <summary>技能共用的缓动、朝向和范围计算；不依赖鼠标或玩家全局状态。</summary>
internal static class MokouSkillMotion
{
    internal static float Smooth(float value)
    {
        value = Math.Clamp(value, 0, 1);
        return value * value * (3 - 2 * value);
    }

    /// <summary>沿骨骼自身 X 轴对准屏幕方向。已有偏移需传入；镜像只在这里处理一次。</summary>
    internal static float AimBone(SpinePlayer animator, string bone, Vector2 direction, int facing, float offset = 0)
    {
        if (!animator.TryGetBoneTransform(bone, out Matrix transform)) return offset;
        float actual = MathF.Atan2(transform.M12, transform.M11);
        float target = MathF.Atan2(direction.Y, direction.X);
        float result = offset + MathHelper.WrapAngle(target - actual) * facing;
        animator.SetBoneRotationOffset(bone, result);
        return result;
    }

    /// <summary>矩形与正面半圆相交；内部也命中，不只检测爪痕细线。forward 必须为单位向量。</summary>
    internal static bool InFrontArea(Vector2 origin, Vector2 forward, float radius, Rectangle target)
    {
        Vector2 nearest = Vector2.Clamp(origin,
            new Vector2(target.Left, target.Top), new Vector2(target.Right, target.Bottom));
        if (Vector2.DistanceSquared(origin, nearest) > radius * radius) return false;
        if (Vector2.Dot(nearest - origin, forward) >= 0) return true;
        // 最近点在后半圆时，只可能跨过半圆的直径边界；用线段裁剪精确判定。
        Vector2 side = new(-forward.Y, forward.X);
        Vector2 a = origin - side * radius, delta = side * radius * 2;
        float lo = 0, hi = 1;
        return Clip(a.X, delta.X, target.Left, target.Right, ref lo, ref hi)
            && Clip(a.Y, delta.Y, target.Top, target.Bottom, ref lo, ref hi);
    }

    private static bool Clip(float start, float delta, float min, float max, ref float lo, ref float hi)
    {
        if (Math.Abs(delta) < 0.00001f) return start >= min && start <= max;
        float a = (min - start) / delta, b = (max - start) / delta;
        lo = Math.Max(lo, Math.Min(a, b)); hi = Math.Min(hi, Math.Max(a, b));
        return lo <= hi;
    }

    /// <summary>爪痕在身前形成倾斜弧，三条平行排列；左右朝向由同一组局部坐标镜像。</summary>
    internal static Vector2 ClawPoint(Vector2 origin, Vector2 forward, int facing, float radius,
        int slashSign, int lane, float progress)
    {
        float u = progress * 2 - 1;
        float x = radius * (0.40f + 0.24f * (1 - u * u) + slashSign * u * 0.25f)
            + (lane - 1) * radius * 0.08f;
        float y = u * radius * 0.68f;
        Vector2 side = new Vector2(-forward.Y, forward.X) * facing;
        return origin + forward * x + side * y;
    }

    internal static float DashSpeedAt(int tick, int travelTicks, float peakSpeed)
    {
        float launch = Smooth((tick + 1) / 3f);
        float braking = Smooth((tick - (travelTicks - 6)) / 5f);
        return MathHelper.Lerp(peakSpeed * 0.55f, peakSpeed, launch) * MathHelper.Lerp(1, 0.58f, braking);
    }
}
