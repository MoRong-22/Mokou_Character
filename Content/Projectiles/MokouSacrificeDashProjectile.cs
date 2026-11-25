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

/// <summary>冲刺身体碰撞和连续焰尾；路径固定在世界坐标中，每段仅命中一次。</summary>
public sealed class MokouSacrificeDashProjectile : ModProjectile
{
    private const int TrailTicks = 12, Capacity = 32;
    private readonly Vector2[] points = new Vector2[Capacity];
    private readonly Vector2[] normals = new Vector2[Capacity];
    private readonly float[] times = new float[Capacity];
    private readonly float[] distances = new float[Capacity];
    private readonly SacrificeFlameDrawing drawing = new();
    private FlameRibbonMesh mesh;
    private Color dark, light;
    private int start, count, age, stoppedTicks;
    private Vector2 previousCenter;
    public override string Texture => "Terraria/Images/MagicPixel";

    public override void SetStaticDefaults()
        => ProjectileID.Sets.DrawScreenCheckFluff[Type] = (int)MokouSacrificeStrikeSkill.MaxDashDistance;

    public override void SetDefaults()
    {
        Projectile.width = 32; Projectile.height = 48;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.timeLeft = MokouSacrificeStrikeSkill.MaxDashTicks + TrailTicks + 2;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override void OnSpawn(IEntitySource source)
    {
        previousCenter = Projectile.Center;
        MokouSkillFlameDust.GetColors(Projectile.owner, out dark, out light);
        Record(Projectile.Center);
    }
    public override void SendExtraAI(BinaryWriter writer) { writer.Write(dark.PackedValue); writer.Write(light.PackedValue); }
    public override void ReceiveExtraAI(BinaryReader reader)
    {
        dark = new Color { PackedValue = reader.ReadUInt32() };
        light = new Color { PackedValue = reader.ReadUInt32() };
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
        // 只有拥有者读取本地技能阶段；其他客户端使用同步状态和寿命上限。
        if (age > MokouSacrificeStrikeSkill.MaxDashTicks
            || Projectile.owner == Main.myPlayer && !player.GetModPlayer<Mokou>().SacrificeStrikeSkill.IsTravelling)
        { Finish(player.Center); return; }
        if (!Main.dedServ) Lighting.AddLight(Projectile.Center, light.ToVector3() * 0.7f);
    }

    private void Record(Vector2 center)
    {
        previousCenter = Projectile.Center;
        Projectile.Center = center;
        if (Vector2.DistanceSquared(previousCenter, center) > 96 * 96)
        { previousCenter = center; start = count = 0; }
        float distance = 0;
        if (count > 0)
        {
            int last = (start + count - 1) % Capacity;
            float step = Vector2.Distance(points[last], center);
            if (step < 0.01f) return;
            distance = distances[last] + step;
        }
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        int index = (start + count) % Capacity;
        points[index] = center;
        normals[index] = new Vector2(-direction.Y, direction.X);
        times[index] = age;
        distances[index] = distance;
        if (count < Capacity) count++;
        else start = (start + 1) % Capacity;
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
        mesh ??= new FlameRibbonMesh(Capacity);
        int vertices = mesh.Build(points, normals, times, distances, start, count, age, TrailTicks, 16f, Main.screenPosition);
        drawing.Ribbon(mesh.Vertices, vertices, age / 60f, dark, light);
        return false;
    }
}