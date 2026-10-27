using CharacterRely.Content.SkillSystem;

namespace Mokou_Character.Skill.Mokou_PassiveSkill;

public class Nirvana : PassiveSkill
{
    public Nirvana() : base(
        id: "Nirvana",name:"涅槃",description:"妹红在死亡的时候 会进入涅槃状态 状态结束会满血复活" +
                                            "\n复活以后会对敌人打上仇恨标志 被仇恨的人"){}
}