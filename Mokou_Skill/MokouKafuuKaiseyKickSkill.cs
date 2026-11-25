using System;
using CharacterRely.Animation;
using CharacterRely.Content.SkillSystem;
using Microsoft.Xna.Framework;
using Mokou_Character.Character;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Mokou_Character.Skill;

/// <summary>地面蓄力上冲旋踢后再次按键飞踢；空中起手直接使用富士火山，朝鼠标飞踢并在落地处喷出火柱。</summary>
public sealed class MokouKafuuKaiseyKickSkill : CharacterRely.Content.SkillSystem.Skill, IMokouActionSkill
{
    public const string SkillId = "kafuu_kaisey_kick";
    /// <summary>成功释放时立即扣除的生命，必须至少保留 1 点生命。</summary>
    public const int LifeCost = 50;

    private const int ChargeTicks = 22;
    private const int RiseTicks = 24;
    private const int SpinTicks = 12;
    /// <summary>旋踢后等待第二次按键的滞空时间，90 tick 为 1.5 秒。</summary>
    private const int FollowUpWindowTicks = 90;
    private const int DiveTicksLimit = 150;
    private const int LandingTicks = 12;
    private const int Duration = ChargeTicks + RiseTicks + SpinTicks + FollowUpWindowTicks + DiveTicksLimit
        + MokouKickExplosionProjectile.EmpoweredLifetime + LandingTicks;

    private MokouSkillDamageSnapshot damageSnapshot;
    private const float RiseStartSpeed = -15f;
    private const float RiseEndSpeed = -4.5f;
    private const float DiveSpeed = 18f;

    private static readonly SkillInfo InfoData = new(
        SkillId,
        "凯风快晴踢",
        "地面释放：蓄力上冲并旋踢一圈，再次按键朝鼠标位置飞踢，旋踢后可滞空等待1.5秒。空中释放：直接使用富士火山，朝当前鼠标位置飞踢。落地喷出约两人宽、五人高的火柱，燃烧2秒，两道上升火环表示燃烧进度；落地收势后可释放其他技能。燃烧期间持续命中火柱范围内的敌人，对同一敌人每0.5秒造成一次伤害，基础数值随世界BOSS进度成长，只受魔法与通用伤害加成（通用只计一次）。普通火柱燃尽消散；人体自燃强化时，燃尽后在顶部爆开，造成一次半径64像素的伤害，并向四周射出6枚穿墙飞弹，每枚造成火柱伤害的50%，整套伤害提高50%。成功释放时立即消耗50点生命，至少保留1点生命；火柱和飞弹全部结束后结算自燃进度与强化回血。",
        cooldownTicks: 300,
        durationTicks: Duration,
        maxCount: 1,
        cost: 0);

    private SpinePlayer animator;
    private int facingDirection;
    private float targetHeadAngle;
    private int diveTicks;
    private int landingTicks;
    private int spinProjectile = -1;
    private int diveProjectile = -1;
    private Projectile landingExplosion;
    private int landingExplosionIdentity;
    private bool isLanding, actionReleased;
    private bool previousNoFallDamage;
    // 第一段起手时锁定，旋踢、第二次按键的飞踢和落地火柱共用本次强化。
    private bool empoweredCast;
    private bool diveRequested, reachedDiveTarget;
    // 只在起手时判定，地面技能自行升空后仍继续原本的旋踢流程。
    private bool startedAirborne;
    private Vector2 diveTarget, diveDirection, diveVelocity;
    private float diveBodyRotation;
    /// <summary>落地收势后可恢复移动；火柱及其飞弹仍属于本次技能，全部结束后才结算强化。</summary>
    public bool IsPerformingAction => IsActive && !actionReleased;

    private bool ExplosionActive => landingExplosion != null && landingExplosion.active
        && landingExplosion.identity == landingExplosionIdentity && landingExplosion.owner == Player.whoAmI
        && landingExplosion.type == ModContent.ProjectileType<MokouKickExplosionProjectile>();

    public MokouKafuuKaiseyKickSkill() : base(InfoData) { }

