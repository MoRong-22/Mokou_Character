using System;
using CharacterRely.Animation;
using CharacterRely.Content.SkillSystem;
using Microsoft.Xna.Framework;
using Mokou_Character.Character;
using Mokou_Character.SkillVisuals;
using Terraria;
using Terraria.ModLoader;

namespace Mokou_Character.Skill;

/// <summary>限时逐次按键冲刺，每段起手扣血；次数用完或窗口结束进入冷却，不再触发舍身死亡。</summary>
public sealed class MokouSacrificeStrikeSkill : CharacterRely.Content.SkillSystem.Skill
{
    public const string SkillId = "immortal_sacrifice_strike";
    public const int NormalDashCount = 3, EmpoweredDashCount = 5;
    /// <summary>每次成功冲刺消耗 50 生命；连放窗口从第一次起手计 3 秒，整套冷却 30 秒。</summary>
    public const int LifeCost = 50, RecastWindowTicks = 3 * 60, CooldownTicks = 30 * 60;
    public const float DashSpeed = 36f, MaxDashDistance = 480f;
    public const int BaseDamage = 48, MaxDashTicks = 18;
    public const int WindupTicks = 5, RecoveryTicks = 7;
    private enum DashPhase { Ready, Windup, Travel, Recovery }
    private static readonly string[] PoseBones = {
        "body_2", "body_1", "head", "front_arm", "front_hand", "back_arm", "back_hand",
        "front_leg_1", "front_leg_2", "back_leg_1", "back_leg_2"
    };
    private SpinePlayer animator;
    private Projectile hitbox;
    private int hitboxIdentity;
    private Vector2 dashDirection, stepStart;
    private float remainingDistance;
    private float dashBodyRotation, dashArmRotation;
    private int phaseTicks, facing;
    private DashPhase phase;
    private bool stepPrepared, hitWall;

    public int RemainingDashes { get; private set; }
    public bool EmpoweredCast { get; private set; }
    /// <summary>包括短起手和收势，用于占用动作与移动；等待下一次按键时为 false。</summary>
    public bool IsDashing => IsActive && phase != DashPhase.Ready;
    internal bool IsTravelling => IsActive && phase == DashPhase.Travel;
    protected override bool CooldownStartsOnUse => false;

    public MokouSacrificeStrikeSkill() : base(new SkillInfo(SkillId, "不死身的舍身击",
        "每次成功释放立即消耗50点生命，至少保留1点生命。按技能键收身蓄势后朝鼠标位置冲刺，最远480像素，碰撞敌人造成48点基础伤害并留下火焰轨迹。" +
        "从第一次释放起3秒内可再次按键，普通最多3次；人体自燃强化时最多5次，整套伤害提高50%，后续按键不延长窗口。" +
        "次数用完或窗口超时进入30秒冷却，中断同样进入冷却；两次冲刺间可正常移动，不再自动死亡。整套结束后统一结算自燃与强化回血。",
        cooldownTicks: CooldownTicks, durationTicks: RecastWindowTicks + WindupTicks + MaxDashTicks + RecoveryTicks,
        maxCount: 1, cost: 0)) { }

    protected override bool CanUse() => !Main.dedServ && Player.whoAmI == Main.myPlayer && !Player.CCed
        && !Player.pulley && !Player.mount.Active && Player.grapCount == 0
        && Player.itemAnimation == 0 && Player.heldProj < 0
        && Owner is Mokou c && c.CanSpendSkillLife(LifeCost) && !c.IsNirvana
        && !c.ClawSkill.IsActive && !c.KafuuKaiseyKickSkill.IsActive && !c.IsPhoenixChanting;

    protected override bool OnUse()
        => ((Mokou)Owner).TryUseSkillWithLifeCost(LifeCost, BeginCast);

    private bool BeginCast(bool empowered)
    {
        var character = (Mokou)Owner;
        animator = character.Animator;
        if (animator == null || animator.IsDisposed) return false;
        _ = character.VanillaActions;
        EmpoweredCast = empowered;
        RemainingDashes = empowered ? EmpoweredDashCount : NormalDashCount;
        BeginDash(Main.MouseWorld);
        return true;
    }

