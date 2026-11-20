using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mokou_Character.Character;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace Mokou_Character.Content.UI;

/// <summary>右上角的人体自燃火焰表。双色随角色实时变化，亮色从底部填充，百分比绘制在火焰上方。</summary>
[Autoload(Side = ModSide.Client)]
public sealed class SpontaneousCombustionUISystem : ModSystem
{
    private Asset<Texture2D> outerFlame;
    private Asset<Texture2D> innerFlame;

    public override void Load()
    {
        outerFlame = ModContent.Request<Texture2D>("Mokou_Character/Assets/UI/CombustionFlameOuter");
        innerFlame = ModContent.Request<Texture2D>("Mokou_Character/Assets/UI/CombustionFlameInner");
    }

    public override void Unload()
    {
        outerFlame = null;
        innerFlame = null;
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
        if (index < 0) index = layers.Count;
        layers.Insert(index, new LegacyGameInterfaceLayer("Mokou: Spontaneous Combustion", DrawUI, InterfaceScaleType.UI));
    }

    /// <summary>小地图显示时置于其下方；关闭或切换叠加地图时回到右上角血条下方。</summary>
    private static Rectangle GetFlameArea()
    {
        // 本层是 InterfaceScaleType.UI，screenWidth 已经换算成 UI 坐标，不能再次除以 UIScale。
        float centerX = Main.screenWidth - 172f;
        float top = 136f;
        if (Main.mapEnabled && Main.mapReady && Main.mapStyle == 1 && !Main.mapFullscreen)
        {
            // 原版 miniMap 坐标位于地图自身的缩放空间，乘 MapScale 才能与本层 UI 对齐。
            float mapScale = Math.Max(0.1f, Main.MapScale);
            centerX = (Main.miniMapX + Main.miniMapWidth * 0.5f) * mapScale;
            // 给地图边框、底部按钮和火焰上方的百分比文字留出间隔。
            top = (Main.miniMapY + Main.miniMapHeight + 36f) * mapScale + 28f;
        }
        return new Rectangle((int)Math.Max(16f, centerX - 32f), (int)top, 64, 88);
    }

    private bool DrawUI()
    {
        if (Main.gameMenu || Main.hideUI || Main.LocalPlayer == null
            || !Main.LocalPlayer.active || Main.LocalPlayer.dead) return true;
        var character = Main.LocalPlayer.GetModPlayer<Mokou>();
        if (!character.IsSelected) return true;

        float progress = character.SpontaneousCombustionProgress;
        Rectangle area = GetFlameArea();
        SpriteBatch batch = Main.spriteBatch;
        DrawFlame(batch, outerFlame.Value, innerFlame.Value, area, progress,
            character.fire_darkColor, character.fire_lightColor);
        int percent = (int)MathF.Floor(progress * 100f + 0.0001f);
        Utils.DrawBorderString(batch, percent + "%", new Vector2(area.Center.X, area.Top - 23f),
            Color.White, 0.85f, 0.5f);
        Utils.DrawBorderString(batch, "人体自燃", new Vector2(area.Center.X, area.Bottom + 4f),
            Color.White, 0.6f, 0.5f);
        return true;
    }

    /// <summary>复用两张灰度贴图；裁切填充范围即可显示进度，无需 RT、着色器或逐帧创建纹理。</summary>
    internal static void DrawFlame(SpriteBatch batch, Texture2D outer, Texture2D inner,
        Rectangle area, float progress, Color dark, Color light)
    {
        progress = MathHelper.Clamp(progress, 0f, 1f);
        // 未蓄满部分保留暗色轮廓，0% 时也能识别火焰形状。
        batch.Draw(outer, area, Color.Lerp(new Color(17, 19, 26), dark, 0.20f));
        batch.Draw(inner, area, Color.Lerp(new Color(23, 26, 34), light, 0.12f));
        int filledPixels = (int)MathF.Round(outer.Height * progress);
        if (filledPixels <= 0) return;
        Rectangle source = new(0, outer.Height - filledPixels, outer.Width, filledPixels);
        int filledHeight = (int)MathF.Round(area.Height * filledPixels / (float)outer.Height);
        Rectangle destination = new(area.X, area.Bottom - filledHeight, area.Width, filledHeight);
        batch.Draw(outer, destination, source, dark);
        batch.Draw(inner, destination, source, light);
    }
}
