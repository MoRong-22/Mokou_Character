using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Mokou_Character.Content.Mokou_Buff;

public class WeaponImbueShadowFlame : ModBuff
{
    public override void SetStaticDefaults()
    {
        Main.pvpBuff[Type] = true;
        Main.persistentBuff[Type] = true;

        Main.buffDoubleApply[Type] = false;
        
        Main.meleeBuff[Type] = true;
        Main.buffNoTimeDisplay[Type] = true;
        Main.buffNoSave[Type] = false;
        BuffID.Sets.IsATagBuff[Type] = true;
        BuffID.Sets.IsAFlaskBuff[Type] = true;
        BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
    }
}