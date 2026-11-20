using System;
using Microsoft.Xna.Framework;
using Mokou_Character.Content.Dusts;
using Mokou_Character.SkillVisuals;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Mokou_Character.Skill;

/// <summary>富士火山：普通仅落地一爆；ai[1]锁定本次强化时，180 tick 后再爆一次。</summary>
public sealed class MokouKickExplosionProjectile : ModProjectile
{
    public const float Radius = 192f, SecondRadius = 240f;
    public const int SecondBlastDelayTicks = 3 * 60;
    public const int BurstVisualTicks = 26;
    public const int EmpoweredLifetime = SecondBlastDelayTicks + BurstVisualTicks + 1;
    private const int DamageTicks = 3;
    private bool Empowered => Projectile.ai[1] == 1f;
    private int Age => (int)Projectile.ai[0];
    private bool SecondBlast => Empowered && Age >= SecondBlastDelayTicks + 1;
    private float CurrentRadius => SecondBlast ? SecondRadius : Radius;
    public override string Texture => "Terraria/Images/MagicPixel";

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = (int)SecondRadius * 2;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.timeLeft = EmpoweredLifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() =>
        Age is >= 1 and <= DamageTicks || Empowered && Age > SecondBlastDelayTicks && Age <= SecondBlastDelayTicks + DamageTicks
            ? null : false;
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        Vector2 nearest = Vector2.Clamp(Projectile.Center, targetHitbox.TopLeft(), targetHitbox.BottomRight());
        return Vector2.DistanceSquared(Projectile.Center, nearest) <= CurrentRadius * CurrentRadius;
    }

    public override void AI()
    {
        Projectile.ai[0]++;
        // ai 在 SetDefaults 之后写入；首帧才按同步的本次强化标记确定寿命。
        if (Age == 1) Projectile.timeLeft = Empowered ? EmpoweredLifetime : BurstVisualTicks + 1;
        // 一个弹幕负责整套时序；清除第一段的局部免疫，允许第二段再次命中同一个敌人。
        if (Empowered && Age == SecondBlastDelayTicks + 1) Array.Clear(Projectile.localNPCImmunity);
        if (Main.dedServ) return;
        MokouSkillFlameDust.GetColors(Projectile.owner, out Color dark, out Color light);
        bool burst = Age == 1 || Empowered && Age == SecondBlastDelayTicks + 1;
        Lighting.AddLight(Projectile.Center, light.ToVector3() * (burst ? 1.6f : 0.45f));
        if (burst) EmitBurst(dark, light);
        else if (Empowered && Age > BurstVisualTicks && !SecondBlast && Age % 4 == 0)
        {
            float charge = Age / (float)SecondBlastDelayTicks;
            Vector2 position = Projectile.Center + new Vector2(Main.rand.NextFloat(-Radius * 0.8f, Radius * 0.8f), -3);
            MokouSkillFlameDust.Spawn(position, new Vector2(0, -1 - charge * 3), dark, light, 0.7f + charge * 0.6f);
        }
    }

    private void EmitBurst(Color dark, Color light)
    {
        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.65f, Pitch = SecondBlast ? -0.25f : -0.1f }, Projectile.Center);
        int count = SecondBlast ? 100 : 72;
        for (int i = 0; i < count; i++)
        {
            float angle = MathHelper.Pi + MathHelper.Pi * (i + Main.rand.NextFloat()) / count;
            Vector2 direction = angle.ToRotationVector2();
            Vector2 position = Projectile.Center + direction * Main.rand.NextFloat(8, CurrentRadius * 0.75f);
            Vector2 velocity = direction * Main.rand.NextFloat(5, SecondBlast ? 15 : 11) - Vector2.UnitY * 2;
            MokouSkillFlameDust.Spawn(position, velocity, dark, light, Main.rand.NextFloat(1.6f, SecondBlast ? 3.6f : 2.8f));
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        MokouSkillFlameDust.GetColors(Projectile.owner, out Color dark, out Color light);
        int burstAge = SecondBlast ? Age - SecondBlastDelayTicks - 1 : Age - 1;
        if (burstAge >= 0 && burstAge < BurstVisualTicks)
        {
            float progress = burstAge / (float)BurstVisualTicks;
            float radius = CurrentRadius * (0.2f + 0.8f * (1 - MathF.Pow(1 - progress, 3)));
            for (int i = 0; i < 40; i++)
            {
                float a = MathHelper.Pi + MathHelper.Pi * i / 40, b = MathHelper.Pi + MathHelper.Pi * (i + 1) / 40;
                SkillFlameDrawing.Stroke(Projectile.Center + a.ToRotationVector2() * radius,
                    Projectile.Center + b.ToRotationVector2() * radius, 12 * (1 - progress), dark, light, 1 - progress);
            }
            for (int i = -4; i <= 4; i++)
            {
                Vector2 start = Projectile.Center + new Vector2(i * radius * 0.17f, -2);
                Vector2 end = start + new Vector2(i * radius * 0.06f, -radius * (1 - Math.Abs(i) * 0.11f));
                SkillFlameDrawing.Stroke(start, end, 22 * (1 - progress), dark, light, (1 - progress) * 0.65f);
            }
        }
        else if (Empowered && !SecondBlast)
        {
            // 余焰预告第二段的位置，等待时没有伤害。
            float charge = Age / (float)SecondBlastDelayTicks;
            for (int i = 0; i < 24; i++)
            {
                float a = MathHelper.TwoPi * i / 24, b = MathHelper.TwoPi * (i + 0.65f) / 24;
                Vector2 Ring(float angle) => Projectile.Center + new Vector2(MathF.Cos(angle) * Radius, MathF.Sin(angle) * 10 - 3);
                SkillFlameDrawing.Stroke(Ring(a), Ring(b), 3 + charge * 3, dark, light, 0.25f + charge * 0.5f);
            }
        }
        return false;
    }
}
