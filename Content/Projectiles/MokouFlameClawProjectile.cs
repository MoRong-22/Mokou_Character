using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.GameContent;
using Mokou_Character.Content.Dusts;
using Mokou_Character.SkillVisuals;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Mokou_Character.Skill;

/// <summary>固定在出爪位置的三道弧形火焰爪痕。ai[0]：左/右/双爪；ai[1]：朝向。</summary>
public sealed class MokouFlameClawProjectile : ModProjectile
{
    public const int Lifetime = 22;
    private const int SweepTicks = 5;
    private Asset<Effect> clawEffect;
    private Color darkColor, lightColor;
    private float Age => Lifetime - Projectile.timeLeft;
    private float Reveal => MokouSkillMotion.Smooth((Age + 1) / SweepTicks);
    private bool BothHands => Projectile.ai[0] == 2;
    private Vector2 Forward => Projectile.velocity.SafeNormalize(Vector2.UnitX);
    public override string Texture => "Terraria/Images/MagicPixel";

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = (int)MokouClawSkill.Reach * 2;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = -1;
        Projectile.timeLeft = Lifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override bool ShouldUpdatePosition() => false;

    // 爪痕脱手后保持自身焰色，不能随着接下来释放的技能变色。
    public override void OnSpawn(IEntitySource source)
        => MokouSkillFlameDust.GetColors(Projectile.owner, out darkColor, out lightColor);

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(darkColor.PackedValue);
        writer.Write(lightColor.PackedValue);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        darkColor = new Color { PackedValue = reader.ReadUInt32() };
        lightColor = new Color { PackedValue = reader.ReadUInt32() };
    }
    public override bool? CanDamage() => Age < SweepTicks ? null : false;
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        => MokouSkillMotion.InFrontArea(Projectile.Center, Forward, MokouClawSkill.Reach, targetHitbox);

    private Vector2 Point(int hand, int lane, float t)
        => MokouSkillMotion.ClawPoint(Projectile.Center, Forward, Projectile.ai[1] < 0 ? -1 : 1,
            MokouClawSkill.Reach, hand == 0 ? 1 : -1, lane, hand == 0 ? t : 1 - t);

    public override void AI()
    {
        if (Main.dedServ) return;
        Color dark = darkColor, light = this.lightColor;
        Lighting.AddLight(Projectile.Center + Forward * (MokouClawSkill.Reach * 0.5f), light.ToVector3() * (Projectile.timeLeft / (float)Lifetime));
        if (Age >= SweepTicks) return;
        int first = BothHands ? 0 : (int)Projectile.ai[0], last = BothHands ? 1 : first;
        for (int hand = first; hand <= last; hand++)
            for (int lane = 0; lane < 3; lane++)
            {
                float t = MathHelper.Lerp(MokouSkillMotion.Smooth(Age / SweepTicks), Reveal, Main.rand.NextFloat());
                Vector2 tangent = (Point(hand, lane, Math.Min(1, t + 0.01f)) - Point(hand, lane, Math.Max(0, t - 0.01f)))
                    .SafeNormalize(Vector2.UnitY);
                MokouSkillFlameDust.Spawn(Point(hand, lane, t), tangent * 3, dark, light, 0.9f);
            }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Color dark = darkColor, light = this.lightColor;
        clawEffect ??= ModContent.Request<Effect>(
            "Mokou_Character/Assets/Effects/HandFlame", AssetRequestMode.ImmediateLoad);
        Effect effect = clawEffect.Value;
        effect.CurrentTechnique = effect.Techniques["Claw"];
        effect.Parameters["uTime"].SetValue((float)Main.GlobalTimeWrappedHourly);
        effect.Parameters["uOpacity"].SetValue(Math.Clamp(Projectile.timeLeft / (float)(Lifetime - SweepTicks), 0, 1));
        effect.Parameters["uReveal"].SetValue(Reveal);
        effect.Parameters["uDarkColor"].SetValue(dark.ToVector3());
        effect.Parameters["uLightColor"].SetValue(light.ToVector3());
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        Vector2 textureSize = new(pixel.Width, pixel.Height);
        Vector2 center = Projectile.Center + Forward * (MokouClawSkill.Reach * 0.50f);
        int facing = Projectile.ai[1] < 0 ? -1 : 1;
        int first = BothHands ? 0 : (int)Projectile.ai[0], last = BothHands ? 1 : first;
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
        try
        {
            for (int hand = first; hand <= last; hand++)
            {
                Vector2 axis = MokouSkillMotion.ClawAxis(Forward, facing, hand == 0 ? 1 : -1);
                effect.Parameters["uReverse"].SetValue(hand == 0 ? 0f : 1f);
                Main.spriteBatch.Draw(pixel, center - Main.screenPosition, null, Color.White, axis.ToRotation(),
                    textureSize * 0.5f, MokouSkillMotion.ClawCanvas * MokouClawSkill.Reach / textureSize,
                    facing < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None, 0f);
            }
        }
        finally
        {
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
        return false;
    }
}
