using System;
using CharacterRely.Animation;
using CharacterRely.Content.SkillSystem;
using Microsoft.Xna.Framework;
using Mokou_Character.Character;
using Mokou_Character.SkillVisuals;
using Terraria;
using Terraria.ModLoader;

namespace Mokou_Character.Skill;

/// <summary>每段原地蓄力一秒，期间火弹逐枚显现并自行飞出，随后玩家冲刺；蓄力暂停连放窗口。</summary>
public sealed class MokouSacrificeStrikeSkill : CharacterRely.Content.SkillSystem.Skill, IMokouActionSkill
{
    public const string SkillId = "immortal_sacrifice_strike";
    public const int NormalDashCount = 3, EmpoweredDashCount = 5;
    /// <summary>每次起手扣 50 生命；3 秒连放窗口不含蓄力，整套冷却 30 秒。</summary>
    public const int LifeCost = 50, RecastWindowTicks = SacrificeCastTiming.WindowTicks, CooldownTicks = 30 * 60;
    public const float DashSpeed = 36f, MaxDashDistance = 1200f;
    // 距离延长2.5倍，同时放宽动作时限，保证不会仍在原来的18帧处被截断。
    public const int MaxDashTicks = 45;
    private MokouSkillDamageSnapshot damageSnapshot;
    public const int WindupTicks = SacrificeCastTiming.ChargeTicks, RecoveryTicks = 7;
    public const int MinFlames = 8, MaxFlames = 10;
    private enum DashPhase { Ready, Windup, Travel, Recovery, Finishing }
    private static readonly string[] PoseBones = {
        "body_2", "body_1", "head", "front_arm", "front_hand", "back_arm", "back_hand",
        "front_leg_1", "front_leg_2", "back_leg_1", "back_leg_2"
    };
    private SpinePlayer animator;
    private Projectile hitbox;
    private Projectile chargeEffect;
    // 只用于本段随机出生点避让，火弹的发射由各自出生计时决定。
    private readonly Projectile[] chargingFlames = new Projectile[MaxFlames];
    private readonly int[] chargingFlameIdentities = new int[MaxFlames];
    private int plannedFlames, spawnedFlames;
    private int chargeIdentity, castSerial;
    private readonly SacrificeCastTiming recastClock = new();
    private int hitboxIdentity;
    private Vector2 dashDirection, stepStart, chargePosition;
    private float remainingDistance;
    private float dashBodyRotation, dashArmRotation;
    private int phaseTicks, facing;
    private DashPhase phase;
    private bool stepPrepared, hitWall, ownsPose;

    public int RemainingDashes { get; private set; }
    public bool EmpoweredCast { get; private set; }
    /// <summary>蓄力、冲刺及收势占用动作；等待连放或火弹消失时允许正常移动。</summary>
    public bool IsDashing => IsActive && phase is DashPhase.Windup or DashPhase.Travel or DashPhase.Recovery;
    public bool IsPerformingAction => IsDashing;
    internal bool IsTravelling => IsActive && phase == DashPhase.Travel;
    internal bool IsCharging => IsActive && phase == DashPhase.Windup;
    internal int CastSerial => castSerial;
    protected override bool CooldownStartsOnUse => false;

    public MokouSacrificeStrikeSkill() : base(new SkillInfo(SkillId, "不死身的舍身击",
        "每次成功释放立即消耗50点生命，至少保留1点生命。原地蓄力1秒，随后朝按键时的鼠标方向冲刺，最远1200像素，身体碰撞造成伤害。冲刺与火弹伤害随世界BOSS进度成长，只受魔法与通用伤害加成（通用只计一次）。" +
        "蓄力期间，8～10枚独立火弹在身体周围随机位置逐枚缓入，每枚显现并停留约0.3秒后沿本次冲刺方向自行飞出，射程等于本次瞄准的冲刺距离，初始基础伤害5，随进度提升，命中附加3秒着火。" +
        "3秒连放窗口内可再次按键，每次蓄力均暂停窗口计时；普通最多3次，人体自燃强化时最多5次，整套伤害提高50%。" +
        "次数用完或窗口超时进入30秒冷却，中断同样进入冷却；两次冲刺间可正常移动。所有冲刺和火弹结束后统一结算自燃与强化回血。",
        cooldownTicks: CooldownTicks, durationTicks: RecastWindowTicks + WindupTicks * EmpoweredDashCount
            + MaxDashTicks + RecoveryTicks + MokouSacrificeFlameProjectile.FlightTicks + 2,
        maxCount: 1, cost: 0)) { }

