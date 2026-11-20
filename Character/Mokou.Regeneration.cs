using System;
using Terraria;
using Terraria.ID;

namespace Mokou_Character.Character;

public partial class Mokou
{
    /// <summary>独立回血的基础速率，单位为生命/秒；不使用原版 lifeRegen 计数。</summary>
    public const int BasePassiveRecoveryPerSecond = 3;
    /// <summary>防御清零前，每满 8 点防御额外恢复 1 生命/秒。</summary>
    public const int DefensePerRecoveryPoint = 8;

    private int passiveRecoveryPerSecond = BasePassiveRecoveryPerSecond;
    private int passiveRecoveryUnits;

    private void CapturePassiveRecoveryRate()
        => passiveRecoveryPerSecond = BasePassiveRecoveryPerSecond
            + Math.Max(0, (int)Player.statDefense) / DefensePerRecoveryPoint;

    /// <summary>由玩家 UpdateLifeRegen 每帧推进，直接恢复生命，不使用原版回血计数或被动调度。</summary>
    internal void UpdatePassiveRecovery()
    {
        if (Main.dedServ || Player.whoAmI != Main.myPlayer || Main.gameMenu || Main.gamePaused) return;
        if (!IsSelected || !Player.active || Player.dead || Player.statLife <= 0 || IsNirvana
            || Jintai_Shizen_Hakka_Active || Player.statLife >= Player.statLifeMax2)
        {
            ResetPassiveRecovery();
            return;
        }

        passiveRecoveryUnits += passiveRecoveryPerSecond * (IsPhoenixForm ? 2 : 1);
        int amount = passiveRecoveryUnits / 60;
        passiveRecoveryUnits %= 60;
        if (amount > 0 && RestoreLifeDirect(amount) > 0) SyncCurrentLife();
    }

    /// <summary>停用、死亡、满血或人体自燃蓄满时清空余数，不积攒暂停期间的回血。</summary>
    internal void ResetPassiveRecovery() => passiveRecoveryUnits = 0;

    /// <summary>固定在玩家回血阶段推进独立回血；只有自燃蓄满期间阻止原版正恢复。</summary>
    public override void UpdateLifeRegen()
    {
        if (!IsSelected) return;
        UpdatePassiveRecovery();
        if (!Jintai_Shizen_Hakka_Active) return;
        if (Player.lifeRegen > 0) Player.lifeRegen = 0;
        if (Player.lifeRegenCount > 0) Player.lifeRegenCount = 0;
    }

    /// <summary>自燃蓄满时同时暂停自然恢复；消耗强化后保留原版装备、Buff 和自然回血。</summary>
    public override void NaturalLifeRegen(ref float regen)
    {
        if (!IsSelected || !Jintai_Shizen_Hakka_Active) return;
        regen = 0f;
        if (Player.lifeRegen > 0) Player.lifeRegen = 0;
        if (Player.lifeRegenCount > 0) Player.lifeRegenCount = 0;
    }

    private int RestoreLifeDirect(int amount)
    {
        int restored = Math.Min(Math.Max(0, amount), Math.Max(0, Player.statLifeMax2 - Player.statLife));
        Player.statLife += restored;
        return restored;
    }

    private void SyncCurrentLife()                       
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            NetMessage.SendData(MessageID.PlayerLifeMana, number: Player.whoAmI);
    }
}
