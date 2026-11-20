using System;
using Microsoft.Xna.Framework;
using Mokou_Character.Character;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Mokou_Character.Skill;

/// <summary>不死鸟附体吟唱期间的风柱伤害和吸附。轮廓对应 NirvanaVortex.fx，不重复绘制龙卷。</summary>
public sealed class MokouPhoenixVortexProjectile : ModProjectile
{
    /// <summary>基础伤害与同一敌人的受击间隔；30 tick = 0.5 秒。</summary>
    public const int BaseDamage = 20;
    public const int HitInterval = 30;
    public const float PullRange = 240f;
    private const float Height = 280f;
    private const float HalfWidth = 170f;
    private float Progress => MathHelper.Clamp(Projectile.ai[0] / MokouPhoenixPossessionSkill.ChantTicks, 0f, 1f);

    public override string Texture => "Terraria/Images/MagicPixel";

    public override void SetDefaults()
    {
        Projectile.width = 400;
        Projectile.height = 320;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Generic;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.timeLeft = MokouPhoenixPossessionSkill.ChantTicks;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = HitInterval;
    }

    public override bool ShouldUpdatePosition() => false;
    public override bool PreDraw(ref Color lightColor) => false;
    public override bool? CanDamage() => Progress > 0.015f && Progress < 0.98f ? null : false;

    public override void AI()
    {
        if ((uint)Projectile.owner >= (uint)Main.maxPlayers)
        {
            Projectile.Kill();
            return;
        }
        Player player = Main.player[Projectile.owner];
        if (!player.active || player.dead || player.CCed || player.mount.Active || player.pulley
            || player.grapCount > 0 || !player.GetModPlayer<Mokou>().IsSelected)
        {
            Projectile.Kill();
            return;
        }
        // 网络端不读取仅在本机运行的 Skill.IsActive；同步弹幕的年龄与结束包即可。
        Projectile.Center = player.Bottom - new Vector2(0f, Height * 0.5f);
        Projectile.ai[0]++;
        if (Projectile.ai[0] >= MokouPhoenixPossessionSkill.ChantTicks)
        {
            Projectile.Kill();
            return;
        }
        if (Projectile.owner == Main.myPlayer && (int)Projectile.ai[0] % 30 == 0)
            Projectile.netUpdate = true;
        if (Main.netMode != NetmodeID.MultiplayerClient && CanDamage() != false)
            PullEnemies(player.Bottom);
    }

    private static float Smooth(float a, float b, float value)
    {
        float t = MathHelper.Clamp((value - a) / (b - a), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float SpeedIntegral(float p, float a, float b)
    {
        float x = MathHelper.Clamp((p - a) / (b - a), 0f, 1f);
        return (b - a) * (x * x * x - 0.5f * x * x * x * x) + Math.Max(p - b, 0f);
    }

    private float VisibleHeight => Math.Min(1.03f, 0.015f + 1.085f * Smooth(0f, 2f / 3f, Progress) - 0.025f);

    /// <summary>按高度取外层风带的轮廓。使用与着色器相同的腰线、扩张阶段和轻微摆动。</summary>
    private void GetCrossSection(float height, out float x, out float radius)
    {
        float p = Progress;
        float scale = 0.56f + 0.22f * Smooth(2f / 3f, 5.15f / 6f, p)
            + 0.32f * Smooth(5.15f / 6f, 0.95f, p);
        float burst = MathHelper.Clamp((p - 0.95f) / 0.05f, 0f, 1f);
        float travel = 1f - (1f - burst) * (1f - burst);
        float modelHeight = (height - 0.43f) / (1f + travel * 0.1f) + 0.43f;
        float waist = 0.32f + 0.20f * MathF.Pow(MathHelper.Clamp(MathF.Abs(modelHeight - 0.46f) / 0.54f, 0f, 1f), 1.4f);
        float root = 0.10f * (1f - Smooth(0f, 0.24f, modelHeight));
        float spin = MokouPhoenixPossessionSkill.ChantTicks / 60f * (0.22f * p
            + 0.38f * SpeedIntegral(p, 0.20f, 2f / 3f)
            + 0.85f * SpeedIntegral(p, 2f / 3f, 5.15f / 6f)
            + 0.65f * SpeedIntegral(p, 5.15f / 6f, 0.95f));
        x = MathF.Sin(modelHeight * 7f - spin * 1.5f) * 0.024f * Smooth(0f, 0.8f, modelHeight)
            * HalfWidth * (1f + travel * 0.65f);
        radius = (waist + root) * scale * 1.10f * HalfWidth * (1f + travel * 0.65f);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        if (CanDamage() == false) return false;
        Vector2 feet = Projectile.Center + new Vector2(0f, Height * 0.5f);
        // 8 像素高的横截面拼成腰形碰撞区；尚未形成的上部不会提前命中。
        float top = feet.Y - Math.Max(0f, VisibleHeight) * Height;
        float from = Math.Max(targetHitbox.Top, top);
        float to = Math.Min(targetHitbox.Bottom, feet.Y + 6f);
        for (float y = from; y < to; y += 8f)
        {
            float sample = (y + Math.Min(y + 8f, to)) * 0.5f;
            GetCrossSection((feet.Y - sample) / Height, out float sway, out float radius);
            if (targetHitbox.Right >= feet.X + sway - radius && targetHitbox.Left <= feet.X + sway + radius)
                return true;
        }
        return false;
    }

    private void PullEnemies(Vector2 feet)
    {
        float strength = Smooth(0f, 0.25f, Progress) * (1f - Smooth(0.95f, 0.98f, Progress));
        Vector2 center = feet - new Vector2(0f, MathHelper.Clamp(VisibleHeight * Height * 0.4f, 8f, 100f));
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];
            if (!npc.active || npc.friendly || npc.townNPC || npc.dontTakeDamage || npc.knockBackResist <= 0f
                || !npc.CanBeChasedBy(Projectile)) continue;
            Vector2 offset = center - npc.Center;
            float distance = offset.Length();
            if (distance < 12f || distance > PullRange) continue;
            Vector2 direction = offset / distance;
            float acceleration = 0.38f * MathHelper.Clamp(npc.knockBackResist, 0f, 1f) * strength
                * (0.35f + 0.65f * (1f - distance / PullRange));
            // 限制朝风眼的吸附速度，不直接瞬移，也不覆盖敌怪自己的切向运动。
            float delta = Math.Min(acceleration, Math.Max(0f, Math.Min(7f, distance * 0.12f) - Vector2.Dot(npc.velocity, direction)));
            if (delta <= 0f) continue;
            npc.velocity += direction * delta;
            if (Main.netMode == NetmodeID.Server && (int)Projectile.ai[0] % 10 == 0) npc.netUpdate = true;
        }
    }
}
