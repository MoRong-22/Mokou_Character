using Mokou_Character.Content.Mokou_Buff;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Mokou_Character.Character;

public partial class Mokou
{
    /// <summary>攻击附加的咒火、暗影焰持续时间；120 tick 为 2 秒，重复命中刷新时间。</summary>
    public const int InflictedFlameTicks = 2 * 60;

    /// <summary>通用命中回调覆盖武器和所属弹幕，包括技能；只为当前选择妹红的玩家附加火焰。</summary>
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        base.OnHitNPC(target, hit, damageDone);
        if (!IsSelected || damageDone <= 0 || target.friendly || target.immortal) return;

        // 妖术-火焰会把受到的减益转换成灌注，因此两种状态都要识别。
        if (Player.HasBuff(BuffID.CursedInferno) || Player.HasBuff(BuffID.WeaponImbueCursedFlames))
            target.AddBuff(BuffID.CursedInferno, InflictedFlameTicks);

        // 两项独立判断：同时持有时，两个减益都能传递，仍遵守敌怪自身的减益免疫。
        if (Player.HasBuff(BuffID.ShadowFlame) || Player.HasBuff(ModContent.BuffType<WeaponImbueShadowFlame>()))
            target.AddBuff(BuffID.ShadowFlame, InflictedFlameTicks);
    }
}
