namespace Mokou_Character.Skill;

/// <summary>技能当前是否占用玩家动作。脱手弹幕仍可保持 IsActive，但不会挡住其他技能起手。</summary>
public interface IMokouActionSkill
{
    bool IsPerformingAction { get; }
}
