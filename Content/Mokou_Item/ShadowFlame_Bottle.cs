using Mokou_Character.Content.Mokou_Buff;
using Terraria.ID;
using Terraria.ModLoader;

namespace Mokou_Character.Content.Mokou_Item;

public class ShadowFlame_Bottle : ModItem
{
    public override void SetDefaults()
    {    
        Item.noMelee = true;
        Item.width = 40;
        Item.height = 20;
        Item.useTime = 15;
        Item.useAnimation = 15;
        Item.useStyle = ItemUseStyleID.DrinkLiquid; 
        Item.consumable = true;
        Item.buffType = ModContent.BuffType<WeaponImbueShadowFlame>();
        Item.buffTime = 3600*20;
        Item.maxStack = 999;
    }

    public override void AddRecipes()
    {
        
    }
}