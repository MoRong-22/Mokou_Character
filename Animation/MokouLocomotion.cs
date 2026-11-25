using System;
using Microsoft.Xna.Framework;
using Spine;

namespace Mokou_Character.Animation;

/// <summary>基础动作调节项：距离为游戏像素，角度为度，速度为每秒循环次数。</summary>
public sealed class MokouLocomotionSettings
{
    /// <summary>待机呼吸的身体升降幅度。</summary>
    public float BreathHeight { get; set; } = 0.32f;
    /// <summary>迈步时脚踝相对站姿的前后距离。</summary>
    public float StepReach { get; set; } = 4.2f;
    /// <summary>摆动腿抬脚高度，支撑阶段不抬脚。</summary>
    public float StepHeight { get; set; } = 2.4f;
    /// <summary>跑动时的最大前倾角。</summary>
    public float RunLean { get; set; } = 9f;
    /// <summary>水平飞行时的额外前倾角。</summary>
    public float FlightLean { get; set; } = 15f;
    /// <summary>姿势接近目标的时间，越小响应越快。</summary>
    public float BlendSeconds { get; set; } = 0.10f;
    /// <summary>飞行时每秒拍翼次数。</summary>
    public float WingFrequency { get; set; } = 2.2f;
}

internal enum LocomotionState { Idle, Move, Rise, Fall, Fly }

/// <summary>玩家运动快照；本模块不修改玩家速度、碰撞或技能数值。</summary>
internal readonly record struct LocomotionInput(Vector2 Velocity, bool Grounded,
    bool PoweredFlight, bool Suspended, bool UpperBodyAction);

/// <summary>
/// Tick 每游戏帧推进一次；Apply 只重建世界矩阵，重复绘制不会累加旋转。
/// 保留原始 Spine 文件和局部属性，技能与原版持物动作仍可接管。
/// </summary>
internal sealed class MokouLocomotion
{
    private readonly Skeleton target, pose;
    private readonly MokouLocomotionSettings settings;
    private readonly float modelScale;
    private readonly float[] rotations, positionsX, positionsY, scalesX;
    private readonly bool[] legBones;
    private readonly Leg frontLeg, backLeg;
    private readonly Bone hips, chest, head, frontArm, backArm, frontHand, backHand;
    private readonly Bone[] hair, wings;
    private float clock, stepPhase, wingPhase, speed, landing, previousY;
    private bool groundedLast, initialized;
    internal LocomotionState State { get; private set; }

    internal MokouLocomotion(Skeleton skeleton, float scale, MokouLocomotionSettings settings)
    {
        target = skeleton;
        pose = new Skeleton(skeleton.Data);
        modelScale = Math.Max(0.001f, scale);
        this.settings = settings;
        int count = pose.Bones.Count;
        rotations = new float[count]; positionsX = new float[count]; positionsY = new float[count];
        scalesX = new float[count]; legBones = new bool[count];
        hips = Find("body_2"); chest = Find("body_1"); head = Find("head");
        frontArm = Find("front_arm"); backArm = Find("back_arm");
        frontHand = Find("front_hand"); backHand = Find("back_hand");
        hair = new[] { Find("back_fair_2"), Find("back_fair_3"), Find("back_fair_4"), Find("back_fair_5"), Find("back_fair_6") };
        wings = new[] { Find("front_wing_1"), Find("back_wing_1"), Find("front_wing_2"), Find("back_wing_2") };
        pose.UpdateWorldTransform();
        frontLeg = new Leg(Find("front_leg_1"), Find("front_leg_2"), Find("front_foot"));
        backLeg = new Leg(Find("back_leg_1"), Find("back_leg_2"), Find("back_foot"));
        foreach (Leg leg in new[] { frontLeg, backLeg })
        {
            legBones[leg.Upper.Data.Index] = true;
            legBones[leg.Lower.Data.Index] = true;
            legBones[leg.Foot.Data.Index] = true;
        }
        CachePose(1f);
    }

    private Bone Find(string name) => pose.FindBone(name)
        ?? throw new InvalidOperationException("妹红基础动作缺少骨骼：" + name);

