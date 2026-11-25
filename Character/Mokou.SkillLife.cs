using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Mokou_Character.Skill;
using Terraria;
using Terraria.ModLoader;
using CharacterSkill = CharacterRely.Content.SkillSystem.Skill;

namespace Mokou_Character.Character;

public partial class Mokou
{
    // 每个技能单独记账；不能让后释放的火爪结算仍留在场上的火柱。
    private sealed record SkillLifeUse(object Key, int Cost, bool Empowered, Color Dark, Color Light);
    private Dictionary<object, SkillLifeUse> skillLifeUses;
    private SkillLifeUse startingLifeUse;
    private bool startingSkillLifeUse;
    // 强化还在场上时，普通技能先结算到下一轮，避免被强化结束时的清零吞掉。
    private float nextCombustionLife;

    public override bool UsesCost => false;
    public const float SpontaneousCombustionLifeRequired = 300f;
    public const float EmpoweredSkillDamageMultiplier = 1.5f;
    public const int EmpoweredSkillHealing = 100;

    public static int GetEmpoweredSkillDamage(int damage, bool empowered)
        => empowered ? (int)MathF.Round(damage * EmpoweredSkillDamageMultiplier) : damage;

    public float SpontaneousCombustionProgress
        => MathHelper.Clamp(Jintai_Shizen_Hakka_Life / SpontaneousCombustionLifeRequired, 0f, 1f);

    /// <summary>仅动作阶段互斥。未接入动作接口的技能保守地占用其完整持续时间。</summary>
    internal bool HasBlockingSkillAction(CharacterSkill requester)
    {
        foreach (var skill in Skills)
            if (!ReferenceEquals(skill, requester) && skill.IsActive
                && (skill is not IMokouActionSkill action || action.IsPerformingAction)) return true;
        return false;
    }

    private SkillLifeUse ReservedEmpowerment
    {
        get
        {
            if (skillLifeUses != null)
                foreach (var use in skillLifeUses.Values) if (use.Empowered) return use;
            return null;
        }
    }

    // 新动作优先显示自己的焰色；没有动作时保留尚未结束的强化技能颜色。
    private SkillLifeUse DisplayLifeUse
    {
        get
        {
            if (startingLifeUse != null) return startingLifeUse;
            SkillLifeUse latest = null, empowered = null;
            if (skillLifeUses != null)
                foreach (var use in skillLifeUses.Values)
                {
                    if (use.Key is CharacterSkill skill && skill.IsActive
                        && (skill is not IMokouActionSkill action || action.IsPerformingAction)) return use;
                    latest = use;
                    if (use.Empowered) empowered = use;
                }
            return empowered ?? latest;
        }
    }

    private Color? lockedSkillDarkColor => DisplayLifeUse?.Dark;
    private Color? lockedSkillLightColor => DisplayLifeUse?.Light;

    /// <summary>生命消耗不能致死；只检查，不扣血。</summary>
    public bool CanSpendSkillLife(int amount)
        => amount >= 0 && IsSelected && Player.active && !Player.dead && Player.statLife > amount;

    /// <summary>为指定技能独立记录消耗、强化与焰色。起手成功立即扣血，结束时按同一技能结算。</summary>
    internal bool TryUseSkillWithLifeCost(CharacterSkill skill, int amount, Func<bool, bool> startSkill)
        => ReferenceEquals(skill?.Owner, this) && TryBeginSkillLifeUse(skill, amount, startSkill);

    /// <summary>兼容旧入口；没有技能标识时只允许独占释放，防止误结算其他技能。</summary>
    public bool TryUseSkillWithLifeCost(int amount, Func<bool, bool> startSkill)
        => (skillLifeUses == null || skillLifeUses.Count == 0) && TryBeginSkillLifeUse(this, amount, startSkill);

    private bool TryBeginSkillLifeUse(object key, int amount, Func<bool, bool> startSkill)
    {
        ArgumentNullException.ThrowIfNull(startSkill);
        if (Main.dedServ || Player.whoAmI != Main.myPlayer || startingSkillLifeUse
            || skillLifeUses?.ContainsKey(key) == true || (key != this && skillLifeUses?.ContainsKey(this) == true)
            || IsResurrecting || !CanSpendSkillLife(amount)) return false;
        // 一次蓄满只归属第一个成功起手的技能，脱手期间释放别的技能不能重复领取强化。
        bool empowered = Jintai_Shizen_Hakka_Active && ReservedEmpowerment == null;
        startingLifeUse = new(key, amount, empowered, GetUnlatchedDarkColor(empowered), GetUnlatchedLightColor(empowered));
        startingSkillLifeUse = true;
        try
        {
            if (!startSkill(empowered)) return false;
            (skillLifeUses ??= new())[key] = startingLifeUse;
            Player.statLife -= amount;
            SyncCurrentLife();
            return true;
        }
        finally { startingLifeUse = null; startingSkillLifeUse = false; }
    }

