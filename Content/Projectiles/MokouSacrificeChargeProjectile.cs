using System.IO;
using Microsoft.Xna.Framework;
using Mokou_Character.Character;
using Mokou_Character.Content.Dusts;
using Mokou_Character.SkillVisuals;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Mokou_Character.Skill;

/// <summary>舍身击一秒蓄力的收拢焰弧；只有视觉，没有伤害，ai[2]对应本次技能编号。</summary>
public sealed class MokouSacrificeChargeProjectile : ModProjectile
{
    private readonly SacrificeFlameDrawing drawing = new();
    private Color dark, light;
    private int age;
    public override string Texture => "Terraria/Images/MagicPixel";
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 100;
        Projectile.timeLeft = MokouSacrificeStrikeSkill.WindupTicks + 1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
    }
    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => false;
    public override void OnSpawn(IEntitySource source)
        => MokouSkillFlameDust.GetColors(Projectile.owner, out dark, out light);
    public override void SendExtraAI(BinaryWriter writer) { writer.Write(dark.PackedValue); writer.Write(light.PackedValue); }
    public override void ReceiveExtraAI(BinaryReader reader)
    {
        dark = new Color { PackedValue = reader.ReadUInt32() };
        light = new Color { PackedValue = reader.ReadUInt32() };
    }
    public override void AI()
    {
        if ((uint)Projectile.owner >= Main.maxPlayers) { Projectile.Kill(); return; }
        Player player = Main.player[Projectile.owner];
        if (!player.active || player.dead) { Projectile.Kill(); return; }
        if (Projectile.owner == Main.myPlayer)
        {
            var skill = player.GetModPlayer<Mokou>().SacrificeStrikeSkill;
            if (!skill.IsCharging || skill.CastSerial != (int)Projectile.ai[2]) { Projectile.Kill(); return; }
        }
        Projectile.Center = player.Center;
        age++;
        if (!Main.dedServ) Lighting.AddLight(Projectile.Center, light.ToVector3() * (0.15f + age / 100f));
    }
    public override bool PreDraw(ref Color lightColor)
    {
        float progress = MathHelper.Clamp(age / (float)MokouSacrificeStrikeSkill.WindupTicks, 0f, 1f);
        drawing.Sprite("Charge", Projectile.Center, new Vector2(100f, 108f), new Vector2(0.5f),
            Projectile.velocity.ToRotation(), age / 60f, progress, dark, light);
        return false;
    }
}
