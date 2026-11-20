using System;
using Microsoft.Xna.Framework;
using Mokou_Character.Character;
using Mokou_Character.Content.Dusts;
using Mokou_Character.SkillVisuals;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Mokou_Character.Skill;

/// <summary>冲刺身体碰撞和固定世界坐标拖尾；一次冲刺对同一目标只命中一次。</summary>
public sealed class MokouSacrificeDashProjectile : ModProjectile
{
    private const int TrailTicks = 12, Capacity = 32;
    private readonly Vector2[] points = new Vector2[Capacity];
    private readonly int[] times = new int[Capacity];
    private int count, age, stoppedTicks;
    private Vector2 previousCenter;
    public override string Texture => "Terraria/Images/MagicPixel";

    public override void SetStaticDefaults()
        => ProjectileID.Sets.DrawScreenCheckFluff[Type] = (int)MokouSacrificeStrikeSkill.MaxDashDistance;

    public override void SetDefaults()
    {
        Projectile.width = 32; Projectile.height = 48;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Generic;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.timeLeft = MokouSacrificeStrikeSkill.MaxDashTicks + TrailTicks + 2;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => Projectile.ai[0] == 0 || stoppedTicks <= 1 ? null : false;

    /// <summary>保留最后实际走过的一段判定；此后只淡出轨迹，不跟随玩家移动。</summary>
    internal void Finish(Vector2 end)
    {
        if (Projectile.ai[0] != 0) return;
        if (Vector2.DistanceSquared(end, Projectile.Center) > 0.001f) Record(end);
        Projectile.ai[0] = 1;
        Projectile.timeLeft = TrailTicks;
        Projectile.netUpdate = true;
    }

    public override void AI()
    {
        age++;
        if (Projectile.ai[0] != 0) { stoppedTicks++; return; }
        if ((uint)Projectile.owner >= Main.maxPlayers) { Projectile.Kill(); return; }
        Player player = Main.player[Projectile.owner];
        if (!player.active || player.dead) { Finish(Projectile.Center); return; }
        Record(player.Center);
        // 所有端都有同样的存活上限；只有拥有者读取本地技能阶段。
        if (age > MokouSacrificeStrikeSkill.MaxDashTicks
            || Projectile.owner == Main.myPlayer && !player.GetModPlayer<Mokou>().SacrificeStrikeSkill.IsTravelling)
        { Finish(player.Center); return; }
        if (Main.dedServ) return;
        MokouSkillFlameDust.GetColors(Projectile.owner, out Color dark, out Color light);
        Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        Lighting.AddLight(Projectile.Center, light.ToVector3() * 0.9f);
        if (age == 1)
        {
            for (int i = 0; i < 14; i++)
                MokouSkillFlameDust.Spawn(Projectile.Center, -forward.RotatedBy(Main.rand.NextFloat(-0.7f, 0.7f))
                    * Main.rand.NextFloat(2, 7), dark, light, Main.rand.NextFloat(0.8f, 1.4f));
        }
        for (int i = 0; i < 2; i++)
            MokouSkillFlameDust.Spawn(Vector2.Lerp(previousCenter, Projectile.Center, Main.rand.NextFloat()),
                -forward * 1.5f + Main.rand.NextVector2Circular(0.8f, 0.8f), dark, light, 0.9f);
    }

    private void Record(Vector2 center)
    {
        previousCenter = Projectile.Center;
        Projectile.Center = center;
        if (Vector2.DistanceSquared(previousCenter, center) > 96 * 96)
        { previousCenter = center; count = 0; }
        if (count == Capacity)
        {
            Array.Copy(points, 1, points, 0, Capacity - 1);
            Array.Copy(times, 1, times, 0, Capacity - 1);
            count--;
        }
        points[count] = center; times[count++] = age;
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        if (projHitbox.Intersects(targetHitbox)) return true;
        float point = 0;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            previousCenter, Projectile.Center, 32, ref point);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        MokouSkillFlameDust.GetColors(Projectile.owner, out Color dark, out Color light);
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        Vector2 side = new(-direction.Y, direction.X);
        for (int i = 1; i < count; i++)
        {
            float fade = Math.Clamp(1 - (age - times[i]) / (float)TrailTicks, 0, 1);
            float width = 20 * fade;
            SkillFlameDrawing.Stroke(points[i - 1], points[i], width, dark, light, fade * 0.65f);
            // 两侧破风细线和中间火焰主体，尾部留在经过的位置并迅速收细。
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector2 offset = side * sign * (11 + 4 * MathF.Sin(i * 0.65f));
                SkillFlameDrawing.Stroke(points[i - 1] + offset, points[i] + offset,
                    3 * fade, dark, light, fade * 0.8f);
            }
        }
        if (Projectile.ai[0] == 0)
        {
            // 身前短弧标出突进方向，不遮住整个角色。
            for (int i = 0; i < 12; i++)
            {
                Vector2 Point(float t) => Projectile.Center + direction * (30 - 14 * t * t) + side * t * 23;
                SkillFlameDrawing.Stroke(Point(-1 + i / 6f), Point(-1 + (i + 1) / 6f), 5, dark, light, 0.8f);
            }
        }
        return false;
    }
}
