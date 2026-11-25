using CharacterRely.Content.SkillSystem;

namespace Mokou_Character.Skill.Mokou_PassiveSkill;

public class Nirvana : PassiveSkill
{
    /// <summary>致命伤触发重生后，按游戏更新帧计时；60 tick 为 1 秒。</summary>
    internal const int CooldownMinutes = 5;
    internal const int CooldownTicks = CooldownMinutes * 60 * 60;
    public Nirvana() : base(
        id: "Nirvana", name: "不死鸟重生",
        description: "受到致命伤害时保留 1 点生命，原地重生 6 秒，期间无法行动且不会死亡。" +
                     "\n重生结束后恢复全部生命，并获得 30 秒不死鸟形态：独立回血翻倍，飞行时间无限，展开火焰翅膀。" +
                     $"\n重生冷却为 {CooldownMinutes} 分钟，从触发时开始计时。") { }
}
