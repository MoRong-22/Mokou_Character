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

    // 爪痕形状和动作共用这组参数。角度设为 0 就是横挥，正负角度对应两只手。
    internal const float ClawTilt = MathHelper.Pi / 6f;
    internal static readonly Vector2 ClawCanvas = new(1.4f, 1.05f);
    internal static Vector2 ClawAxis(Vector2 forward, int facing, int slashSign)
    {
        Vector2 side = new(-forward.Y * facing, forward.X * facing);
        float angle = ClawTilt * slashSign;
        return forward * MathF.Cos(angle) + side * MathF.Sin(angle);
    }

    /// <summary>与 Claw shader 的中心线一致：身前斜挥、弧形展开，三条爪痕渐渐分开。</summary>
    internal static Vector2 ClawPoint(Vector2 origin, Vector2 forward, int facing, float radius,
        int slashSign, int lane, float progress)
    {
        float u = progress * 2f - 1f;
        Vector2 axis = ClawAxis(forward, facing, slashSign);
        Vector2 normal = new(-axis.Y * facing, axis.X * facing);
        float along = u * 0.445f * ClawCanvas.X * radius;
        float arc = (0.10f + lane * 0.018f) * (1f - u * u);
        float across = ((lane - 1) * (0.205f + 0.025f * progress) + arc) * ClawCanvas.Y * radius;
        return origin + forward * (radius * 0.50f) + axis * along + normal * across;
    }

    internal static float DashSpeedAt(int tick, int travelTicks, float peakSpeed)
    {
        float launch = Smooth((tick + 1) / 3f);
        float braking = Smooth((tick - (travelTicks - 6)) / 5f);
        return MathHelper.Lerp(peakSpeed * 0.55f, peakSpeed, launch) * MathHelper.Lerp(1, 0.58f, braking);
    }
}
