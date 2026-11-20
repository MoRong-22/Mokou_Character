using CharacterRely.Content.SkillSystem;
using Mokou_Character.Content.Mokou_Buff;
using Terraria.ID;
using Terraria.ModLoader;

namespace Mokou_Character.Skill.Mokou_PassiveSkill;

public class Fire_Ctrl : PassiveSkill
{
    public Fire_Ctrl() : base(id:"Fire_Ctrl",name:"妖术-火焰",
        "妹红拥有操控火焰程度的能力\n 妹红免疫点燃 冻伤\n" +
        "妹红在喝下咒火和暗影焰增益瓶或受到暗影焰和咒火的时候 妹红的能力会得到变化"){}
    protected override void ApplyEffects()
    {
        if (Player.HasBuff(BuffID.ShadowFlame))
        {
            Player.AddBuff(ModContent.BuffType<WeaponImbueShadowFlame>(),60*20);
            Player.ClearBuff(BuffID.ShadowFlame);
        }

        if (Player.HasBuff(BuffID.CursedInferno))
        {
            Player.AddBuff(BuffID.WeaponImbueCursedFlames, 60 * 20);
            Player.ClearBuff(BuffID.CursedInferno);
        }
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