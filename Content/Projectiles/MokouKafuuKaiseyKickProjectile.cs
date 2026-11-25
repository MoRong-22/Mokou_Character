using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mokou_Character.Character;
using Mokou_Character.Content.Dusts;
using Mokou_Character.SkillVisuals;
using Terraria;
using Terraria.ModLoader;

namespace Mokou_Character.Skill;

/// <summary>旋踢和俯冲的近战命中框；旋踢记录前脚世界路径，留下逐渐熄灭的火焰拖尾。</summary>
public sealed class MokouKafuuKaiseyKickProjectile : ModProjectile
{
    private const int SpinPhase = 0;
    private const int DivePhase = 1;
    private const int TrailTicks = 9;
    private const int TrailCapacity = 128;
    private const int TrailSubsteps = 8;
    /// <summary>火焰弧沿腰部到脚尖的方向向外延伸的距离，单位为游戏像素。</summary>
    private const float TrailOutwardOffset = 28f;
    private readonly Vector2[] trailPositions = new Vector2[TrailCapacity];
    private readonly float[] trailTimes = new float[TrailCapacity];
    private readonly Vector2[] trailNormals = new Vector2[TrailCapacity];
    private readonly float[] trailDistances = new float[TrailCapacity];
    private KafuuCrescentMesh trailMesh;
    private readonly KafuuFlameDrawing drawing = new();
    private Vector2 diveFoot;
    // 包含外焰和辉光的半宽；实际亮芯窄于此范围。
    private const float TrailHalfWidth = 42f;
    private int trailStart, trailCount;
    private Vector2 previousFoot, previousPivot;
    private bool hasPreviousFoot;

    public override string Texture => "Terraria/Images/MagicPixel";

    public override void SetDefaults()
    {
        Projectile.width = 120;
        Projectile.height = 120;
        Projectile.friendly = true;
        Projectile.hide = true;
        Projectile.DamageType = DamageClass.Magic;
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
        if (isDive && !Main.dedServ)
        {
            diveFoot = player.MountedCenter + Projectile.velocity.SafeNormalize(Vector2.UnitY) * 25f;
            if (TryGetKickPoints(player, out Vector2 foot, out _)) diveFoot = foot;
            MokouSkillFlameDust.GetColors(Projectile.owner, out _, out Color light);
            Lighting.AddLight(diveFoot, light.ToVector3() * 1.2f);
        }
        if (!isDive && !Main.dedServ && Projectile.localAI[2] <= Projectile.ai[2])
            RecordFootTrail(player);
    }

    /// <summary>读取脚尖与腰部坐标，沿脚尖方向外扩火焰弧，再用短弧补点避免快速旋转留下折线。</summary>
    private void RecordFootTrail(Player player)
    {
        if (!TryGetKickPoints(player, out Vector2 foot, out Vector2 pivot)) return;
        // 先扩展半径再记录世界路径，火焰弧移到角色外围，左右镜像保持一致。
        foot += (foot - pivot).SafeNormalize(new Vector2(player.direction, 0f)) * TrailOutwardOffset;
        MokouSkillFlameDust.GetColors(Projectile.owner, out _, out Color light);
        float time = Projectile.localAI[2];
        if (!hasPreviousFoot || Vector2.DistanceSquared(pivot, previousPivot) > 128f * 128f)
        {
            // 传送不把两处场景连成一条线。
            trailStart = trailCount = 0;
            AddTrailPoint(foot, pivot, time);
            hasPreviousFoot = true;
        }
        else
        {
            Vector2 before = previousFoot - previousPivot;
            Vector2 after = foot - pivot;
            float angle = before.ToRotation();
            float delta = MathHelper.WrapAngle(after.ToRotation() - angle);
            for (int i = 1; i <= TrailSubsteps; i++)
            {
                float t = i / (float)TrailSubsteps;
                Vector2 point = Vector2.Lerp(previousPivot, pivot, t)
                    + (angle + delta * t).ToRotationVector2() * MathHelper.Lerp(before.Length(), after.Length(), t);
                AddTrailPoint(point, Vector2.Lerp(previousPivot, pivot, t), time - 1f + t);
            }
        }
        previousFoot = foot;
        previousPivot = pivot;
        Lighting.AddLight(foot, light.ToVector3() * 0.9f);
    }

    private void AddTrailPoint(Vector2 position, Vector2 pivot, float time)
    {
        float distance = 0f;
        if (trailCount > 0)
        {
            int last = (trailStart + trailCount - 1) % TrailCapacity;
            float step = Vector2.Distance(trailPositions[last], position);
            if (step < 0.01f) return;
            distance = trailDistances[last] + step;
        }
        int index = (trailStart + trailCount) % TrailCapacity;
        trailPositions[index] = position;
        trailTimes[index] = time;
        trailNormals[index] = (position - pivot).SafeNormalize(Vector2.UnitY);
        trailDistances[index] = distance;
        if (trailCount < TrailCapacity) trailCount++;
        else trailStart = (trailStart + 1) % TrailCapacity;
    }

    /// <summary>读取实际脚尖和腰部，旋踢与飞踢共用骨骼锚点，不用碰撞框猜测火焰位置。</summary>
    private static bool TryGetKickPoints(Player player, out Vector2 foot, out Vector2 pivot)
    {
        foot = pivot = player.MountedCenter;
        var character = player.GetModPlayer<Mokou>();
        if (!character.IsSelected) return false;
        var animator = character.Animator;
        if (animator == null || animator.IsDisposed) return false;
        animator.FlipX = player.direction < 0;
        animator.RefreshWorldPose();
        if (!animator.TryGetBoneTransform("front_foot", out Matrix footTransform)
            || !animator.TryGetBoneTransform("body_2", out Matrix pivotTransform)) return false;
        float footLength = animator.Skeleton.FindBone("front_foot").Data.Length;
        foot = player.Bottom + Vector2.Transform(new Vector2(footLength * 0.55f, 0), footTransform) * character.CharacterScale;
        pivot = player.Bottom + new Vector2(pivotTransform.M41, pivotTransform.M42) * character.CharacterScale;
        return true;
    }

    // 火头需要包住飞踢的脚与身体；月牙中间留空，因此同样能在此前景层绘制。
    public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
        List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => overPlayers.Add(index);

    public override bool PreDraw(ref Color lightColor)
    {
        MokouSkillFlameDust.GetColors(Projectile.owner, out Color dark, out Color light);
        float time = Projectile.localAI[2] / 60f;
        if (Projectile.ai[0] == DivePhase)
        {
            Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitY);
            drawing.DrawDive(diveFoot, direction.ToRotation(), time,
                MathHelper.Clamp(Projectile.localAI[2] / 3f, 0f, 1f), dark, light);
            return false;
        }
        if (trailCount < 2) return false;
        trailMesh ??= new KafuuCrescentMesh(TrailCapacity);
        int vertices = trailMesh.Build(trailPositions, trailNormals, trailTimes, trailDistances,
            trailStart, trailCount, Projectile.localAI[2], TrailTicks, TrailHalfWidth, Main.screenPosition);
        drawing.DrawArc(trailMesh.Vertices, vertices, time, dark, light);
        return false;
    }
}