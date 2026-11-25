using System;
using CharacterRely.Animation;
using CharacterRely.Content.SkillSystem;
using Microsoft.Xna.Framework;
using Mokou_Character.Character;
using Mokou_Character.SkillVisuals;
using Terraria;
using Terraria.ModLoader;

namespace Mokou_Character.Skill;

/// <summary>普通左右手交替斜砍，强化双手同时交叉斜砍；每只手三道爪痕。</summary>
public sealed class MokouClawSkill : CharacterRely.Content.SkillSystem.Skill, IMokouActionSkill
{
    // 保留注册 ID，已有存档的技能装配不受显示名称修改影响。
    public const string SkillId = "flame_claw";
    /// <summary>成功释放时立即扣除的生命，必须至少保留 1 点生命。</summary>
    public const int LifeCost = 10;
    private const int SwingTicks = 24;
    private const int StrikeTick = 8;
    private const int StrikeCount = 2;
    // 普通两击，强化双手合并为一击；阶段伤害与装备加成在起手时锁定。
    private MokouSkillDamageSnapshot damageSnapshot;
    public const float Reach = 80f;

    private static readonly SkillInfo skillInfo = new(
        SkillId, "自伤的火爪", "左、右手交替贴身斜砍，每次留下三道火焰爪痕，攻击身前80像素内的整个范围。人体自燃强化时，改为双手同时交叉斜砍，伤害提高50%。伤害随世界BOSS进度成长，只受魔法与通用伤害加成（通用只计一次）。成功释放时立即消耗10点生命，至少保留1点生命，冷却1秒；整套结束后结算自燃进度与强化回血。",
        cooldownTicks: 60, durationTicks: SwingTicks + StrikeTick + MokouFlameClawProjectile.Lifetime, cost: 0);

    private SpinePlayer animator;
    private Vector2 aimTarget;
    private int swingIndex = -1;
    private int facing;
    private float aimAngle;
    // 起手时锁定，三道火爪共享同一次强化，不再读取正在变化的自燃进度。
    private bool empoweredCast, ownsPose;
    /// <summary>挥爪动作收势后，爪痕可以继续存在而不占用新动作。</summary>
    public bool IsPerformingAction => IsActive && ownsPose;
    private readonly System.Collections.Generic.List<(Projectile Projectile, int Identity)> claws = new(2);
    /// <summary>供手部火焰决定当前是哪一只手挥爪；强化时双手同时燃烧。</summary>
    public bool FrontHandActive => IsPerformingAction && (empoweredCast || swingIndex == 0);
    public bool BackHandActive => IsPerformingAction && (empoweredCast || swingIndex == 1);

    public MokouClawSkill() : base(skillInfo) { }

    protected override bool CanUse() => !Main.dedServ && Player.whoAmI == Main.myPlayer
        && !Player.dead && !Player.CCed && !Player.pulley && !Player.mount.Active
        && Player.itemAnimation == 0 && Player.heldProj < 0
        && Owner is Mokou character && character.CanSpendSkillLife(LifeCost) && !character.IsNirvana && !character.IsPhoenixChanting
        && !character.HasBlockingSkillAction(this);

    protected override bool OnUse()
        => ((Mokou)Owner).TryUseSkillWithLifeCost(this, LifeCost, BeginCast);

    private bool BeginCast(bool empowered)
    {
        var character = (Mokou)Owner;
        empoweredCast = empowered;
        damageSnapshot = MokouSkillBalance.Capture(Player, empowered);
        _ = character.VanillaActions;
        animator = character.Animator;
        swingIndex = -1;
        claws.Clear();
        BeginSwing(0);
        ApplyPose(0, 0);
        return true;
    }

    protected override void UpdateActive()
    {
        if (Player.dead || Player.CCed || Player.pulley || Player.mount.Active || ((Mokou)Owner).IsNirvana
            || animator == null || animator.IsDisposed)
        {
            End(interrupted: true);
            return;
        }
        int swings = empoweredCast ? 1 : StrikeCount;
        if (ElapsedTicks > SwingTicks * swings)
        {
            ReleasePose();
            // 动作已收势，但最后的爪痕仍属于本次技能，消散完才结束结算。
            if (ElapsedTicks >= SwingTicks * (swings - 1) + StrikeTick + MokouFlameClawProjectile.Lifetime) End();
            return;
        }
        int index = Math.Min((ElapsedTicks - 1) / SwingTicks, StrikeCount - 1);
        int phase = (ElapsedTicks - 1) % SwingTicks;
        if (index != swingIndex) BeginSwing(index);
        Player.ChangeDir(facing);
        animator.FlipX = facing < 0;
        ApplyPose(index, phase);
        if (phase == StrikeTick - 1) Strike(index);
    }

    private void BeginSwing(int index)
    {
        swingIndex = index;
        // 每次起手重新瞄准，挥出期间锁定，避免整条手臂突然左右翻转。
        aimTarget = Main.MouseWorld;
        facing = aimTarget.X < Player.Center.X ? -1 : 1;
        Player.ChangeDir(facing);
        animator.FlipX = facing < 0;
        Vector2 aim = aimTarget - Player.MountedCenter;
        aimAngle = MathHelper.Clamp(MathF.Atan2(aim.Y, Math.Max(1f, aim.X * facing)), -1.05f, 1.05f);
    }

