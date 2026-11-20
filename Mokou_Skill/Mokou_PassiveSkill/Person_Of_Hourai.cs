using CharacterRely.Content.SkillSystem;
using Terraria;
using Terraria.ID;

namespace Mokou_Character.Skill.Mokou_PassiveSkill;

public class Person_Of_Hourai : PassiveSkill
{
    public Person_Of_Hourai() : base(
        id: "PersonHatred_of_Hourai",
        name: "蓬莱人",
        description: "妹红喝下蓬莱药 获得了不老不死的能力\n对部分buff有极快速的恢复效果\n独立回血：每秒恢复3点生命值，每拥有8点防御力额外恢复1点；不死鸟形态时翻倍\n独立回血不使用原版计数；保留原版回血，仅在人体自燃蓄满时暂停持续回血\n妹红无防御力 同时每次最多扣除最大生命值30%的血量") { }

    protected override void OnDisable() => ((Character.Mokou)Owner).ResetPassiveRecovery();

    protected override void ApplyEffects()
    {
        for (int i = 0; i < Player.buffType.Length; i++)
        {
            if(Player.buffType[i] == BuffID.Bleeding)
                Player.buffType[i] /= 2;
            if(Player.buffType[i] == BuffID.Rabies)
                Player.buffType[i] /= 2;
        }
    }
}