    /// <summary>目标为世界坐标；窗口内每次成功起手扣 50 血，冲刺未收势或生命不足时不扣血、不扣次数。</summary>
    public bool TryDash(Vector2 worldTarget)
    {
        if (!IsActive || phase != DashPhase.Ready || RemainingDashes <= 0
            || ElapsedTicks >= RecastWindowTicks || !CanUse()) return false;
        if (!((Mokou)Owner).TryContinueSkillWithLifeCost(LifeCost, () =>
            {
                BeginDash(worldTarget);
                return true;
            })) return false;
        if (RemainingDashes == 0) StartCooldown();
        return true;
    }

    private void BeginDash(Vector2 target)
    {
        Vector2 offset = target - Player.Center;
        dashDirection = offset.SafeNormalize(new Vector2(Player.direction, 0));
        remainingDistance = Math.Clamp(offset.Length(), 1, MaxDashDistance);
        facing = Math.Abs(dashDirection.X) > 0.01f ? Math.Sign(dashDirection.X) : Player.direction;
        RemainingDashes--;
        phase = DashPhase.Windup;
        phaseTicks = 0;
        stepPrepared = hitWall = false;
        Player.velocity = Vector2.Zero;
        ApplyPose();
    }

    private void Launch()
    {
        phase = DashPhase.Travel;
        phaseTicks = 0;
        int index = Projectile.NewProjectile(Player.GetSource_Misc("CharacterSkill:" + Id),
            Player.Center, dashDirection, ModContent.ProjectileType<MokouSacrificeDashProjectile>(),
            Mokou.GetEmpoweredSkillDamage(BaseDamage, EmpoweredCast), 5, Player.whoAmI);
        hitbox = (uint)index < Main.maxProjectiles ? Main.projectile[index] : null;
        hitboxIdentity = hitbox?.identity ?? -1;
    }

    /// <summary>原版移动前设置速度，保留物块碰撞；不直接改位置穿墙。</summary>
    internal void ApplyDashMovement()
    {
        if (!IsDashing || Main.dedServ || Player.whoAmI != Main.myPlayer) return;
        if (Player.CCed || Player.pulley || Player.mount.Active || Player.grapCount > 0)
        { End(interrupted: true); return; }
        Player.noFallDmg = true;
        Player.fallStart = (int)(Player.position.Y / 16);
        if (phase != DashPhase.Travel) { Player.velocity = Vector2.Zero; return; }
        stepStart = Player.Center;
        float speed = MokouSkillMotion.DashSpeedAt(phaseTicks, MaxDashTicks, DashSpeed);
        Vector2 requested = dashDirection * Math.Min(speed, remainingDistance);
        Vector2 allowed = Collision.TileCollision(Player.position, requested, Player.width, Player.height,
            fallThrough: true, fall2: true, gravDir: (int)Player.gravDir);
        hitWall = Vector2.DistanceSquared(allowed, requested) > 0.01f;
        Player.velocity = allowed;
        stepPrepared = true;
    }

    protected override void UpdateActive()
    {
        if (Player.dead || !Owner.IsSelected || ((Mokou)Owner).IsNirvana || Player.CCed
            || Player.pulley || Player.mount.Active || Player.grapCount > 0 || animator == null || animator.IsDisposed)
        { End(interrupted: true); return; }
        if (ElapsedTicks >= RecastWindowTicks)
        {
            // 窗口不随连按刷新；已起手的最后一段允许完成动作，之后不再接受下一段。
            StartCooldown();
            if (phase == DashPhase.Ready) { End(); return; }
        }
        if (phase != DashPhase.Ready)
        {
            if (phase == DashPhase.Windup && ++phaseTicks >= WindupTicks) Launch();
            else if (phase == DashPhase.Travel && stepPrepared)
            {
                remainingDistance -= Vector2.Distance(stepStart, Player.Center);
                stepPrepared = false;
                if (++phaseTicks >= MaxDashTicks || hitWall || remainingDistance <= 0.5f) BeginRecovery();
            }
            else if (phase == DashPhase.Recovery && ++phaseTicks >= RecoveryTicks)
            {
                ResetPose();
                phase = DashPhase.Ready;
                if (RemainingDashes == 0 || ElapsedTicks >= RecastWindowTicks) End();
                return;
            }
            ApplyPose();
            return;
        }
        ReadNextDash();
    }

