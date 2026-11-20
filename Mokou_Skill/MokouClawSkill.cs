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
public sealed class MokouClawSkill : CharacterRely.Content.SkillSystem.Skill
{
    // 保留注册 ID，已有存档的技能装配不受显示名称修改影响。
    public const string SkillId = "flame_claw";
    /// <summary>成功释放时立即扣除的生命，必须至少保留 1 点生命。</summary>
    public const int LifeCost = 50;
    private const int SwingTicks = 24;
    private const int StrikeTick = 8;
    private const int StrikeCount = 2;
    // 普通三次改成两次，整套基础伤害仍为60；强化双手一次结算90，避免同一双爪重复计伤。
    private const int Damage = 30;
    public const float Reach = 80f;

    private static readonly SkillInfo skillInfo = new(
        SkillId, "自伤的火爪", "左、右手交替贴身斜砍，每次留下三道火焰爪痕，攻击身前80像素内的整个范围。人体自燃强化时，改为双手同时交叉斜砍，伤害提高50%。成功释放时立即消耗50点生命，至少保留1点生命；整套结束后结算自燃进度与强化回血。",
        cooldownTicks: 120, durationTicks: SwingTicks + StrikeTick + MokouFlameClawProjectile.Lifetime, cost: 0);

    private SpinePlayer animator;
    private Vector2 aimTarget;
    private int swingIndex = -1;
    private int facing;
    private float aimAngle;
    // 起手时锁定，三道火爪共享同一次强化，不再读取正在变化的自燃进度。
    private bool empoweredCast;
    private readonly System.Collections.Generic.List<(Projectile Projectile, int Identity)> claws = new(2);
    /// <summary>供手部火焰决定当前是哪一只手挥爪；强化时双手同时燃烧。</summary>
    public bool FrontHandActive => IsActive && (empoweredCast || swingIndex == 0);
    public bool BackHandActive => IsActive && (empoweredCast || swingIndex == 1);

    public MokouClawSkill() : base(skillInfo) { }

    protected override bool CanUse() => !Main.dedServ && Player.whoAmI == Main.myPlayer
        && !Player.dead && !Player.CCed && !Player.pulley && !Player.mount.Active
        && Player.itemAnimation == 0 && Player.heldProj < 0
        && Owner is Mokou character && character.CanSpendSkillLife(LifeCost) && !character.IsNirvana && !character.IsPhoenixChanting
        && !character.KafuuKaiseyKickSkill.IsActive && !character.SacrificeStrikeSkill.IsActive;

    protected override bool OnUse()
        => ((Mokou)Owner).TryUseSkillWithLifeCost(LifeCost, BeginCast);

    private bool BeginCast(bool empowered)
    {
        var character = (Mokou)Owner;
        empoweredCast = empowered;
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
        float windup = Ease(phase / 6f);
        float slash = 1f - MathF.Pow(1f - Math.Clamp((phase - 6) / 5f, 0, 1), 3);
        float strength = phase < 12 ? windup : 1 - Ease((phase - 12) / 11f);
        float sweep = MathHelper.Lerp(-1.15f, 0.95f, slash);
        float twist = empoweredCast ? 0.12f : (index == 0 ? 1 : -1) * sweep * 0.18f;
        animator.SetBoneRotationOffset("body_1", twist * strength);
        animator.SetBoneRotationOffset("head", (aimAngle * 0.3f - twist * 0.6f) * strength);
        PoseArm("front_arm", "front_hand", empoweredCast || index == 0, sweep, strength, -1);
        PoseArm("back_arm", "back_hand", empoweredCast || index == 1, -sweep, strength, 1);
    }

    private void PoseArm(string arm, string hand, bool attacking, float sweep, float strength, int side)
    {
        animator.SetBoneRotationOffset(arm, 0);
        animator.SetBoneRotationOffset(hand, 0);
        if (!attacking)
        {
            animator.SetBoneRotationOffset(arm, 0.18f * strength);
            animator.SetBoneRotationOffset(hand, -0.35f * strength);
            return;
        }
        // 以真实骨骼朝向求偏移，避免把“垂下的手臂”的零度当成朝前。
        animator.TryGetBoneTransform(arm, out Matrix transform);
        float current = MathF.Atan2(transform.M12, transform.M11);
        Vector2 target = new(MathF.Cos(aimAngle + sweep) * facing, MathF.Sin(aimAngle + sweep));
        float delta = MathHelper.WrapAngle(target.ToRotation() - current) * facing;
        animator.SetBoneRotationOffset(arm, delta * strength);
        animator.SetBoneRotationOffset(hand, (side * 0.3f - sweep * 0.25f) * strength);
    }

    private static float Ease(float value)
        => MokouSkillMotion.Smooth(value);

    private void Strike(int index)
    {
        Vector2 direction = new(MathF.Cos(aimAngle) * facing, MathF.Sin(aimAngle));
        // 不沿瞄准方向远推中心；贴身范围与三道爪痕使用同一个80像素半径。
        int slot = Projectile.NewProjectile(Player.GetSource_Misc("CharacterSkill:" + Id), Player.MountedCenter, direction,
            ModContent.ProjectileType<MokouFlameClawProjectile>(),
            Mokou.GetEmpoweredSkillDamage(Damage * (empoweredCast ? StrikeCount : 1), empoweredCast),
            2f, Player.whoAmI, empoweredCast ? 2 : index, facing);
        if ((uint)slot < Main.maxProjectiles) claws.Add((Main.projectile[slot], Main.projectile[slot].identity));
    }

    protected override void OnEnd(bool interrupted)
    {
        if (interrupted)
            foreach (var (projectile, identity) in claws)
                if (projectile.active && projectile.identity == identity && projectile.owner == Player.whoAmI)
                    projectile.Kill();
        claws.Clear();
        empoweredCast = false;
        if (animator != null && !animator.IsDisposed)
        {
            animator.SetBoneRotationOffset("front_arm", 0f);
            animator.SetBoneRotationOffset("front_hand", 0f);
            animator.SetBoneRotationOffset("back_arm", 0f);
            animator.SetBoneRotationOffset("back_hand", 0f);
            animator.SetBoneRotationOffset("body_1", 0f);
            animator.SetBoneRotationOffset("head", 0f);
        }
        animator = null;
        ((Mokou)Owner).CompleteSkillLifeUse();
    }
}
