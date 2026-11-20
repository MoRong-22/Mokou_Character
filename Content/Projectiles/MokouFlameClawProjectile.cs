using System;
using Microsoft.Xna.Framework;
using Mokou_Character.Content.Dusts;
using Mokou_Character.SkillVisuals;
using Terraria;
using Terraria.ModLoader;

namespace Mokou_Character.Skill;

/// <summary>固定在出爪位置的倾斜爪痕。ai[0]：左/右/双爪；ai[1]：朝向。</summary>
public sealed class MokouFlameClawProjectile : ModProjectile
{
    public const int Lifetime = 22;
    private const int SweepTicks = 5, Segments = 32;
    private float Age => Lifetime - Projectile.timeLeft;
    private float Reveal => Math.Clamp((Age + 1) / SweepTicks, 0, 1);
    private bool BothHands => Projectile.ai[0] == 2;
    private Vector2 Forward => Projectile.velocity.SafeNormalize(Vector2.UnitX);
    public override string Texture => "Terraria/Images/MagicPixel";

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = (int)MokouClawSkill.Reach * 2;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Generic;
        Projectile.penetrate = -1;
        Projectile.timeLeft = Lifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => Age < SweepTicks ? null : false;
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        => MokouSkillMotion.InFrontArea(Projectile.Center, Forward, MokouClawSkill.Reach, targetHitbox);

    private Vector2 Point(int hand, int lane, float t)
        => MokouSkillMotion.ClawPoint(Projectile.Center, Forward, Projectile.ai[1] < 0 ? -1 : 1,
            MokouClawSkill.Reach, hand == 0 ? 1 : -1, lane, hand == 0 ? t : 1 - t);

    public override void AI()
    {
        if (Main.dedServ) return;
        MokouSkillFlameDust.GetColors(Projectile.owner, out Color dark, out Color light);
        Lighting.AddLight(Projectile.Center + Forward * (MokouClawSkill.Reach * 0.5f), light.ToVector3() * (Projectile.timeLeft / (float)Lifetime));
        if (Age >= SweepTicks) return;
        int first = BothHands ? 0 : (int)Projectile.ai[0], last = BothHands ? 1 : first;
        for (int hand = first; hand <= last; hand++)
            for (int lane = 0; lane < 3; lane++)
            {
                float t = MathHelper.Lerp(Math.Max(0, Reveal - 1f / SweepTicks), Reveal, Main.rand.NextFloat());
                Vector2 tangent = (Point(hand, lane, Math.Min(1, t + 0.01f)) - Point(hand, lane, Math.Max(0, t - 0.01f)))
                    .SafeNormalize(Vector2.UnitY);
                MokouSkillFlameDust.Spawn(Point(hand, lane, t), tangent * 3, dark, light, 0.9f);
            }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        MokouSkillFlameDust.GetColors(Projectile.owner, out Color dark, out Color light);
        float opacity = Math.Clamp(Projectile.timeLeft / (float)(Lifetime - SweepTicks), 0, 1);
        int first = BothHands ? 0 : (int)Projectile.ai[0], last = BothHands ? 1 : first;
        for (int hand = first; hand <= last; hand++)
            for (int lane = 0; lane < 3; lane++)
                for (int segment = 1; segment <= Segments; segment++)
                {
                    float a = (segment - 1f) / Segments;
                    if (a >= Reveal) break;
                    float b = Math.Min(segment / (float)Segments, Reveal);
                    float taper = MathF.Pow(Math.Max(0, MathF.Sin((a + b) * 0.5f * MathHelper.Pi)), 0.65f);
                    float width = (lane == 1 ? 12 : 9) * taper * (0.65f + opacity * 0.35f);
                    SkillFlameDrawing.Stroke(Point(hand, lane, a), Point(hand, lane, b), width, dark, light, opacity);
                }
        return false;
    }
}