    /// <summary>允许地面与空中起手；释放频率由生命消耗、充能和冷却控制。</summary>
    protected override bool CanUse()
        => !Main.dedServ && Player.whoAmI == Main.myPlayer
            && !Player.CCed && !Player.pulley && !Player.mount.Active
            && Player.itemAnimation == 0 && Player.heldProj < 0
            && Owner is Mokou character && character.CanSpendSkillLife(LifeCost)
            && !character.IsNirvana
            && !character.IsPhoenixChanting
            && !character.HasBlockingSkillAction(this);

    protected override bool OnUse()
        => ((Mokou)Owner).TryUseSkillWithLifeCost(this, LifeCost, BeginCast);

    private bool BeginCast(bool empowered)
    {
        var character = (Mokou)Owner;
        empoweredCast = empowered;
        damageSnapshot = MokouSkillBalance.Capture(Player, empowered);
        animator = character.Animator;
        if (animator == null || animator.IsDisposed) return false;
        startedAirborne = !IsGrounded();
        facingDirection = Main.MouseWorld.X < Player.Center.X ? -1 : 1;
        targetHeadAngle = 0f;
        Player.ChangeDir(facingDirection);
        animator.FlipX = facingDirection < 0;

        diveTicks = 0;
        landingTicks = 0;
        isLanding = actionReleased = false;
        diveRequested = reachedDiveTarget = false;
        diveTarget = diveDirection = diveVelocity = Vector2.Zero;
        diveBodyRotation = 0f;
        spinProjectile = -1;
        diveProjectile = -1;
        landingExplosion = null;
        previousNoFallDamage = Player.noFallDmg;
        Player.velocity = Vector2.Zero;
        Player.noFallDmg = true;
        SetBone("body_2", 0f);
        if (startedAirborne)
        {
            diveTarget = Main.MouseWorld;
            diveRequested = true;
            BeginDive();
            diveVelocity = diveDirection * DiveSpeed;
            Player.velocity = diveVelocity;
            ApplyDivePose(1f);
        }
        else
            ApplyChargePose(0f);
        _ = character.VanillaActions;
        return true;
    }

    protected override void UpdateActive()
    {
        var character = (Mokou)Owner;
        if (Player.CCed || Player.pulley || Player.mount.Active || Player.dead
            || character.IsNirvana || animator == null || animator.IsDisposed)
        {
            End(interrupted: true);
            return;
        }

        int tick = ElapsedTicks;
        if (isLanding)
        {
            landingTicks++;
            if (landingTicks <= LandingTicks)
            {
                Player.velocity = Vector2.Zero;
                Player.noFallDmg = true;
                ApplyLandingPose(landingTicks / (float)LandingTicks);
                if (landingTicks == LandingTicks) ReleaseAction();
            }
            // 不重复写速度和骨骼，允许自由移动；等本次火柱和飞弹全部结束再结算。
            if (landingTicks >= LandingTicks && !ExplosionActive) End();
            return;
        }

        Player.noFallDmg = true;
        ReadFollowUpInput();
        Player.ChangeDir(facingDirection);
        animator.FlipX = facingDirection < 0;

        if (startedAirborne)
        {
            UpdateDive();
            return;
        }

        if (tick <= ChargeTicks)
        {
            Player.velocity = new Vector2(0f, Player.velocity.Y);
            ApplyChargePose(tick / (float)ChargeTicks);
            return;
        }

        int riseTick = tick - ChargeTicks;
        if (riseTick <= RiseTicks)
        {
            float progress = Ease((riseTick - 1) / (float)(RiseTicks - 1));
            Player.velocity = new Vector2(0f, MathHelper.Lerp(RiseStartSpeed, RiseEndSpeed, progress));
            ApplyRisePose(progress);
            return;
        }

        int spinTick = riseTick - RiseTicks;
        if (spinTick <= SpinTicks)
        {
            if (spinTick == 1)
                spinProjectile = SpawnHitbox(0f, damageSnapshot.Base.Spin, SpinTicks);

            float progress = (spinTick - 1) / (float)(SpinTicks - 1);
            Player.velocity = Vector2.Zero;
            ApplySpinPose(progress);
            return;
        }

        if (!diveRequested)
        {
            Player.velocity = Vector2.Zero;
            ApplySpinPose(1f);
            if (spinTick - SpinTicks >= FollowUpWindowTicks)
                End();
            return;
        }

        UpdateDive();
    }

