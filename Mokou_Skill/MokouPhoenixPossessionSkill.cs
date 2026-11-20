using System;
using CharacterRely.Animation;
using CharacterRely.Content.SkillSystem;
using Microsoft.Xna.Framework;
using Mokou_Character.Character;
using Terraria;
using Terraria.ModLoader;

namespace Mokou_Character.Skill;

/// <summary>原地吟唱后获得不死鸟形态；主动释放不具备被动重生的无敌和满血恢复。</summary>
public sealed class MokouPhoenixPossessionSkill : CharacterRely.Content.SkillSystem.Skill
{
    public const string SkillId = "phoenix_possession";
    /// <summary>成功释放时立即扣除的生命，必须至少保留 1 点生命。</summary>
    public const int LifeCost = 200;
    public const int ChantTicks = 3 * 60;
    public const int CooldownTicks = 45 * 60;
    private SpinePlayer animator;
    private Vector2 chantPosition;
    private int chantDirection;
    private Projectile vortex;

    public MokouPhoenixPossessionSkill() : base(new SkillInfo(
        SkillId, "不死鸟附体",
        "原地吟唱 3 秒，龙卷持续伤害接触的敌人，并吸附附近可被击退的敌人。完成后获得不死鸟形态，持续 30 秒：独立回血翻倍，飞行时间无限，并展开火焰翅膀。成功释放时立即消耗200点生命，至少保留1点生命；吟唱结束后结算自燃进度与强化回血，冷却45秒。",
        cooldownTicks: CooldownTicks, durationTicks: ChantTicks, maxCount: 1, cost: 0)) { }

    protected override bool CanUse()
        => !Main.dedServ && Player.whoAmI == Main.myPlayer && !Player.CCed
            && !Player.pulley && !Player.mount.Active && Player.grapCount == 0
            && Player.itemAnimation == 0 && Player.heldProj < 0
            && Owner is Mokou character && character.CanSpendSkillLife(LifeCost) && !character.IsNirvana && !character.IsPhoenixForm
            && !character.ClawSkill.IsActive
            && !character.KafuuKaiseyKickSkill.IsActive && !character.SacrificeStrikeSkill.IsActive;

    protected override bool OnUse()
        => ((Mokou)Owner).TryUseSkillWithLifeCost(LifeCost, BeginCast);

    private bool BeginCast(bool empowered)
    {
        var character = (Mokou)Owner;
        animator = character.Animator;
        chantPosition = Player.position;
        chantDirection = Player.direction;
        Player.velocity = Vector2.Zero;
        _ = character.VanillaActions;
        ApplyPose(0f);
        // 起手前判定，伤害随弹幕生成时同步；该龙卷后续每次命中都使用同一数值。
        int damage = Mokou.GetEmpoweredSkillDamage(MokouPhoenixVortexProjectile.BaseDamage,
            empowered);
        int index = Projectile.NewProjectile(Player.GetSource_Misc("CharacterSkill:" + Id),
            Player.Bottom - new Vector2(0f, 140f), Vector2.Zero,
            ModContent.ProjectileType<MokouPhoenixVortexProjectile>(), damage,
            0f, Player.whoAmI);
        vortex = (uint)index < (uint)Main.maxProjectiles ? Main.projectile[index] : null;
        // 生成失败时不消耗生命或强化；本方法内不会推进游戏帧。
        if (vortex == null || !vortex.active)
        {
            OnEnd(interrupted: true);
            return false;
        }
        return true;
    }

    protected override void UpdateActive()
    {
        if (Player.dead || Player.CCed || Player.pulley || Player.mount.Active || Player.grapCount > 0
            || ((Mokou)Owner).IsNirvana || animator == null || animator.IsDisposed)
        {
            End(interrupted: true);
            return;
        }
        HoldPosition();
        ApplyPose(MathHelper.Clamp(ElapsedTicks / (float)ChantTicks, 0f, 1f));
        if (ElapsedTicks >= ChantTicks)
        {
            ((Mokou)Owner).EnterPhoenixForm();
            End();
        }
    }

    internal void HoldPosition()
    {
        Player.position = chantPosition;
        Player.velocity = Vector2.Zero;
        Player.ChangeDir(chantDirection);
    }

    private void ApplyPose(float progress)
    {
        float open = MathHelper.SmoothStep(0f, 1f, Math.Min(1f, progress * 4f));
        float pulse = MathF.Sin(progress * MathHelper.TwoPi * 3f) * 0.035f * open;
        animator.SetBoneRotationOffset("body_1", -0.08f * open);
        animator.SetBoneRotationOffset("head", -0.3f * open);
        animator.SetBoneRotationOffset("front_arm", (-0.7f + pulse) * open);
        animator.SetBoneRotationOffset("back_arm", (0.65f - pulse) * open);
        animator.SetBoneRotationOffset("front_hand", -0.35f * open);
        animator.SetBoneRotationOffset("back_hand", 0.35f * open);
    }

    protected override void OnEnd(bool interrupted)
    {
        if (vortex != null && vortex.active && vortex.owner == Player.whoAmI
            && vortex.type == ModContent.ProjectileType<MokouPhoenixVortexProjectile>()) vortex.Kill();
        vortex = null;
        if (animator != null && !animator.IsDisposed)
        {
            animator.SetBoneRotationOffset("body_1", 0f);
            animator.SetBoneRotationOffset("head", 0f);
            animator.SetBoneRotationOffset("front_arm", 0f);
            animator.SetBoneRotationOffset("back_arm", 0f);
            animator.SetBoneRotationOffset("front_hand", 0f);
            animator.SetBoneRotationOffset("back_hand", 0f);
        }
        animator = null;
        ((Mokou)Owner).CompleteSkillLifeUse();
    }
}