    internal void Tick(LocomotionInput input, float dt)
    {
        dt = Math.Clamp(dt, 0f, 0.05f);
        State = SelectState(input, State);
        if (input.Suspended)
        {
            initialized = false; groundedLast = input.Grounded; previousY = input.Velocity.Y; landing = 0f;
            return;
        }
        clock = (clock + dt) % 120f;
        speed = MathHelper.Lerp(speed, Math.Abs(input.Velocity.X), 1f - MathF.Exp(-dt * 12f));
        if (input.Grounded && !groundedLast && initialized) landing = Math.Clamp(previousY / 12f, 0f, 1f);
        landing = Math.Max(0f, landing - dt * 4f);
        previousY = input.Velocity.Y; groundedLast = input.Grounded;
        float move = State == LocomotionState.Move ? Math.Clamp(speed / 3f, 0.15f, 1f) : 0f;
        if (move > 0f) stepPhase = (stepPhase + dt * Math.Clamp(0.9f + speed * 0.32f, 0.9f, 3.4f)) % 1f;
        wingPhase = (wingPhase + dt * Math.Clamp(settings.WingFrequency, 0.2f, 6f)) % 1f;
        pose.SetBonesToSetupPose();
        float breath = MathF.Sin(clock * MathHelper.TwoPi / 3.2f);
        float stride = MathF.Cos(stepPhase * MathHelper.TwoPi);
        float wind = Math.Clamp(speed / 8f, 0f, 1f);
        float lean;
        if (input.Grounded)
        {
            hips.Y += (breath * settings.BreathHeight * (1f - move)
                - move * (0.30f + 0.28f * MathF.Cos(stepPhase * MathHelper.TwoPi * 2f))
                - landing * 1.5f) / modelScale;
            lean = -settings.RunLean * move * Math.Clamp(speed / 6f, 0.35f, 1f);
            chest.Rotation += lean + breath * 0.65f * (1f - move);
            head.Rotation -= lean * 0.78f + breath * 0.4f;
            frontArm.Rotation += 2f - stride * 25f * move + breath * 1.2f * (1f - move);
            backArm.Rotation += -2f + stride * 23f * move - breath * 0.9f * (1f - move);
            frontHand.Rotation += 7f + 14f * move + Math.Max(0, stride) * 12f * move;
            backHand.Rotation += 10f + 17f * move + Math.Max(0, -stride) * 10f * move;
        }
        else
        {
            bool flight = State == LocomotionState.Fly;
            float rise = Math.Clamp(-input.Velocity.Y / 8f, 0f, 1f);
            float fall = Math.Clamp(input.Velocity.Y / 10f, 0f, 1f);
            lean = -(flight ? 4f + settings.FlightLean * wind : 5f * wind);
            hips.Rotation += lean; chest.Rotation -= flight ? 3f : 1f;
            head.Rotation -= lean * 0.85f;
            // 空中不蹬步：飞行前腿收起、后腿稍垂；起跳收腿、下落伸腿。
            frontLeg.Upper.Rotation += flight ? 30f : 17f * rise - 5f * fall;
            frontLeg.Lower.Rotation += flight ? -52f : -35f * rise - 7f * fall;
            backLeg.Upper.Rotation += flight ? -12f : -12f * rise + 8f * fall;
            backLeg.Lower.Rotation += flight ? -24f : -19f * rise - 12f * fall;
            frontLeg.Foot.Rotation += flight ? 12f : 10f * rise - 4f * fall;
            backLeg.Foot.Rotation += flight ? 6f : -3f * fall;
            frontArm.Rotation += flight ? -17f - 9f * wind : -9f * rise - 18f * fall;
            backArm.Rotation += flight ? 13f + 8f * wind : 8f * rise + 16f * fall;
            frontHand.Rotation += flight ? 24f : 12f + 8f * rise;
            backHand.Rotation += flight ? 32f : 16f + 8f * rise;
            hips.Y += (flight ? MathF.Sin(wingPhase * MathHelper.TwoPi - 0.6f) * 0.45f : 0f) / modelScale;
        }
        // 马尾逐节滞后，翼尖也滞后翼根；不让整束头发与整片翅膀像硬板同步旋转。
        for (int i = 0; i < hair.Length; i++)
            hair[i].Rotation += -(3f + 2f * i) * wind
                + MathF.Sin(clock * (input.Grounded ? 2.4f : 4.5f) - i * 0.65f) * (1.2f + i * 0.65f + wind * 2f);
        PoseWings(input.Grounded, State == LocomotionState.Fly);
        if (input.UpperBodyAction)
        {
            hips.X = hips.Data.X; hips.Y = hips.Data.Y; hips.Rotation = hips.Data.Rotation;
        }
        pose.UpdateWorldTransform();
        if (input.Grounded)
        {
            PlantFoot(frontLeg, stepPhase, move);
            PlantFoot(backLeg, (stepPhase + 0.5f) % 1f, move);
        }
        float blend = initialized ? 1f - MathF.Exp(-dt / Math.Clamp(settings.BlendSeconds, 0.02f, 0.5f)) : 1f;
        CachePose(blend);
        initialized = true;
    }

    internal static LocomotionState SelectState(LocomotionInput input, LocomotionState previous)
    {
        if (input.Suspended) return LocomotionState.Idle;
        if (!input.Grounded) return input.PoweredFlight ? LocomotionState.Fly
            : input.Velocity.Y < -0.65f ? LocomotionState.Rise : LocomotionState.Fall;
        float threshold = previous == LocomotionState.Move ? 0.10f : 0.22f;
        return Math.Abs(input.Velocity.X) > threshold ? LocomotionState.Move : LocomotionState.Idle;
    }

