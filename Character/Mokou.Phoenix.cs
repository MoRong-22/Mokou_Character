using System;
using Mokou_Character.Content.Mokou_Buff;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Mokou_Character.Character;

public partial class Mokou
{
    /// <summary>主动吟唱和被动重生共用 30 秒形态，60 tick 为一秒。</summary>
    public const int PhoenixFormDuration = 30 * 60;

    /// <summary>是否拥有有效的不死鸟形态；Buff 消失或角色停用后立即停止加成。</summary>
    public bool IsPhoenixForm => IsSelected && !Player.dead
        && Player.HasBuff(ModContent.BuffType<PhoenixFormBuff>());

    /// <summary>进入不死鸟形态；重复进入刷新到 30 秒，不叠加恢复倍率。</summary>
    public void EnterPhoenixForm()
    {
        if (!IsSelected || Player.dead) return;
        Player.AddBuff(ModContent.BuffType<PhoenixFormBuff>(), PhoenixFormDuration);
    }

    private void ApplyPhoenixFlight()
    {
        if (!IsPhoenixForm) return;
        // 未装备翅膀时临时提供烈焰之翼的飞行能力，不生成或占用装备物品。
        // 已装备翅膀保留自身性能。每帧补满，Buff 结束后由原版 ResetEffects 恢复。
        if (Player.wingsLogic == 0)
        {
            Player.wingsLogic = ArmorIDs.Wing.FlameWings;
            Player.wingTimeMax = Player.GetWingStats(Player.wingsLogic).FlyTime;
        }
        Player.wingTime = Math.Max(1, Player.wingTimeMax);
        Player.noFallDmg = true;
    }

    /// <summary>在位移前锁住吟唱和重生的位置，避免重力或击退把角色带离火焰。</summary>
    public override void PreUpdateMovement()
    {
        if (IsNirvana)
        {
            Player.position = nirvanaPosition;
            Player.velocity = Microsoft.Xna.Framework.Vector2.Zero;
        }
        else if (IsPhoenixChanting)
            PhoenixPossessionSkill.HoldPosition();
        else if (IsResurrecting)
            ResurrectionSkill.HoldPosition();
        else if (IsSelected)
            SacrificeStrikeSkill.ApplyDashMovement();
    }

    /// <summary>常态隐藏两片翅膀；形态结束时立即恢复隐藏。</summary>
    private void UpdatePhoenixWings()
    {
        bool visible = IsPhoenixForm;
        SetPhoenixWing("phoenix-wing-far", visible);
        SetPhoenixWing("phoenix-wing-near", visible);
    }

    private void SetPhoenixWing(string slotName, bool visible)
    {
        var slot = Animator.Skeleton.FindSlot(slotName);
        if (slot == null) return;
        // 模型的默认 Alpha 为零，使选人界面也默认隐藏翅膀；游戏内由形态显式控制。
        slot.A = visible ? 1f : 0f;
        Animator.SetSlotVisible(slotName, visible);
    }

    public override void UpdateDead()
    {
        base.UpdateDead();
        nirvanaTimeLeft = 0;
        ResetPassiveRecovery();
        Player.ClearBuff(ModContent.BuffType<PhoenixFormBuff>());
    }
}