    private void ReadNextDash()
    {
        if (ElapsedTicks <= 1 || Main.dedServ || Player.whoAmI != Main.myPlayer
            || Main.gameMenu || Main.gamePaused || Main.blockInput || Main.drawingPlayerChat
            || Main.editSign || Main.editChest || Main.playerInventory || Player.mouseInterface) return;
        for (int slot = 0; slot < SkillKeybindSystem.SlotKeys.Count; slot++)
            if (ReferenceEquals(Owner.GetEquippedSkill(slot), this) && SkillKeybindSystem.SlotKeys[slot].JustPressed)
            { TryDash(Main.MouseWorld); break; }
    }

    private void ApplyPose()
    {
        Player.ChangeDir(facing);
        animator.FlipX = facing < 0;
        float windup = MokouSkillMotion.Smooth(phaseTicks / (float)WindupTicks);
        float recover = phase == DashPhase.Recovery ? 1 - MokouSkillMotion.Smooth(phaseTicks / (float)RecoveryTicks) : 1;
        if (phase == DashPhase.Windup)
        {
            // 屈膝收身，双臂回收；起手仅5帧，不让连续空中冲刺显得迟钝。
            Pose("body_2", 0.18f * windup); Pose("body_1", -0.32f * windup);
            Pose("head", 0.12f * windup);
            Pose("front_arm", -0.55f * windup); Pose("front_hand", -1.1f * windup);
            Pose("back_arm", 0.5f * windup); Pose("back_hand", -0.7f * windup);
            Pose("front_leg_1", -0.55f * windup); Pose("front_leg_2", 0.95f * windup);
            Pose("back_leg_1", 0.4f * windup); Pose("back_leg_2", 0.75f * windup);
            return;
        }
        // 前手向前破风，后手拖后；一腿收膝、一腿伸展，避免整个角色僵硬平移。
        Pose("body_2", 0); Pose("body_1", 0.08f * recover); Pose("head", 0);
        Pose("front_arm", (phase == DashPhase.Recovery ? dashArmRotation : -1.65f) * recover);
        Pose("front_hand", -0.15f * recover);
        Pose("back_arm", 0.35f * recover); Pose("back_hand", -0.7f * recover);
        Pose("front_leg_1", -0.7f * recover); Pose("front_leg_2", 1.15f * recover);
        Pose("back_leg_1", 0.18f * recover); Pose("back_leg_2", 0.12f * recover);
        animator.TryGetBoneTransform("body_2", out Matrix hip);
        animator.TryGetBoneTransform("head", out Matrix head);
        Vector2 torso = new(head.M41 - hip.M41, head.M42 - hip.M42);
        // 真实躯干向量朝冲刺方向对齐；左镜像和垂直冲刺共用同一计算。
        float rotation = MathHelper.WrapAngle(dashDirection.ToRotation() - torso.ToRotation()) * facing;
        if (phase == DashPhase.Travel) dashBodyRotation = rotation;
        Pose("body_2", dashBodyRotation * recover);
        if (phase == DashPhase.Travel)
            dashArmRotation = MokouSkillMotion.AimBone(animator, "front_arm", dashDirection, facing, -1.65f);
    }

    private void Pose(string name, float radians) => animator.SetBoneRotationOffset(name, radians);
    private void ResetPose()
    {
        if (animator != null && !animator.IsDisposed)
            foreach (string bone in PoseBones) Pose(bone, 0);
    }

    private void FinishHitbox()
    {
        if (hitbox != null && hitbox.active && hitbox.identity == hitboxIdentity && hitbox.owner == Player.whoAmI
            && hitbox.ModProjectile is MokouSacrificeDashProjectile dash)
            dash.Finish(Player.Center);
        hitbox = null;
    }

    private void BeginRecovery()
    {
        FinishHitbox();
        Player.velocity = Vector2.Zero;
        phase = DashPhase.Recovery;
        phaseTicks = 0;
        stepPrepared = false;
    }

    protected override void OnEnd(bool interrupted)
    {
        FinishHitbox();
        if (phase != DashPhase.Ready) Player.velocity = Vector2.Zero;
        ResetPose();
        phase = DashPhase.Ready;
        stepPrepared = false;
        animator = null;
        RemainingDashes = 0;
        EmpoweredCast = false;
        ((Mokou)Owner).CompleteSkillLifeUse();
    }
}