    /// <summary>地面技能的第二段与空中直接释放共用飞踢、命中和落地逻辑。</summary>
    private void UpdateDive()
    {
        if (diveTicks == 0)
            BeginDive();
        else if (HasLanded())
        {
            StartLanding();
            return;
        }

        // 朝起手或第二次按键锁定的位置直线飞踢；抵达目标或撞墙/撞顶后自然下落。
        // 水平飞踢的 Y 速度为零，不代表已经接触地面。
        if (diveTicks > 0 && !reachedDiveTarget)
        {
            reachedDiveTarget = Vector2.Dot(diveTarget - Player.Center, diveDirection) <= DiveSpeed
                || (Math.Abs(diveVelocity.X) > 0.1f && Player.velocity.X == 0f)
                || (diveVelocity.Y < -0.1f && Player.velocity.Y == 0f);
        }

        diveTicks++;
        if (diveTicks == 1)
            diveProjectile = SpawnHitbox(1f, damageSnapshot.Base.Dive, DiveTicksLimit);

        float diveProgress = MathHelper.Clamp(diveTicks / 36f, 0f, 1f);
        diveVelocity = reachedDiveTarget
            ? new Vector2(Player.velocity.X * 0.94f, Math.Min(15f, Math.Max(0f, Player.velocity.Y) + 0.55f))
            : diveDirection * DiveSpeed;
        Player.velocity = diveVelocity;
        ApplyDivePose(startedAirborne ? 1f : diveProgress);

        // 超时只是中断俯冲；只有前面的实际落地分支允许爆炸。
        if (diveTicks >= DiveTicksLimit)
            End(interrupted: true);
    }

    /// <summary>按当前装配槽读取新的按下事件；不写死 C 键，也不再次调用扣费入口。</summary>
    private void ReadFollowUpInput()
    {
        if (ElapsedTicks <= 1 || diveRequested || isLanding || Main.dedServ
            || Player.whoAmI != Main.myPlayer || Main.gameMenu || Main.gamePaused
            || Main.blockInput || Main.drawingPlayerChat || Main.editSign || Main.editChest
            || Main.playerInventory || Player.mouseInterface) return;

        for (int slot = 0; slot < SkillKeybindSystem.SlotKeys.Count; slot++)
        {
            if (ReferenceEquals(Owner.GetEquippedSkill(slot), this)
                && SkillKeybindSystem.SlotKeys[slot].JustPressed)
            {
                QueueDive(Main.MouseWorld);
                return;
            }
        }
    }

    /// <summary>只记录一次第二段指令；提前按下也会等第一段完整旋转结束。</summary>
    private void QueueDive(Vector2 target)
    {
        if (!IsActive || diveRequested || isLanding) return;
        diveTarget = target;
        diveRequested = true;
    }

    private void BeginDive()
    {
        Vector2 offset = diveTarget - Player.Center;
        diveDirection = offset.SafeNormalize(new Vector2(facingDirection, 1f).SafeNormalize(Vector2.UnitY));
        if (Math.Abs(offset.X) > 1f) facingDirection = offset.X < 0f ? -1 : 1;
        Player.ChangeDir(facingDirection);
        animator.FlipX = facingDirection < 0;
        targetHeadAngle = MathHelper.Clamp(MathF.Atan2(diveDirection.Y, Math.Max(0.01f, Math.Abs(diveDirection.X))), -0.9f, 0.9f);
    }

    /// <summary>检查实际地面支撑；空中悬停、跳跃最高点的零速度不算落地。</summary>
    private bool IsGrounded()
    {
        if (Player.velocity.Y * Player.gravDir < 0f) return false;
        if (Player.sloping) return true;
        if (Player.velocity.Y != 0f) return false;
        Vector2 probe = Collision.TileCollision(Player.position, new Vector2(0f, 2f * Player.gravDir),
            Player.width, Player.height, fallThrough: false, fall2: false, gravDir: (int)Player.gravDir);
        return Math.Abs(probe.Y) < 2f;
    }

