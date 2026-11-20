using CharacterRely.Content.SkillSystem;

namespace Mokou_Character.Skill.Mokou_PassiveSkill;

public class Jintai_Shizen_Hakka: PassiveSkill
{
    public Jintai_Shizen_Hakka() : base(id:"Jintai_Shizen_Hakka",name: "人体自燃",description:"妹红将都市传说化为自己的力量\n在其使用技能时损失的生命化为'人体自燃'层数\n" +
        "技能成功释放时立即扣除生命，整套结束后累计自燃进度；累计300点生命后蓄满，并停止持续回血\n下一次成功释放的技能整套伤害提高50%，期间保持强化焰色\n强化技能结束后清除旧进度并恢复100点生命（不超过上限），起手已扣生命计入下一轮"){}
}