    private void PoseWings(bool grounded, bool powered)
    {
        float flap = MathF.Cos(wingPhase * MathHelper.TwoPi);
        float tip = MathF.Cos(wingPhase * MathHelper.TwoPi - 0.65f);
        float amplitude = grounded ? 2f : powered ? 23f : 6f;
        wings[0].Rotation += (grounded ? -14f : -4f) + flap * amplitude;
        wings[1].Rotation += (grounded ? 14f : 4f) - flap * amplitude;
        wings[2].Rotation += (grounded ? -9f : -3f) + tip * amplitude * 0.60f;
        wings[3].Rotation += (grounded ? 9f : 3f) - tip * amplitude * 0.60f;
        // 轻微透视变化，缩放始终为正，不翻面。
        wings[0].ScaleX *= 1f - (powered ? 0.12f : 0.02f) * (flap + 1f) * 0.5f;
        wings[1].ScaleX *= 1f - (powered ? 0.10f : 0.02f) * (flap + 1f) * 0.5f;
    }

    private void PlantFoot(Leg leg, float phase, float amount)
    {
        // 前60%为支撑：从前向后；后40%抬脚画弧返回。两腿错开半周期。
        float x, lift;
        if (phase < 0.6f) { x = 1f - phase / 0.6f * 2f; lift = 0f; }
        else { float t = (phase - 0.6f) / 0.4f; x = -MathF.Cos(t * MathHelper.Pi); lift = MathF.Sin(t * MathHelper.Pi); }
        Vector2 ankle = leg.Ankle + new Vector2(x * settings.StepReach, lift * settings.StepHeight) * (amount / modelScale);
        Bone upper = leg.Upper, lower = leg.Lower, foot = leg.Foot;
        upper.Parent.WorldToLocal(ankle.X, ankle.Y, out float tx, out float ty);
        Vector2 delta = new(tx - upper.X, ty - upper.Y);
        // 使用真实骨骼间距而非 Length：模型的小腿长度字段并不等于踝点距离。
        float l1 = new Vector2(lower.X, lower.Y).Length(), l2 = new Vector2(foot.X, foot.Y).Length();
        float cosine = Math.Clamp((delta.LengthSquared() - l1 * l1 - l2 * l2) / (2f * l1 * l2), -0.999f, 0.999f);
        float knee = -MathF.Acos(cosine);
        float thigh = MathF.Atan2(delta.Y, delta.X) - MathF.Atan2(l2 * MathF.Sin(knee), l1 + l2 * MathF.Cos(knee));
        float offset1 = MathF.Atan2(lower.Y, lower.X), offset2 = MathF.Atan2(foot.Y, foot.X);
        upper.Rotation = MathHelper.ToDegrees(thigh - offset1);
        lower.Rotation = MathHelper.ToDegrees(knee + offset1 - offset2);
        upper.UpdateWorldTransform(); lower.UpdateWorldTransform();
        float angle = MathHelper.ToRadians(leg.FootAngle + lift * 15f * amount);
        Bone parent = foot.Parent;
        float sin = MathF.Sin(angle), cos = MathF.Cos(angle);
        foot.Rotation = MathHelper.ToDegrees(MathF.Atan2(parent.A * sin - parent.C * cos,
            parent.D * cos - parent.B * sin));
        foot.UpdateWorldTransform();
    }

    private void CachePose(float blend)
    {
        for (int i = 0; i < pose.Bones.Count; i++)
        {
            Bone b = pose.Bones.Items[i];
            rotations[i] += MathHelper.ToDegrees(MathHelper.WrapAngle(MathHelper.ToRadians(b.Rotation - rotations[i]))) * blend;
            positionsX[i] = MathHelper.Lerp(positionsX[i], b.X, blend);
            positionsY[i] = MathHelper.Lerp(positionsY[i], b.Y, blend);
            scalesX[i] = MathHelper.Lerp(scalesX[i], b.ScaleX, blend);
        }
    }

    /// <summary>从缓存姿势重建世界矩阵，镜像只交给根骨骼处理；原版手臂随后覆盖。</summary>
    internal void Apply(bool suspended, bool upperBodyAction)
    {
        if (suspended || !initialized) return;
        for (int i = 0; i < target.Bones.Count; i++)
        {
            Bone b = target.Bones.Items[i];
            if (upperBodyAction && !legBones[i]) b.UpdateWorldTransform();
            else b.UpdateWorldTransform(positionsX[i], positionsY[i], rotations[i], scalesX[i],
                b.Data.ScaleY, b.Data.ShearX, b.Data.ShearY);
        }
    }

    private sealed class Leg
    {
        internal readonly Bone Upper, Lower, Foot;
        internal readonly Vector2 Ankle;
        internal readonly float FootAngle;
        internal Leg(Bone upper, Bone lower, Bone foot)
        {
            Upper = upper; Lower = lower; Foot = foot;
            Ankle = new(foot.WorldX, foot.WorldY);
            FootAngle = MathHelper.ToDegrees(MathF.Atan2(foot.C, foot.A));
        }
    }
}