    private bool HasLanded() => diveVelocity.Y * Player.gravDir >= 0f && IsGrounded();

    private void StartLanding()
    {
        if (isLanding) return;
        isLanding = true;
        landingTicks = 0;
        Player.velocity = Vector2.Zero;
        KillHitbox(ref diveProjectile);
        if (Player.whoAmI == Main.myPlayer)
        {
            int index = Projectile.NewProjectile(
                Player.GetSource_FromThis("CharacterSkill:" + Id),
                Player.Bottom,
                Vector2.Zero,
                ModContent.ProjectileType<MokouKickExplosionProjectile>(),
                damageSnapshot.Resolve(damageSnapshot.Base.Pillar),
                7f,
                Player.whoAmI, 0f, empoweredCast ? 1f : 0f);
            landingExplosion = (uint)index < Main.maxProjectiles ? Main.projectile[index] : null;
            landingExplosionIdentity = landingExplosion?.identity ?? -1;
        }
    }

    private int SpawnHitbox(float phase, int damage, int lifetime)
    {
        if (Player.whoAmI != Main.myPlayer) return -1;

        return Projectile.NewProjectile(
            Player.GetSource_FromThis("CharacterSkill:" + Id),
            Player.Center,
            phase == 1f ? diveDirection * DiveSpeed : Vector2.Zero,
            ModContent.ProjectileType<MokouKafuuKaiseyKickProjectile>(),
            damageSnapshot.Resolve(damage),
            5f,
            Player.whoAmI,
            phase,
            facingDirection,
            lifetime);
    }

    private void ApplyChargePose(float progress)
    {
        float t = Ease(progress);
        SetBone("body_1", 0.16f * t);
        SetBone("head", -0.58f * t);
        SetBone("front_arm", -0.48f * t);
        SetBone("back_arm", 0.28f * t);
        SetBone("front_leg_1", 0.52f * t);
        SetBone("front_leg_2", -0.76f * t);
        SetBone("back_leg_1", -0.3f * t);
        SetBone("back_leg_2", 0.34f * t);
    }

    private void ApplyRisePose(float progress)
    {
        SetBone("body_1", MathHelper.Lerp(0f, -0.18f, progress));
        SetBone("head", MathHelper.Lerp(-0.58f, -0.12f, progress));
        SetBone("front_arm", MathHelper.Lerp(-0.48f, -0.72f, progress));
        SetBone("back_arm", MathHelper.Lerp(0.28f, 0.78f, progress));
        SetBone("front_leg_1", MathHelper.Lerp(0.52f, -1.95f, progress));
        SetBone("front_leg_2", MathHelper.Lerp(-0.76f, 0.68f, progress));
        SetBone("back_leg_1", MathHelper.Lerp(-0.3f, 0.62f, progress));
        SetBone("back_leg_2", MathHelper.Lerp(0.34f, -0.52f, progress));
    }

    private void ApplySpinPose(float progress)
    {
        float spin = progress * MathHelper.TwoPi;
        // body_2 是躯干和双腿共同的父骨骼，绕腰部转动整个人。
        // 镜像由 SpinePlayer 处理，左右朝向都走完整的一圈。
        SetBone("body_2", spin);
        SetBone("body_1", -0.1f);
        SetBone("head", 0f);
        SetBone("front_arm", 0.6f);
        SetBone("back_arm", -0.55f);
        SetBone("front_leg_1", -1.45f);
        SetBone("front_leg_2", 0.04f);
        SetBone("back_leg_1", 0.55f);
        SetBone("back_leg_2", -1.05f);
    }

