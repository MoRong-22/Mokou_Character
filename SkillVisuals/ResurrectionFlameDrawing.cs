using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace Mokou_Character.SkillVisuals;

/// <summary>Resurrection 的局部火焰绘制，进入和退出时保持弹幕绘制批次约定。</summary>
internal sealed class ResurrectionFlameDrawing
{
    private Asset<Effect> asset;

    internal void Draw(Vector2 center, Vector2 size, float time, float progress, bool arriving, Color dark, Color light)
    {
        asset ??= ModContent.Request<Effect>("Mokou_Character/Assets/Effects/ResurrectionFlame", AssetRequestMode.ImmediateLoad);
        Effect effect = asset.Value;
        effect.Parameters["uTime"].SetValue(time);
        effect.Parameters["uProgress"].SetValue(progress);
        effect.Parameters["uArriving"].SetValue(arriving ? 1f : 0f);
        effect.Parameters["uDarkColor"].SetValue(dark.ToVector3());
        effect.Parameters["uLightColor"].SetValue(light.ToVector3());
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        Vector2 textureSize = new(pixel.Width, pixel.Height);
        // MagicPixel 实际为 1×1000。origin 使用贴图像素，scale 要除以贴图尺寸，
        // 才能让完整的 0～1 UV 落在角色身边的 size 矩形内。
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
            SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
            effect, Main.GameViewMatrix.TransformationMatrix);
        try
        {
            Main.spriteBatch.Draw(pixel, center - Main.screenPosition,
                null, Color.White, 0f, textureSize * 0.5f, size / textureSize, SpriteEffects.None, 0f);
        }
        finally
        {
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
