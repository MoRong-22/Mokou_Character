using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace Mokou_Character.Character;

public partial class Mokou
{
    // 起手成功立即扣血并锁定颜色；结束时消费旧强化，并把本次已扣生命计入新进度。
    private bool startingSkillLifeUse, pendingSkillLifeUse, pendingSkillEmpowered;
    private int pendingSkillLifeCost;
    private Color? lockedSkillDarkColor, lockedSkillLightColor;

    /// <summary>妹红技能使用生命值；关闭角色库的 Cost 扣除、恢复与显示。</summary>
    public override bool UsesCost => false;

    /// <summary>沿用人体自燃的 300 点生命阈值，UI 和可用判定共用此值。</summary>
    public const float SpontaneousCombustionLifeRequired = 300f;

    /// <summary>蓄满后下一次技能的整套伤害倍率；各技能在起手时保存强化结果。</summary>
    public const float EmpoweredSkillDamageMultiplier = 1.5f;
    /// <summary>强化技能结束结算时恢复的生命，最多恢复到生命上限。</summary>
    public const int EmpoweredSkillHealing = 100;

    public static int GetEmpoweredSkillDamage(int damage, bool empowered)
        => empowered ? (int)System.MathF.Round(damage * EmpoweredSkillDamageMultiplier) : damage;

    /// <summary>人体自燃进度，0 为未积累，1 为已蓄满；UI 显示最多 100%。</summary>
    public float SpontaneousCombustionProgress
        => MathHelper.Clamp(Jintai_Shizen_Hakka_Life / SpontaneousCombustionLifeRequired, 0f, 1f);

    /// <summary>生命消耗不能致死，释放后至少保留 1 点生命；0 表示不耗血的技能。仅检查，不扣血。</summary>
    public bool CanSpendSkillLife(int amount)
        => amount >= 0 && IsSelected && Player.active && !Player.dead && Player.statLife > amount;

    /// <summary>
    /// 起手成功立即扣血，并锁定强化、颜色和已扣生命；自燃进度留到整套结束结算。
    /// startSkill 接收本次是否强化，只负责起手，不自行扣血或修改自燃进度。
    /// 起手失败不结算；成功后须在整套技能 OnEnd 调用 CompleteSkillLifeUse，且只调用一次。
    /// </summary>
    public bool TryUseSkillWithLifeCost(int amount, Func<bool, bool> startSkill)
    {
        ArgumentNullException.ThrowIfNull(startSkill);
        if (Main.dedServ || Player.whoAmI != Main.myPlayer || startingSkillLifeUse || pendingSkillLifeUse
            || !CanSpendSkillLife(amount)) return false;
        bool empowered = Jintai_Shizen_Hakka_Active;
        Color dark = fire_darkColor, light = fire_lightColor;
        startingSkillLifeUse = true;
        try
        {
            if (!startSkill(empowered)) return false;
            pendingSkillLifeUse = true;
            pendingSkillLifeCost = amount;
            pendingSkillEmpowered = empowered;
            lockedSkillDarkColor = dark;
            lockedSkillLightColor = light;
            Player.statLife -= amount;
            SyncCurrentLife();
            return true;
        }
        finally { startingSkillLifeUse = false; }
    }

    /// <summary>
    /// 已开始的同一套技能追加一段：成功后立即扣血，沿用起手强化与颜色，合并已扣生命。
    /// continueSkill 只负责开启下一段；失败不扣血，全部段落结束后仍只结算一次自燃与回血。
    /// </summary>
    internal bool TryContinueSkillWithLifeCost(int amount, Func<bool> continueSkill)
    {
        ArgumentNullException.ThrowIfNull(continueSkill);
        if (Main.dedServ || Player.whoAmI != Main.myPlayer || startingSkillLifeUse || !pendingSkillLifeUse
            || !CanSpendSkillLife(amount)) return false;
        startingSkillLifeUse = true;
        try
        {
            if (!continueSkill()) return false;
            pendingSkillLifeCost += amount;
            Player.statLife -= amount;
            SyncCurrentLife();
            return true;
        }
        finally { startingSkillLifeUse = false; }
    }

    /// <summary>
    /// 兼容原方法名，立即扣血；调用方必须在整套结束时调用 CompleteSkillLifeUse 结算自燃。
    /// </summary>
    public bool TrySpendSkillLife(int amount) => TrySpendSkillLife(amount, out _);

    /// <summary>
    /// 兼容入口：立即扣血并返回释放前的强化状态，回血与自燃累计在结束时执行。
    /// </summary>
    public bool TrySpendSkillLife(int amount, out bool empowered)
    {
        bool snapshot = false;
        bool success = TryUseSkillWithLifeCost(amount, value => { snapshot = value; return true; });
        empowered = snapshot;
        return success;
    }

    /// <summary>
    /// 整套技能（包含延迟爆炸）结束后结算自燃与强化回血，不再扣血；重复调用无效。
    /// 已开始后被打断也在 OnEnd 结算；死亡时只清理锁定，不让这次回血复活玩家。
    /// </summary>
    public void CompleteSkillLifeUse()
    {
        if (!pendingSkillLifeUse || startingSkillLifeUse) return;
        int amount = pendingSkillLifeCost;
        bool empowered = pendingSkillEmpowered;
        pendingSkillLifeUse = false;
        pendingSkillLifeCost = 0;
        pendingSkillEmpowered = false;
        lockedSkillDarkColor = lockedSkillLightColor = null;
        if (!Player.active || Player.dead || Player.statLife <= 0) return;
        // 按起手时已扣除的生命累计，期间受伤或回血不会改变本次消耗记录。
        if (empowered)
        {
            Jintai_Shizen_Hakka_Life = 0f;
            RestoreLifeDirect(EmpoweredSkillHealing);
        }
        Jintai_Shizen_Hakka_Life = MathHelper.Clamp(
            Jintai_Shizen_Hakka_Life + amount, 0f, SpontaneousCombustionLifeRequired);
        if (empowered || Jintai_Shizen_Hakka_Active) ResetPassiveRecovery();
        SyncCurrentLife();
    }
}
