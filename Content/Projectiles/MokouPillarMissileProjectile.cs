using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace Mokou_Character.Skill;

/// <summary>强化火柱燃尽后放出的穿墙飞弹；ai[0]为所属火柱 identity。颜色在生成时锁定并同步。</summary>
public sealed class MokouPillarMissileProjectile : ModProjectile
{
    public const int Lifetime = 75;
    public const float StartSpeed = 7f, MaxSpeed = 12f;
    private Color darkColor = new(166, 15, 3), lightColor = new(255, 184, 51);
    private Asset<Effect> flameEffect;
    public override string Texture => "Terraria/Images/MagicPixel";

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 14;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 1;
        Projectile.timeLeft = Lifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    internal void SetColors(Color dark, Color light) { darkColor = dark; lightColor = light; }
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

    public override void AI()
    {
        float speed = MathHelper.Min(MaxSpeed, Projectile.velocity.Length() + 0.4f);
        Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitY) * speed;
        Projectile.rotation = Projectile.velocity.ToRotation();
        if (!Main.dedServ) Lighting.AddLight(Projectile.Center, lightColor.ToVector3() * 0.55f);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        flameEffect ??= ModContent.Request<Effect>("Mokou_Character/Assets/Effects/KafuuFlamePillar", AssetRequestMode.ImmediateLoad);
        Effect effect = flameEffect.Value;
        effect.CurrentTechnique = effect.Techniques["FlameMissile"];
        effect.Parameters["uTime"].SetValue((Lifetime - Projectile.timeLeft) / 60f);
        effect.Parameters["uDarkColor"].SetValue(darkColor.ToVector3());
        effect.Parameters["uLightColor"].SetValue(this.lightColor.ToVector3());
        float opacity = MathHelper.Clamp(Projectile.timeLeft / 10f, 0f, 1f);
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        Vector2 textureSize = new(pixel.Width, pixel.Height);
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
        try
        {
            Main.spriteBatch.Draw(pixel, Projectile.Center - Main.screenPosition, null, Color.White * opacity,
                Projectile.rotation, textureSize * new Vector2(0.72f, 0.5f), new Vector2(46f, 22f) / textureSize, SpriteEffects.None, 0f);
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