    protected override bool CanUse() => !Main.dedServ && Player.whoAmI == Main.myPlayer && !Player.CCed
        && !Player.pulley && !Player.mount.Active && Player.grapCount == 0
        && Player.itemAnimation == 0 && Player.heldProj < 0
        && Owner is Mokou c && c.CanSpendSkillLife(LifeCost) && !c.IsNirvana
        && !c.HasBlockingSkillAction(this);

    protected override bool OnUse()
        => ((Mokou)Owner).TryUseSkillWithLifeCost(this, LifeCost, BeginCast);

    private bool BeginCast(bool empowered)
    {
        var character = (Mokou)Owner;
        animator = character.Animator;
        if (animator == null || animator.IsDisposed) return false;
        _ = character.VanillaActions;
        EmpoweredCast = empowered;
        damageSnapshot = MokouSkillBalance.Capture(Player, empowered);
        RemainingDashes = empowered ? EmpoweredDashCount : NormalDashCount;
        recastClock.Reset();
        castSerial = castSerial % 16000000 + 1;
        BeginDash(Main.MouseWorld);
        return true;
    }

    /// <summary>目标为世界坐标；蓄力暂停窗口，起手失败不扣血、不扣次数。</summary>
    public bool TryDash(Vector2 worldTarget)
    {
        if (!IsActive || phase != DashPhase.Ready || RemainingDashes <= 0
            || recastClock.Expired || !CanUse()) return false;
        if (!((Mokou)Owner).TryContinueSkillWithLifeCost(this, LifeCost, () =>
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
        plannedFlames = Main.rand.Next(MinFlames, MaxFlames + 1);
        spawnedFlames = 0;
        Array.Clear(chargingFlames);
        stepPrepared = hitWall = false;
        chargePosition = Player.position;
        Player.velocity = Vector2.Zero;
        int index = Projectile.NewProjectile(Player.GetSource_FromThis("CharacterSkill:" + Id),
            Player.Center, dashDirection, ModContent.ProjectileType<MokouSacrificeChargeProjectile>(),
            0, 0, Player.whoAmI, ai2: castSerial);
        chargeEffect = (uint)index < Main.maxProjectiles ? Main.projectile[index] : null;
        chargeIdentity = chargeEffect?.identity ?? -1;
        ApplyPose();
    }

    private void Launch()
    {
        FinishCharge();
        phase = DashPhase.Travel;
        phaseTicks = 0;
        int index = Projectile.NewProjectile(Player.GetSource_FromThis("CharacterSkill:" + Id),
            Player.Center, dashDirection, ModContent.ProjectileType<MokouSacrificeDashProjectile>(),
            damageSnapshot.Resolve(damageSnapshot.Base.SacrificeDash), 5, Player.whoAmI, ai2: castSerial);
        hitbox = (uint)index < Main.maxProjectiles ? Main.projectile[index] : null;
        hitboxIdentity = hitbox?.identity ?? -1;
        Array.Clear(chargingFlames);
    }

    private void SpawnChargingFlames()
    {
        if (Player.whoAmI != Main.myPlayer) return;
        // 第4帧开始逐枚生成，最后一枚也会在玩家冲刺前完成显现与发射。
        int lastSpawnTick = WindupTicks - MokouSacrificeFlameProjectile.LaunchDelayTicks - 2;
        while (spawnedFlames < plannedFlames
            && phaseTicks >= 4 + spawnedFlames * (lastSpawnTick - 4) / (plannedFlames - 1))
        {
            Vector2 position = FindFlamePosition();
            int index = Projectile.NewProjectile(Player.GetSource_FromThis("CharacterSkill:" + Id), position,
                dashDirection,
                ModContent.ProjectileType<MokouSacrificeFlameProjectile>(),
                damageSnapshot.Resolve(damageSnapshot.Base.SacrificeFlame), 1f, Player.whoAmI,
                ai1: Main.rand.NextFloat(), ai2: castSerial);
            Projectile flame = (uint)index < Main.maxProjectiles ? Main.projectile[index] : null;
            if (flame?.ModProjectile is MokouSacrificeFlameProjectile bolt)
            {
                bolt.SetRange(remainingDistance);
                flame.netUpdate = true;
            }
            chargingFlames[spawnedFlames] = flame;
            chargingFlameIdentities[spawnedFlames] = flame?.identity ?? -1;
            spawnedFlames++;
        }
    }

    private Vector2 FindFlamePosition()
    {
        Vector2 best = Player.Center;
        float bestSpacing = -1f;
        for (int attempt = 0; attempt < 40; attempt++)
        {
            Vector2 offset = Main.rand.NextVector2Circular(52f, 44f);
            if (offset.LengthSquared() < 22f * 22f) continue;
            Vector2 candidate = Player.Center + offset;
            if (Collision.SolidCollision(candidate - new Vector2(6f), 12, 12)) continue;
            float spacing = float.MaxValue;
            for (int i = 0; i < spawnedFlames; i++)
                if (chargingFlames[i] is { active: true } previous
                    && previous.identity == chargingFlameIdentities[i] && previous.ai[0] == 0f)
                    spacing = Math.Min(spacing, Vector2.DistanceSquared(candidate, previous.Center));
            // 随机采样，只排除贴得太近和埋在物块里的点，不采用等距圆环队形。
            if (spacing >= 18f * 18f) return candidate;
            if (spacing > bestSpacing) { best = candidate; bestSpacing = spacing; }
        }
        return best;
    }

    /// <summary>原版移动前设置速度，保留物块碰撞；不直接改位置穿墙。</summary>
    internal void ApplyDashMovement()
    {
        if (!IsDashing || Main.dedServ || Player.whoAmI != Main.myPlayer) return;
        if (Player.CCed || Player.pulley || Player.mount.Active || Player.grapCount > 0)
        { End(interrupted: true); return; }
        Player.noFallDmg = true;
        Player.fallStart = (int)(Player.position.Y / 16);
        if (phase != DashPhase.Travel)
        {
            if (phase == DashPhase.Windup) Player.position = chargePosition;
            Player.velocity = Vector2.Zero;
            return;
        }
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
        if (phase == DashPhase.Finishing)
        {
            if (!HasLiveEffects()) End();
            return;
        }
        recastClock.Update(phase == DashPhase.Windup);
        if (recastClock.Expired)
        {
            // 暂停后的窗口耗尽，已起手的最后一段仍允许完整结束。
            StartCooldown();
            if (phase == DashPhase.Ready) { FinishSequence(); return; }
        }
        if (phase != DashPhase.Ready)
        {
            if (phase == DashPhase.Windup)
            {
                phaseTicks++;
                SpawnChargingFlames();
                if (phaseTicks >= WindupTicks) Launch();
            }
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
                if (RemainingDashes == 0 || recastClock.Expired) FinishSequence();
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
        ownsPose = true;
        Player.ChangeDir(facing);
        animator.FlipX = facing < 0;
        float windup = MokouSkillMotion.Smooth(phaseTicks / (float)WindupTicks);
        float recover = phase == DashPhase.Recovery ? 1 - MokouSkillMotion.Smooth(phaseTicks / (float)RecoveryTicks) : 1;
        if (phase == DashPhase.Windup)
        {
            // 一秒内逐步压低重心，双臂收向躯干，头部保持朝冲刺方向。
            Pose("body_2", 0.14f * windup); Pose("body_1", -0.26f * windup);
            Pose("head", MathHelper.Clamp(MathF.Atan2(dashDirection.Y, Math.Abs(dashDirection.X)), -0.65f, 0.65f) * windup);
            Pose("front_arm", -0.82f * windup); Pose("front_hand", -0.9f * windup);
            Pose("back_arm", 0.72f * windup); Pose("back_hand", -0.8f * windup);
            Pose("front_leg_1", -0.62f * windup); Pose("front_leg_2", 1.03f * windup);
            Pose("back_leg_1", 0.38f * windup); Pose("back_leg_2", 0.80f * windup);
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
        if (!ownsPose) return;
        ownsPose = false;
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

    private void FinishCharge()
    {
        if (chargeEffect != null && chargeEffect.active && chargeEffect.identity == chargeIdentity
            && chargeEffect.owner == Player.whoAmI && chargeEffect.ModProjectile is MokouSacrificeChargeProjectile)
            chargeEffect.Kill();
        chargeEffect = null;
    }

    private bool IsCastEffect(Projectile p) => p.active && p.owner == Player.whoAmI && (int)p.ai[2] == castSerial
        && p.ModProjectile is MokouSacrificeDashProjectile or MokouSacrificeFlameProjectile;

    private bool HasLiveEffects()
    {
        foreach (Projectile p in Main.ActiveProjectiles) if (IsCastEffect(p)) return true;
        return false;
    }

    private void FinishSequence()
    {
        StartCooldown();
        phase = DashPhase.Finishing;
        if (!HasLiveEffects()) End();
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
        FinishCharge();
        FinishHitbox();
        if (interrupted)
            foreach (Projectile p in Main.ActiveProjectiles) if (IsCastEffect(p)) p.Kill();
        if (phase is DashPhase.Windup or DashPhase.Travel or DashPhase.Recovery) Player.velocity = Vector2.Zero;
        ResetPose();
        phase = DashPhase.Ready;
        stepPrepared = false;
        animator = null;
        RemainingDashes = 0;
        Array.Clear(chargingFlames);
        plannedFlames = spawnedFlames = 0;
        EmpoweredCast = false;
        ((Mokou)Owner).CompleteSkillLifeUse(this);
    }
}
