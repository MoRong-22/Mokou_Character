using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;

namespace Mokou_Character.Character;

public partial class Mokou
{
    // 只在本次舍身的 KillMe 调用内有效，不能让普通死亡绕过复活冷却。
    private bool sacrificeRebirthPending;

    /// <summary>舍身击结算：走死亡钩子，再由 PreKill 接入现有重生，不产生真正死亡掉落。</summary>
    internal void CompleteSacrificeStrike()
    {
        if (Main.dedServ || Main.gameMenu || Player.whoAmI != Main.myPlayer
            || !IsSelected || !Player.active || Player.dead || IsNirvana || sacrificeRebirthPending) return;
        sacrificeRebirthPending = true;
        try
        {
            Player.KillMe(PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral(Player.name + "完成了不死身的舍身击。")),
                Math.Max(1, Player.statLife), 0);
        }
        finally
        {
            sacrificeRebirthPending = false;
            SyncCurrentLife();
        }
    }
}
