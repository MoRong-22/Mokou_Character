using Microsoft.Xna.Framework;
using Mokou_Character.Skill;
using CharacterSkill = CharacterRely.Content.SkillSystem.Skill;

namespace Mokou_Character.Content.UI;

/// <summary>符卡展示资料独立于布局和技能逻辑；按技能 ID 查图，不按装配槽号绑定图形。</summary>
internal readonly record struct MokouSpellCardContent(int Column, int LifeCost)
{
    internal Rectangle Icon => new(Column * 128, 0, 128, 128);
    internal Rectangle Title => new(Column * 128, 128, 128, 64);

    // 第二行为“火爪、快晴、附体、舍身、重生”；完整名称仍由技能信息提供给提示框。
    internal static MokouSpellCardContent? For(CharacterSkill skill) => skill?.Id switch
    {
        MokouClawSkill.SkillId => new(0, MokouClawSkill.LifeCost),
        MokouKafuuKaiseyKickSkill.SkillId => new(1, MokouKafuuKaiseyKickSkill.LifeCost),
        MokouPhoenixPossessionSkill.SkillId => new(2, MokouPhoenixPossessionSkill.LifeCost),
        MokouSacrificeStrikeSkill.SkillId => new(3, MokouSacrificeStrikeSkill.LifeCost),
        MokouResurrectionSkill.SkillId => new(4, 0),
        _ => null
    };
}
