using CharacterRely.Content.SkillSystem;
using Terraria;
using Terraria.ID;

namespace Mokou_Character.Skill.Mokou_PassiveSkill;

public class Person_Of_Hourai : PassiveSkill
{
    public Person_Of_Hourai() : base(
        id: "PersonHatred_of_Hourai",
        name: "蓬莱人",
        description: "妹红喝下蓬莱药 获得了不老不死的能力\n出血与狂犬病的剩余时间每帧减半，加快减益消退\n独立回血：每秒恢复3点生命值，每拥有8点防御力额外恢复1点；不死鸟形态时翻倍\n独立回血不使用原版计数；保留原版回血，仅在人体自燃蓄满时暂停持续回血\n妹红无防御力 同时每次最多扣除最大生命值30%的血量") { }

    protected override void OnDisable() => ((Character.Mokou)Owner).ResetPassiveRecovery();

    protected override void ApplyEffects()
    {
        for (int i = 0; i < Player.buffType.Length; i++)
        {
            // 只缩短指定减益的剩余时间；buffType 是编号，不能用它计算时长。
            // 留下最后 1 tick，由原版 Buff 更新正常移除。
            if ((Player.buffType[i] == BuffID.Bleeding || Player.buffType[i] == BuffID.Rabies)
                && Player.buffTime[i] > 1)
                Player.buffTime[i] /= 2;
        }
    }
}
