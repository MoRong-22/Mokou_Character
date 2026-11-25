using CharacterRely.Content.SkillSystem;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mokou_Character.Skill;
using Terraria;
using Terraria.GameInput;

namespace Mokou_Character.Character;

public partial class Mokou
{
    /// <summary>新的可选传送技能，需要先装配到四个技能槽之一。</summary>
    public MokouResurrectionSkill ResurrectionSkill => (MokouResurrectionSkill)GetSkill(MokouResurrectionSkill.SkillId);
    internal bool IsResurrecting => IsSelected && ResurrectionSkill.IsActive;
    internal MokouResurrectionFlameProjectile ResurrectionVisual;
    private ulong? lastResurrectionTeleport;

    /// <summary>只补充全屏地图输入，普通游戏输入仍由角色库派发。</summary>
    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        if (!Main.mapFullscreen || Main.dedServ || Main.gameMenu || Main.gamePaused || Main.blockInput
            || Main.drawingPlayerChat || Main.editSign || Main.editChest || Main.playerInventory
            || Player.whoAmI != Main.myPlayer || !Player.active || Player.dead
            || SkillKeybindSystem.GetInputCharacter(Player) != this) return;
        for (int slot = 0; slot < SkillKeybindSystem.SlotKeys.Count; slot++)
            if (SkillKeybindSystem.SlotKeys[slot].JustPressed && GetEquippedSkill(slot) is MokouResurrectionSkill)
                TryUseSkill(slot);
    }

    /// <summary>传送成功才消费全部自燃层数；直接恢复生命，不进入其他技能的强化结算。</summary>
    internal void ConsumeResurrectionStacks()
    {
        int amount = ResurrectionRules.Healing(TakeUnreservedCombustionLife());
        ResetPassiveRecovery();
        int restored = RestoreLifeDirect(amount);
        if (restored > 0) Player.HealEffect(restored);
        SyncCurrentLife();
    }

    internal bool CanAcceptResurrectionTeleport()
        => !lastResurrectionTeleport.HasValue
            || Main.GameUpdateCount - lastResurrectionTeleport.Value >= ResurrectionRules.CooldownTicks;

    internal void RecordResurrectionTeleport() => lastResurrectionTeleport = Main.GameUpdateCount;

    /// <summary>只在焚身/重现期间改变本体透明度，不关闭角色绘制开关，防止露出原版小人。</summary>
    private bool DrawResurrectionBody(GraphicsDevice device)
    {
        if (!IsSelected || !IsDrawEnabled || Main.dedServ || Player.dead || IsNirvana) return false;
        float opacity;
        if (IsResurrecting) opacity = ResurrectionSkill.BodyOpacity;
        else if (ResurrectionVisual?.Projectile.active == true && ResurrectionVisual.ControlsBody(Player))
            opacity = ResurrectionVisual.BodyOpacity;
        else return false;

        if (opacity >= 0.999f) base.DrawCharacter(device);
        else if (opacity > 0.001f)
        {
            // 火焰包裹时轮廓先烧暗；出场时随透明度恢复原色。
            Color tint = Color.Lerp(new Color(70, 30, 24), Color.White, opacity) * opacity;
            Animator.FlipX = Player.direction < 0;
            Animator.Draw(device, Player.Bottom - Main.screenPosition, scale: CharacterScale,
                color: tint, viewMatrix: Main.GameViewMatrix.TransformationMatrix);
        }
        return true;
    }
}
