using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mokou_Character.Character;
using Mokou_Character.Content.Dusts;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Mokou_Character.Skill;

/// <summary>旋踢和俯冲的近战命中框；旋踢记录前脚世界路径，留下逐渐熄灭的火焰拖尾。</summary>
public sealed class MokouKafuuKaiseyKickProjectile : ModProjectile
{
    private const int SpinPhase = 0;
    private const int DivePhase = 1;
    private const int TrailTicks = 10;
    private const int TrailCapacity = 64;
    /// <summary>火焰弧沿腰部到脚尖的方向向外延伸的距离，单位为游戏像素。</summary>
    private const float TrailOutwardOffset = 52f;
    private readonly Vector2[] trailPositions = new Vector2[TrailCapacity];
    private readonly float[] trailTimes = new float[TrailCapacity];
    private int trailStart, trailCount;
    private Vector2 previousFoot, previousPivot;
    private bool hasPreviousFoot;

    public override string Texture => "Terraria/Images/MagicPixel";

    public override void SetDefaults()
    {
        Projectile.width = 120;
        Projectile.height = 120;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 240;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override bool ShouldUpdatePosition() => false;

    public override bool? CanDamage()
        => Projectile.ai[0] == DivePhase ||
            (Projectile.ai[0] == SpinPhase && Projectile.localAI[2] <= Projectile.ai[2]) ? null : false;

    public override void AI()
    {
        if ((uint)Projectile.owner >= (uint)Main.maxPlayers)
        {
            Projectile.Kill();
            return;
        }

        Player player = Main.player[Projectile.owner];
        if (!player.active || player.dead)
        {
            Projectile.Kill();
            return;
        }

        if (Projectile.localAI[1] == 0f)
        {
            // 旋踢结束后只保留拖尾，不再命中，也不再向路径追加点。
            Projectile.timeLeft = Math.Max(1, (int)Projectile.ai[2])
                + (Projectile.ai[0] == SpinPhase ? TrailTicks : 0);
            Projectile.localAI[1] = 1f;
        }

        Projectile.localAI[2]++;
        bool isDive = Projectile.ai[0] == DivePhase;
        Projectile.width = isDive ? 86 : 132;
        Projectile.height = 132;
        Vector2 center = player.MountedCenter;
        if (isDive)
        {
            // velocity 保存飞踢的瞄准方向；ShouldUpdatePosition 已关闭，不会自动移动弹幕。
            center += Projectile.velocity.SafeNormalize(Vector2.UnitY) * 20f;
            // 落地由技能根据实际地形判定并结束命中框，水平飞踢不能仅因 Y=0 就消失。
        }

        Projectile.Center = center;
        if (!isDive && !Main.dedServ && Projectile.localAI[2] <= Projectile.ai[2])
            RecordFootTrail(player);
        if (Projectile.localAI[0] != 0f || Main.dedServ) return;
        Projectile.localAI[0] = 1f;

        // 旋踢粒子沿前脚路径生成，俯冲保留起步时的火焰。
        if (!isDive) return;
        MokouSkillFlameDust.GetColors(Projectile.owner, out Color dark, out Color light);
        int dustCount = 10;
        for (int i = 0; i < dustCount; i++)
        {
            Vector2 velocity = new(-player.velocity.X * 0.2f, Main.rand.NextFloat(-2.8f, 0f));
            MokouSkillFlameDust.Spawn(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                velocity, dark, light, Main.rand.NextFloat(1.1f, 1.6f));
        }
    }

    /// <summary>读取脚尖与腰部坐标，沿脚尖方向外扩火焰弧，再用短弧补点避免快速旋转留下折线。</summary>
    private void RecordFootTrail(Player player)
    {
        var character = player.GetModPlayer<Mokou>();
        if (!character.IsSelected) return;
        var animator = character.Animator;
        if (animator.IsDisposed
            || !animator.TryGetBoneTransform("front_foot", out Matrix footTransform)
            || !animator.TryGetBoneTransform("body_2", out Matrix pivotTransform)) return;

        float footLength = animator.Skeleton.FindBone("front_foot").Data.Length;
        Vector2 foot = player.Bottom + Vector2.Transform(new Vector2(footLength * 0.55f, 0f), footTransform) * character.CharacterScale;
        Vector2 pivot = player.Bottom + new Vector2(pivotTransform.M41, pivotTransform.M42) * character.CharacterScale;
        // 先扩展半径再记录世界路径，火焰弧与粒子一起移到角色外围，左右镜像保持一致。
        foot += (foot - pivot).SafeNormalize(new Vector2(player.direction, 0f)) * TrailOutwardOffset;
        MokouSkillFlameDust.GetColors(Projectile.owner, out Color dark, out Color light);
        float time = Projectile.localAI[2];
        if (!hasPreviousFoot || Vector2.DistanceSquared(pivot, previousPivot) > 128f * 128f)
        {
            // 传送不把两处场景连成一条线。
            trailStart = trailCount = 0;
            AddTrailPoint(foot, time);
            hasPreviousFoot = true;
        }
        else
        {
            Vector2 before = previousFoot - previousPivot;
            Vector2 after = foot - pivot;
            float angle = before.ToRotation();
            float delta = MathHelper.WrapAngle(after.ToRotation() - angle);
            for (int i = 1; i <= 4; i++)
            {
                float t = i / 4f;
                Vector2 point = Vector2.Lerp(previousPivot, pivot, t)
                    + (angle + delta * t).ToRotationVector2() * MathHelper.Lerp(before.Length(), after.Length(), t);
                AddTrailPoint(point, time - 1f + t);
                MokouSkillFlameDust.Spawn(point,
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-1.1f, -0.2f)),
                    dark, light, Main.rand.NextFloat(1f, 1.5f));
            }
        }
        previousFoot = foot;
        previousPivot = pivot;
        Lighting.AddLight(foot, light.ToVector3() * 0.9f);
    }

    private void AddTrailPoint(Vector2 position, float time)
    {
        int index = (trailStart + trailCount) % TrailCapacity;
        trailPositions[index] = position;
        trailTimes[index] = time;
        if (trailCount < TrailCapacity) trailCount++;
        else trailStart = (trailStart + 1) % TrailCapacity;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        MokouSkillFlameDust.GetColors(Projectile.owner, out Color dark, out Color light);
        Color glow = new(light.R, light.G, light.B, 0);
        for (int i = 1; i < trailCount; i++)
        {
            int before = (trailStart + i - 1) % TrailCapacity;
            int current = (trailStart + i) % TrailCapacity;
            float remaining = MathHelper.Clamp(1f - (Projectile.localAI[2] - trailTimes[current]) / TrailTicks, 0f, 1f);
            if (remaining <= 0f) continue;
            float width = (22f + 3f * MathF.Sin(i * 1.7f + Projectile.localAI[2])) * remaining;
            DrawTrailSegment(trailPositions[before], trailPositions[current],
                dark * remaining, width + 7f * remaining);
            // 预乘透明度下 A=0 只叠加火光，无需切换 SpriteBatch 或分配 RT。
            DrawTrailSegment(trailPositions[before], trailPositions[current],
                Color.Lerp(dark, light, 0.65f) * remaining, width * 0.70f);
            DrawTrailSegment(trailPositions[before], trailPositions[current],
                glow * (remaining * 0.9f), width * 0.25f);
        }
        return false;
    }

    private static void DrawTrailSegment(Vector2 start, Vector2 end, Color color, float width)
    {
        Vector2 line = end - start;
        float length = line.Length();
        if (length < 0.01f) return;
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        Main.EntitySpriteDraw(pixel, start - Main.screenPosition, null, color, line.ToRotation(),
            new Vector2(0f, pixel.Height * 0.5f),
            new Vector2((length + 0.5f) / pixel.Width, width / pixel.Height), SpriteEffects.None);
    }
}
