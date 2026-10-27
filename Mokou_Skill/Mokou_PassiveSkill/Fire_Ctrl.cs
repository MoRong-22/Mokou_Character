using CharacterRely.Content.SkillSystem;
using Terraria.ID;

namespace Mokou_Character.Skill.Mokou_PassiveSkill;

public class Fire_Ctrl : PassiveSkill
{
    public Fire_Ctrl() : base(id:"Fire_Ctrl",name:"操控火焰",
        "妹红拥有操控火焰程度的能力\n 妹红免疫点燃 冻伤 咒火以及暗影焰\n" +
        " 并且再喝下对应增益瓶的时候 妹红的能力会得到变化"){}
    protected override void ApplyEffects()
    {
        if(Player.HasBuff(BuffID.ShadowFlame))
            Player.ClearBuff(BuffID.ShadowFlame);
        if(Player.HasBuff(BuffID.CursedInferno))
            Player.ClearBuff(BuffID.CursedInferno);
        if(Player.HasBuff(BuffID.OnFire))
            Player.ClearBuff(BuffID.OnFire);
        if(Player.HasBuff(BuffID.Frostburn))
            Player.ClearBuff(BuffID.Frostburn);
        if(Player.HasBuff(BuffID.Frostburn2))
            Player.ClearBuff(BuffID.Frostburn2);
        if(Player.HasBuff(BuffID.Burning))
            Player.ClearBuff(BuffID.Burning);
        if(Player.HasBuff(BuffID.OnFire3))
            Player.ClearBuff(BuffID.OnFire3);
    }
}