using System;
using CharacterRely.Content.SkillSystem;
using Microsoft.Xna.Framework;
using Mokou_Character.Character;
using Terraria;
using Terraria.ModLoader;

namespace Mokou_Character.Content.UI;

/// <summary>人体自燃改为技能栏周围持续燃烧的进度框，颜色跟随当前妹红焰色。</summary>
[Autoload(Side = ModSide.Client)]
public sealed class SpontaneousCombustionUISystem : ModSystem
{
    private readonly CombustionSkillBarFlame flame = new();

    public override void Load()
    {
        SkillBarDrawing.DrawBackground += DrawFlame;
        SkillBarDrawing.DrawForeground += DrawProgress;
    }

    public override void Unload()
    {
        SkillBarDrawing.DrawBackground -= DrawFlame;
        SkillBarDrawing.DrawForeground -= DrawProgress;
        flame.Unload();
    }

    private static bool TryGetCharacter(SkillBarDrawContext context, out Mokou character)
    {
        character = context.Character as Mokou;
        return character?.IsSelected == true && !Main.gameMenu && !Main.hideUI && !Main.mapFullscreen
            && character.Player.active && !character.Player.dead;
    }

    private void DrawFlame(SkillBarDrawContext context)
    {
        if (!TryGetCharacter(context, out Mokou character)) return;
        flame.Draw(context.SpriteBatch, context.Bounds, context.TransformMatrix,
            Main.GlobalTimeWrappedHourly, character.SpontaneousCombustionProgress,
            character.fire_darkColor, character.fire_lightColor);
    }

    private static void DrawProgress(SkillBarDrawContext context)
    {
        if (!TryGetCharacter(context, out Mokou character)) return;
        int percent = (int)MathF.Floor(character.SpontaneousCombustionProgress * 100f + 0.0001f);
        // 文字位于上缘火舌之外，装配菜单稍后绘制，会自然遮住这一层。
        Utils.DrawBorderString(context.SpriteBatch, "人体自燃 " + percent + "%",
            new Vector2(context.Bounds.Center.X, context.Bounds.Top - 39f),
            percent >= 100 ? character.fire_lightColor : Color.White, 0.65f, 0.5f);
    }
}