    private void ApplyPose(int index, int phase)
    {
        ownsPose = true;
        float windup = Ease(phase / 6f);
        // 与爪痕五帧展开使用同一个进度，避免手已挥完、爪痕才开始出现。
        float slash = Ease((phase - StrikeTick + 2) / 5f);
        float strength = phase < 12 ? windup : 1 - Ease((phase - 12) / 11f);
        float sweep = MathHelper.Lerp(-1.15f, 0.95f, slash);
        float twist = empoweredCast ? 0.12f : (index == 0 ? 1 : -1) * sweep * 0.18f;
        animator.SetBoneRotationOffset("body_1", twist * strength);
        animator.SetBoneRotationOffset("head", (aimAngle * 0.3f - twist * 0.6f) * strength);
        PoseArm("front_arm", "front_hand", empoweredCast || index == 0, slash, strength, 0);
        PoseArm("back_arm", "back_hand", empoweredCast || index == 1, slash, strength, 1);
    }

    private void PoseArm(string arm, string hand, bool attacking, float slash, float strength, int handIndex)
    {
        animator.SetBoneRotationOffset(arm, 0);
        animator.SetBoneRotationOffset(hand, 0);
        if (!attacking)
        {
            animator.SetBoneRotationOffset(arm, 0.18f * strength);
            animator.SetBoneRotationOffset(hand, -0.35f * strength);
            return;
        }
        // 手臂追踪同一条斜挥中心线，左右镜像在 ClawPoint 内统一处理。
        if (!animator.TryGetBoneTransform(arm, out Matrix transform)) return;
        float current = MathF.Atan2(transform.M12, transform.M11);
        Vector2 forward = new(MathF.Cos(aimAngle) * facing, MathF.Sin(aimAngle));
        Vector2 point = MokouSkillMotion.ClawPoint(Player.MountedCenter, forward, facing, Reach,
            handIndex == 0 ? 1 : -1, 1, handIndex == 0 ? slash : 1f - slash);
        Vector2 shoulder = Player.Bottom + new Vector2(transform.M41, transform.M42) * Owner.CharacterScale;
        Vector2 target = (point - shoulder).SafeNormalize(forward);
        float delta = MathHelper.WrapAngle(target.ToRotation() - current) * facing;
        animator.SetBoneRotationOffset(arm, delta * strength);

        // 小臂也朝向爪痕，火焰沿 front_hand / back_hand 的实际方向延伸。
        if (animator.TryGetBoneTransform(hand, out Matrix handTransform))
        {
            Vector2 wrist = Player.Bottom + new Vector2(handTransform.M41, handTransform.M42) * Owner.CharacterScale;
            float handAngle = MathF.Atan2(handTransform.M12, handTransform.M11);
            float desired = (point - wrist).SafeNormalize(target).ToRotation();
            animator.SetBoneRotationOffset(hand, MathHelper.WrapAngle(desired - handAngle) * facing * strength);
        }
    }

    private static float Ease(float value)
        => MokouSkillMotion.Smooth(value);

    private void Strike(int index)
    {
        Vector2 direction = new(MathF.Cos(aimAngle) * facing, MathF.Sin(aimAngle));
        // 不沿瞄准方向远推中心；贴身范围与三道爪痕使用同一个80像素半径。
        int slot = Projectile.NewProjectile(Player.GetSource_FromThis("CharacterSkill:" + Id), Player.MountedCenter, direction,
            ModContent.ProjectileType<MokouFlameClawProjectile>(),
            damageSnapshot.Resolve(damageSnapshot.Base.Claw, empoweredCast ? StrikeCount : 1),
            2f, Player.whoAmI, empoweredCast ? 2 : index, facing);
        if ((uint)slot < Main.maxProjectiles) claws.Add((Main.projectile[slot], Main.projectile[slot].identity));
    }

    private void ReleasePose()
    {
        if (!ownsPose) return;
        ownsPose = false;
        if (animator != null && !animator.IsDisposed)
        {
            animator.SetBoneRotationOffset("front_arm", 0f);
            animator.SetBoneRotationOffset("front_hand", 0f);
            animator.SetBoneRotationOffset("back_arm", 0f);
            animator.SetBoneRotationOffset("back_hand", 0f);
            animator.SetBoneRotationOffset("body_1", 0f);
            animator.SetBoneRotationOffset("head", 0f);
        }
    }

    protected override void OnEnd(bool interrupted)
    {
        if (interrupted)
            foreach (var (projectile, identity) in claws)
                if (projectile.active && projectile.identity == identity && projectile.owner == Player.whoAmI)
                    projectile.Kill();
        claws.Clear();
        empoweredCast = false;
        ReleasePose();
        animator = null;
        ((Mokou)Owner).CompleteSkillLifeUse(this);
    }
}