    private void ApplyDivePose(float progress)
    {
        float transition = Ease(MathHelper.Clamp(progress * 6f, 0f, 1f));
        SetBone("body_2", 0f);
        SetBone("body_1", MathHelper.Lerp(-0.1f, 0.32f, transition));
        SetBone("head", targetHeadAngle * transition);
        SetBone("front_arm", MathHelper.Lerp(0.6f, -0.28f, transition));
        SetBone("back_arm", MathHelper.Lerp(-0.55f, 0.72f, transition));
        SetBone("front_leg_1", MathHelper.Lerp(-1.45f, -1.08f, transition));
        SetBone("front_leg_2", MathHelper.Lerp(0.04f, 0.06f, transition));
        SetBone("back_leg_1", MathHelper.Lerp(0.55f, 0.86f, transition));
        SetBone("back_leg_2", MathHelper.Lerp(-1.05f, -0.7f, transition));

        // 将伸出的脚对准本次飞踢方向，水平或向上瞄准时也不会仍摆出固定的向下姿势。
        animator.TryGetBoneTransform("body_2", out Matrix hip);
        animator.TryGetBoneTransform("front_foot", out Matrix foot);
        Vector2 leg = new(foot.M41 - hip.M41, foot.M42 - hip.M42);
        diveBodyRotation = MathHelper.WrapAngle(diveDirection.ToRotation() - leg.ToRotation()) * facingDirection * transition;
        SetBone("body_2", diveBodyRotation);
        SetBone("head", MathHelper.Clamp(targetHeadAngle - diveBodyRotation, -0.9f, 0.9f) * transition);
    }

    private void ApplyLandingPose(float progress)
    {
        float recover = Ease(progress);
        SetBone("body_2", MathHelper.Lerp(diveBodyRotation, 0f, recover));
        SetBone("body_1", MathHelper.Lerp(0.32f, 0f, recover));
        SetBone("head", MathHelper.Lerp(targetHeadAngle, 0f, recover));
        SetBone("front_arm", MathHelper.Lerp(-0.28f, 0f, recover));
        SetBone("back_arm", MathHelper.Lerp(0.72f, 0f, recover));
        SetBone("front_leg_1", MathHelper.Lerp(0.74f, 0f, recover));
        SetBone("front_leg_2", MathHelper.Lerp(-0.92f, 0f, recover));
        SetBone("back_leg_1", MathHelper.Lerp(-0.28f, 0f, recover));
        SetBone("back_leg_2", MathHelper.Lerp(0.36f, 0f, recover));
    }

    private void SetBone(string name, float radians)
        => animator.SetBoneRotationOffset(name, radians);

    private static float Ease(float value)
    {
        value = MathHelper.Clamp(value, 0f, 1f);
        return value * value * (3f - 2f * value);
    }

    private void KillHitbox(ref int index)
    {
        if ((uint)index < (uint)Main.maxProjectiles)
        {
            Projectile projectile = Main.projectile[index];
            if (projectile.active && projectile.owner == Player.whoAmI
                && projectile.type == ModContent.ProjectileType<MokouKafuuKaiseyKickProjectile>())
                projectile.Kill();
        }
        index = -1;
    }

    /// <summary>落地收势结束就释放骨骼和免摔状态；火柱最终结束不再碰新技能的动作。</summary>
    private void ReleaseAction()
    {
        if (actionReleased) return;
        actionReleased = true;
        Player.noFallDmg = previousNoFallDamage;

        if (animator != null && !animator.IsDisposed)
        {
            SetBone("body_2", 0f);
            SetBone("body_1", 0f);
            SetBone("head", 0f);
            SetBone("front_arm", 0f);
            SetBone("back_arm", 0f);
            SetBone("front_leg_1", 0f);
            SetBone("front_leg_2", 0f);
            SetBone("back_leg_1", 0f);
            SetBone("back_leg_2", 0f);
        }
    }

    protected override void OnEnd(bool interrupted)
    {
        if (interrupted && ExplosionActive) landingExplosion.Kill();
        landingExplosion = null;
        empoweredCast = false;
        KillHitbox(ref spinProjectile);
        KillHitbox(ref diveProjectile);
        diveRequested = reachedDiveTarget = startedAirborne = false;
        ReleaseAction();
        animator = null;
        ((Mokou)Owner).CompleteSkillLifeUse(this);
    }
}
