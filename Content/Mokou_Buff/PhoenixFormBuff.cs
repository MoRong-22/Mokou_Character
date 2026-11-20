using Mokou_Character.Character;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Mokou_Character.Content.Mokou_Buff;

/// <summary>不死鸟形态的计时与 UI 图标；回血、飞行和模型显隐由妹红组件处理。</summary>
public sealed class PhoenixFormBuff : ModBuff
{
    public override string Texture => "Terraria/Images/Buff_" + BuffID.Inferno;

    public override void SetStaticDefaults()
    {
        Main.debuff[Type] = false;
        Main.buffNoSave[Type] = true;
        Main.buffNoTimeDisplay[Type] = false;
    }

    public override void Update(Player player, ref int buffIndex)
    {
        if (!player.GetModPlayer<Mokou>().IsSelected)
        {
            player.DelBuff(buffIndex);
            buffIndex--;
        }
    }
}
