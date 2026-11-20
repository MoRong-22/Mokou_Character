using Microsoft.Xna.Framework;
using Mokou_Character.Character;
using Terraria;
using Terraria.ModLoader;

namespace Mokou_Character.Content.Dusts;

/// <summary>技能火焰使用灰度贴图着色，避免原版火把粒子的橙色覆盖当前灌注焰色。</summary>
public sealed class MokouSkillFlameDust : ModDust
{
    public static void GetColors(int owner, out Color dark, out Color light)
    {
        dark = new Color(166, 15, 3);
        light = new Color(255, 184, 51);
        if ((uint)owner >= (uint)Main.maxPlayers) return;
        var character = Main.player[owner].GetModPlayer<Mokou>();
        dark = character.fire_darkColor;
        light = character.fire_lightColor;
    }

    public static void Spawn(Vector2 position, Vector2 velocity, Color dark, Color light, float scale)
    {
        if (Main.dedServ) return;
        Dust.NewDustPerfect(position, ModContent.DustType<MokouSkillFlameDust>(), velocity,
            newColor: Color.Lerp(dark, light, Main.rand.NextFloat(0.35f, 1f)), Scale: scale);
    }

    public override void OnSpawn(Dust dust)
    {
        dust.frame = new Rectangle(0, 0, 16, 24);
        dust.noGravity = true;
        dust.noLight = true;
        dust.fadeIn = 0f;
    }

    public override Color? GetAlpha(Dust dust, Color lightColor)
        => dust.color * MathHelper.Clamp((30f - dust.fadeIn) / 10f, 0f, 1f);

    public override bool Update(Dust dust)
    {
        dust.position += dust.velocity;
        dust.velocity *= 0.96f;
        dust.velocity.Y -= 0.035f;
        dust.rotation += dust.velocity.X * 0.025f;
        dust.scale *= 0.97f;
        if (++dust.fadeIn >= 30f) dust.active = false;
        return false;
    }
}