    /// <summary>连段追加扣血仅累计到发起连段的技能，保持该技能最初的强化和颜色。</summary>
    internal bool TryContinueSkillWithLifeCost(CharacterSkill skill, int amount, Func<bool> continueSkill)
        => TryContinueSkillLifeUse(skill, amount, continueSkill);

    internal bool TryContinueSkillWithLifeCost(int amount, Func<bool> continueSkill)
        => TryContinueSkillLifeUse(this, amount, continueSkill);

    private bool TryContinueSkillLifeUse(object key, int amount, Func<bool> continueSkill)
    {
        ArgumentNullException.ThrowIfNull(continueSkill);
        if (Main.dedServ || Player.whoAmI != Main.myPlayer || startingSkillLifeUse
            || skillLifeUses == null || !skillLifeUses.TryGetValue(key, out var use)
            || IsResurrecting || !CanSpendSkillLife(amount)) return false;
        startingSkillLifeUse = true;
        startingLifeUse = use;
        try
        {
            if (!continueSkill()) return false;
            skillLifeUses[key] = use with { Cost = use.Cost + amount };
            Player.statLife -= amount;
            SyncCurrentLife();
            return true;
        }
        finally { startingLifeUse = null; startingSkillLifeUse = false; }
    }

    public bool TrySpendSkillLife(int amount) => TrySpendSkillLife(amount, out _);
    public bool TrySpendSkillLife(int amount, out bool empowered)
    {
        bool snapshot = false;
        bool success = TryUseSkillWithLifeCost(amount, value => { snapshot = value; return true; });
        empowered = snapshot;
        return success;
    }

    /// <summary>只结算指定技能，包含其脱手弹幕；重复结束无效，死亡时不恢复生命。</summary>
    internal void CompleteSkillLifeUse(CharacterSkill skill) => CompleteSkillLifeUseCore(skill);
    public void CompleteSkillLifeUse() => CompleteSkillLifeUseCore(this);

    private void CompleteSkillLifeUseCore(object key)
    {
        if (startingSkillLifeUse || skillLifeUses == null || !skillLifeUses.Remove(key, out var use)) return;
        if (!Player.active || Player.dead || Player.statLife <= 0)
        {
            if (skillLifeUses.Count == 0) nextCombustionLife = 0f;
            return;
        }
        if (use.Empowered)
        {
            Jintai_Shizen_Hakka_Life = MathHelper.Clamp(nextCombustionLife + use.Cost, 0f, SpontaneousCombustionLifeRequired);
            nextCombustionLife = 0f;
            RestoreLifeDirect(EmpoweredSkillHealing);
        }
        else if (ReservedEmpowerment != null)
            nextCombustionLife = MathHelper.Clamp(nextCombustionLife + use.Cost, 0f, SpontaneousCombustionLifeRequired);
        else
            Jintai_Shizen_Hakka_Life = MathHelper.Clamp(Jintai_Shizen_Hakka_Life + use.Cost, 0f, SpontaneousCombustionLifeRequired);
        if (use.Empowered || Jintai_Shizen_Hakka_Active) ResetPassiveRecovery();
        SyncCurrentLife();
    }

    /// <summary>传送只消费未被已释放技能占用的进度，不能把同一份强化同时用于传送回血。</summary>
    private float TakeUnreservedCombustionLife()
    {
        if (ReservedEmpowerment != null)
        {
            float available = nextCombustionLife;
            nextCombustionLife = 0f;
            return available;
        }
        float current = Jintai_Shizen_Hakka_Life;
        Jintai_Shizen_Hakka_Life = 0f;
        return current;
    }

    /// <summary>网络/存档克隆不能与真实玩家共享未完成的技能账本。</summary>
    public override ModPlayer Clone(Player newEntity)
    {
        var clone = (Mokou)base.Clone(newEntity);
        clone.skillLifeUses = null;
        clone.startingLifeUse = null;
        clone.startingSkillLifeUse = false;
        clone.nextCombustionLife = 0f;
        return clone;
    }
}
