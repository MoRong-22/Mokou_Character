using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mokou_Character.Content.Dusts;
using Mokou_Character.SkillVisuals;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Mokou_Character.Skill;

/// <summary>富士火山：普通燃尽消散；强化燃尽爆开，发射穿墙飞弹。ai[1]锁定本次强化。</summary>
public sealed class MokouKickExplosionProjectile : ModProjectile
{
    // 按原版玩家 20×42 像素计，主体约两人宽、五人高；画布另留辉光空白。
    public const float PillarWidth = 40f, PillarHeight = 210f;
    public const int PillarVisualTicks = 2 * 60;
    public const int BurstVisualTicks = 18;
    public const int MissileCount = 6;
    public const float BurstRadius = 64f;
    public const int EmpoweredLifetime = PillarVisualTicks + MokouPillarMissileProjectile.Lifetime + 2;
    private const int FormationTicks = 12;
    /// <summary>燃烧期间同一敌人的受击间隔；30 tick 为 0.5 秒，各敌人独立计时。</summary>
    public const int PillarHitInterval = 30;
    private Asset<Effect> pillarEffect;
    private FlamePillarRings rings;
    private Color darkColor, lightColor;
    private bool hasColors;
    private bool Empowered => Projectile.ai[1] == 1f;
    private int Age => (int)Projectile.ai[0];
    private Vector2 BurstCenter => Projectile.Center - Vector2.UnitY * (PillarHeight * 0.82f);
    public override string Texture => "Terraria/Images/MagicPixel";

    public override void SetStaticDefaults()
        => ProjectileID.Sets.DrawScreenCheckFluff[Type] = (int)PillarHeight + 80;

    public override void SetDefaults()
    {
        Projectile.width = (int)PillarWidth;
        Projectile.height = (int)PillarHeight;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = -1;
        Projectile.timeLeft = EmpoweredLifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = PillarHitInterval;
    }

