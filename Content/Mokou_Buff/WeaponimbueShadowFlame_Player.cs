
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Mokou_Character.Content.Mokou_Buff;

public class WeaponimbueShadowFlame_Player : ModPlayer
{
    // 是否拥有你制作的暗影焰灌注 Buff。
    private bool HasShadowFlameImbue =>
        Player.HasBuff(ModContent.BuffType<WeaponImbueShadowFlame>());

// 普通近战武器：在当前攻击范围内生成粒子。
    public override void MeleeEffects(Item item, Rectangle hitbox)
    {
        if (!HasShadowFlameImbue || !item.CountsAsClass(DamageClass.Melee))
            return;

        SpawnImbueDust(
            new Vector2(hitbox.X, hitbox.Y),
            hitbox.Width, hitbox.Height,
            Player.velocity, Player.direction);
    }

// 近战弹幕、鞭子：在弹幕提供的区域生成粒子。
    public override void EmitEnchantmentVisualsAt(
        Projectile projectile, Vector2 boxPosition, int boxWidth, int boxHeight)
    {
        if (!HasShadowFlameImbue ||
            projectile.noEnchantments ||
            !projectile.friendly ||
            projectile.hostile)
            return;

        if (!projectile.CountsAsClass(DamageClass.Melee) &&
            !ProjectileID.Sets.IsAWhip[projectile.type])
            return;

        SpawnImbueDust(
            boxPosition, boxWidth, boxHeight,
            projectile.velocity, projectile.direction);
    }
    private static void SpawnImbueDust(
        Vector2 position, int width, int height,
        Vector2 velocity, int direction)
    {
        if (Main.dedServ || !Main.rand.NextBool(2))
            return;

        Dust dust = Dust.NewDustDirect(
            position, width, height,
            DustID.Shadowflame, 
            velocity.X * 0.2f + direction * 3f,
            velocity.Y * 0.2f,
            100, default, 2.5f);

        dust.noGravity = true;
        dust.velocity *= 0.7f;
        dust.velocity.Y -= 0.5f;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        if(Player.HasBuff(ModContent.BuffType<WeaponImbueShadowFlame>()))
            target.AddBuff(BuffID.ShadowFlame,120);
    }

}