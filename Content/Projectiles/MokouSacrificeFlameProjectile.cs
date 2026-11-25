using System;
using System.IO;
using Microsoft.Xna.Framework;
using Mokou_Character.Character;
using Mokou_Character.Content.Dusts;
using Mokou_Character.SkillVisuals;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Mokou_Character.Skill;

/// <summary>舍身击独立火弹，淡入并短暂停留后自行发射。ai[0]：0显现/1飞行；ai[1]为视觉错相，ai[2]为技能编号。</summary>
public sealed class MokouSacrificeFlameProjectile : ModProjectile
{
    // timeLeft 仅作兜底；实际射程由飞行距离控制，按最低速度预留足够寿命。
    public const int FlightTicks = (int)(MokouSacrificeStrikeSkill.MaxDashDistance / (StartSpeed * UpdatesPerTick)) + 2;
    public const int BurnTicks = 3 * 60;
    /// <summary>8帧淡入，再完整显示10帧；每枚火弹按自己的出生时间计时。</summary>
    public const int FadeInTicks = 8, HoldTicks = 10, LaunchDelayTicks = FadeInTicks + HoldTicks;
    private const int UpdatesPerTick = 2;
    // 速度按子更新计，实际每游戏帧 24～36 像素，与角色冲刺同向。
    public const float StartSpeed = 12f, MaxSpeed = 18f;
    private readonly SacrificeFlameDrawing drawing = new();
    private Color dark, light;
    private int appearAge;
    private float visualTime;
    private bool flightInitialized;
    private float flightRange = MokouSacrificeStrikeSkill.MaxDashDistance;
    private Vector2 flightOrigin;
    private float RemainingRange => Math.Max(0f, flightRange - Vector2.Distance(flightOrigin, Projectile.Center));
    private bool Flying => Projectile.ai[0] == 1f;
    public override string Texture => "Terraria/Images/MagicPixel";
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 12;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 1;
        // 蓄力期间独立悬停、缓入；飞行寿命在 Release 时重新开始。
        Projectile.timeLeft = LaunchDelayTicks + 30;
        Projectile.extraUpdates = 0;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        // 每枚火弹独立判定，避免第一枚命中后其余火弹被目标全局免疫吞掉。
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }
    public override void OnSpawn(IEntitySource source)
        => MokouSkillFlameDust.GetColors(Projectile.owner, out dark, out light);
    public override bool ShouldUpdatePosition() => Flying;
    public override bool? CanDamage() => Flying ? null : false;

    /// <summary>每段蓄力时传入本次冲刺距离；每枚火弹从自己的出生点飞行同样长的距离。</summary>
    internal void SetRange(float distance)
        => flightRange = Math.Clamp(distance, 1f, MokouSacrificeStrikeSkill.MaxDashDistance);

    /// <summary>只有拥有者根据本弹幕的出生计时发射，其他端接收 ai[0] 和速度。</summary>
    private void Release(Vector2 direction)
    {
        if (Flying) return;
        Projectile.ai[0] = 1f;
        flightOrigin = Projectile.Center;
        Projectile.velocity = direction.SafeNormalize(Vector2.UnitX) * Math.Min(StartSpeed, flightRange);
        StartFlight();
        Projectile.netUpdate = true;
    }

    private void StartFlight()
    {
        if (flightInitialized) return;
        flightInitialized = true;
        Projectile.extraUpdates = UpdatesPerTick - 1;
        Projectile.tileCollide = true;
        Projectile.timeLeft = FlightTicks * UpdatesPerTick;
    }

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(dark.PackedValue); writer.Write(light.PackedValue);
        writer.Write(appearAge); writer.Write(visualTime);
        writer.Write(flightRange);
        writer.Write(flightOrigin.X); writer.Write(flightOrigin.Y);
    }
    public override void ReceiveExtraAI(BinaryReader reader)
    {
        dark = new Color { PackedValue = reader.ReadUInt32() };
        light = new Color { PackedValue = reader.ReadUInt32() };
        appearAge = reader.ReadInt32();
        visualTime = reader.ReadSingle();
        flightRange = reader.ReadSingle();
        flightOrigin = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        if (Flying) StartFlight();
    }
    public override void AI()
    {
        // Terraria.Update 在调用 AI 前先将 numUpdates 减一。
        // 无额外更新时这里是 -1；用 0 判断会导致悬停火弹永远透明。
        if (Projectile.numUpdates == -1) appearAge++;
        visualTime += 1f / (60f * (Projectile.extraUpdates + 1));
        Projectile.rotation = Projectile.velocity.ToRotation();
        if (!Flying)
        {
            if ((uint)Projectile.owner >= Main.maxPlayers) { Projectile.Kill(); return; }
            Player player = Main.player[Projectile.owner];
            if (!player.active || player.dead) { Projectile.Kill(); return; }
            if (Projectile.owner == Main.myPlayer)
            {
                var skill = player.GetModPlayer<Mokou>().SacrificeStrikeSkill;
                if (!skill.IsActive || skill.CastSerial != (int)Projectile.ai[2]) { Projectile.Kill(); return; }
                if (appearAge >= LaunchDelayTicks) Release(Projectile.velocity);
            }
            if (!Main.dedServ)
                Lighting.AddLight(Projectile.Center, light.ToVector3() * (0.3f * Appearance));
            return;
        }
        StartFlight();
        float remaining = RemainingRange;
        if (remaining <= 0.01f) { Projectile.Kill(); return; }
        // 最后一个子更新也夹到剩余射程，防止高速火弹多飞出一截。
        Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX)
            * Math.Min(remaining, Math.Min(MaxSpeed, Projectile.velocity.Length() + 0.25f));
        Projectile.rotation = Projectile.velocity.ToRotation();
        if (!Main.dedServ && Projectile.numUpdates == -1)
            Lighting.AddLight(Projectile.Center, light.ToVector3() * 0.4f);
    }
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        => target.AddBuff(BuffID.OnFire, BurnTicks);
    private float Appearance => MokouSkillMotion.Smooth(appearAge / (float)FadeInTicks);
    public override bool PreDraw(ref Color lightColor)
    {
        float grow = Appearance;
        float fade = Flying ? MathHelper.Clamp(RemainingRange / Math.Min(120f, flightRange * 0.2f), 0f, 1f) : 1f;
        drawing.Sprite("Firebolt", Projectile.Center, new Vector2(38f, 18f) * MathHelper.Lerp(0.25f, 1f, grow),
            new Vector2(0.76f, 0.5f), Projectile.rotation, visualTime + Projectile.ai[1], 0f, dark, light, grow * fade);
        return false;
    }
}
