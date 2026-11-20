using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace Mokou_Character.SkillVisuals;

/// <summary>技能共用的火焰线段绘制。使用当前 SpriteBatch，不分配 RT 或切换着色器。</summary>
internal static class SkillFlameDrawing
{
    internal static void Stroke(Vector2 from, Vector2 to, float width, Color dark, Color light, float opacity)
    {
        if (width <= 0 || opacity <= 0) return;
        Line(from, to, width, dark * opacity);
        Line(from, to, width * 0.6f, Color.Lerp(dark, light, 0.55f) * opacity);
        Line(from, to, width * 0.22f, new Color(light.R, light.G, light.B, 0) * opacity);
    }

    internal static void Line(Vector2 from, Vector2 to, float width, Color color)
    {
        Vector2 delta = to - from;
        if (delta.LengthSquared() < 0.001f) return;
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        Main.EntitySpriteDraw(pixel, from - Main.screenPosition, null, color, delta.ToRotation(),
            new Vector2(0, pixel.Height * 0.5f),
            new Vector2((delta.Length() + 0.6f) / pixel.Width, width / pixel.Height), SpriteEffects.None);
    }
}