    public override void OnSpawn(IEntitySource source)
    {
        MokouSkillFlameDust.GetColors(Projectile.owner, out darkColor, out lightColor);
        hasColors = true;
    }

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(darkColor.PackedValue);
        writer.Write(lightColor.PackedValue);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        darkColor = new Color { PackedValue = reader.ReadUInt32() };
        lightColor = new Color { PackedValue = reader.ReadUInt32() };
        hasColors = true;
    }

    public override bool ShouldUpdatePosition() => false;
    // 整段燃烧都能命中，中途进入火柱的敌人同样生效；强化爆开单独命中一次。
    public override bool? CanDamage() => Age is >= 1 and <= PillarVisualTicks
        || Empowered && Age > PillarVisualTicks && Age <= PillarVisualTicks + 3 ? null : false;

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        if (CanDamage() == false) return false;
        if (Age <= PillarVisualTicks)
        {
            float height = PillarHeight * MathHelper.Clamp(Age / (float)FormationTicks, 0f, 1f);
            return new Rectangle((int)(Projectile.Center.X - PillarWidth * 0.5f),
                (int)(Projectile.Center.Y - height), (int)PillarWidth, (int)MathF.Ceiling(height)).Intersects(targetHitbox);
        }
        Vector2 nearest = Vector2.Clamp(BurstCenter, targetHitbox.TopLeft(), targetHitbox.BottomRight());
        return Vector2.DistanceSquared(BurstCenter, nearest) <= BurstRadius * BurstRadius;
    }

    public override void AI()
    {
        Projectile.ai[0]++;
        if (Age == 1) Projectile.timeLeft = Empowered ? EmpoweredLifetime : PillarVisualTicks + 1;
        if (Empowered && Age == PillarVisualTicks + 1)
        {
            // 火柱刚刚命中过也允许爆开命中，但三帧爆开窗口内不重复受伤。
            Projectile.localNPCHitCooldown = -1;
            Array.Clear(Projectile.localNPCImmunity);
            // 只有所属客户端生成一次；服务器及其他客户端接收同步弹幕。
            if (Projectile.owner == Main.myPlayer) ReleaseMissiles();
            if (!Main.dedServ)
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.65f, Pitch = 0.1f }, BurstCenter);
        }
        // 飞弹仍属于这次强化，全部消失才结束父弹幕，让技能最后统一结算焰色和回血。
        if (Empowered && Age > PillarVisualTicks + BurstVisualTicks
            && Projectile.owner == Main.myPlayer && !HasMissiles())
        {
            Projectile.Kill();
            return;
        }
        if (Main.dedServ) return;
        if (Age == 1)
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.5f, Pitch = -0.1f }, Projectile.Center);
        if (Age <= PillarVisualTicks + BurstVisualTicks)
            Lighting.AddLight(Projectile.Center - Vector2.UnitY * (PillarHeight * 0.5f), lightColor.ToVector3() * 0.8f);
    }

    private void ReleaseMissiles()
    {
        for (int i = 0; i < MissileCount; i++)
        {
            float angle = MathHelper.TwoPi * (i + 0.5f) / MissileCount;
            int index = Projectile.NewProjectile(Projectile.GetSource_FromAI(), BurstCenter,
                angle.ToRotationVector2() * MokouPillarMissileProjectile.StartSpeed,
                ModContent.ProjectileType<MokouPillarMissileProjectile>(), Math.Max(1, Projectile.damage / 2),
                3f, Projectile.owner, Projectile.identity);
            if ((uint)index < Main.maxProjectiles
                && Main.projectile[index].ModProjectile is MokouPillarMissileProjectile missile)
            {
                missile.SetColors(darkColor, lightColor);
                Main.projectile[index].netUpdate = true;
            }
        }
    }

    private bool IsChild(Projectile child) => child.active && child.owner == Projectile.owner
        && child.type == ModContent.ProjectileType<MokouPillarMissileProjectile>()
        && (int)child.ai[0] == Projectile.identity;

    private bool HasMissiles()
    {
        foreach (Projectile child in Main.ActiveProjectiles)
            if (IsChild(child)) return true;
        return false;
    }

    public override void OnKill(int timeLeft)
    {
        if (!Empowered) return;
        foreach (Projectile child in Main.ActiveProjectiles)
            if (IsChild(child)) child.Kill();
    }

    public override bool PreDraw(ref Color lightColor)
    {
        int tick = Age - 1;
        if (tick < 0 || tick >= PillarVisualTicks + BurstVisualTicks) return false;
        if (!Empowered && tick >= PillarVisualTicks) return false;
        if (!hasColors) MokouSkillFlameDust.GetColors(Projectile.owner, out darkColor, out this.lightColor);
        pillarEffect ??= ModContent.Request<Effect>("Mokou_Character/Assets/Effects/KafuuFlamePillar", AssetRequestMode.ImmediateLoad);
        Effect effect = pillarEffect.Value;
        effect.Parameters["uDarkColor"].SetValue(darkColor.ToVector3());
        effect.Parameters["uLightColor"].SetValue(this.lightColor.ToVector3());
        effect.Parameters["uEmpowered"].SetValue(Empowered ? 1f : 0f);
        GraphicsDevice device = Main.instance.GraphicsDevice;
        var oldVertices = device.GetVertexBuffers();
        var oldIndices = device.Indices;
        Main.spriteBatch.End();
        try
        {
            if (tick < PillarVisualTicks)
            {
                effect.Parameters["uTime"].SetValue(tick / 60f);
                effect.Parameters["uDuration"].SetValue(PillarVisualTicks / 60f);
                float progress = tick / (float)PillarVisualTicks;
                DrawRings(effect, progress, front: false);
                DrawSprite(effect, "FlamePillar", Projectile.Center,
                    new Vector2(PillarWidth / 0.62f, PillarHeight / 0.88f), new Vector2(0.5f, 1f));
                DrawRings(effect, progress, front: true);
            }
            else
            {
                effect.Parameters["uTime"].SetValue((tick - PillarVisualTicks) / 60f);
                effect.Parameters["uDuration"].SetValue(BurstVisualTicks / 60f);
                DrawSprite(effect, "FlameBurst", BurstCenter, new Vector2(BurstRadius * 2.5f), new Vector2(0.5f));
            }
        }
        finally
        {
            device.SetVertexBuffers(oldVertices);
            device.Indices = oldIndices;
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
        return false;
    }

    private void DrawRings(Effect effect, float progress, bool front)
    {
        rings ??= new FlamePillarRings();
        int count = rings.Build(Projectile.Center - Main.screenPosition, PillarWidth, PillarHeight, progress, front);
        if (count == 0) return;
        GraphicsDevice device = Main.instance.GraphicsDevice;
        Matrix projection = Matrix.CreateOrthographicOffCenter(0, device.Viewport.Width, device.Viewport.Height, 0, 0, 1);
        effect.Parameters["uTransform"].SetValue(Main.GameViewMatrix.TransformationMatrix * projection);
        effect.CurrentTechnique = effect.Techniques["FlameRing"];
        device.BlendState = BlendState.AlphaBlend;
        device.DepthStencilState = DepthStencilState.None;
        device.RasterizerState = RasterizerState.CullNone;
        effect.CurrentTechnique.Passes[0].Apply();
        device.DrawUserPrimitives(PrimitiveType.TriangleList, rings.Vertices, 0, count / 3);
    }

    private static void DrawSprite(Effect effect, string technique, Vector2 center, Vector2 size, Vector2 origin)
    {
        effect.CurrentTechnique = effect.Techniques[technique];
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        Vector2 textureSize = new(pixel.Width, pixel.Height);
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
        try
        {
            Main.spriteBatch.Draw(pixel, center - Main.screenPosition, null, Color.White, 0f,
                textureSize * origin, size / textureSize, SpriteEffects.None, 0f);
        }
        finally { Main.spriteBatch.End(); }
    }
}
