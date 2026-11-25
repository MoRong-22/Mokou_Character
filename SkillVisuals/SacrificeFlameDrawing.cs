using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace Mokou_Character.SkillVisuals;

/// <summary>舍身击专用绘制入口：蓄力、火弹和连续焰尾共用颜色与着色器，不创建 RT。</summary>
internal sealed class SacrificeFlameDrawing
{
    private Asset<Effect> asset;

    private Effect Prepare(string technique, float time, float progress, Color dark, Color light)
    {
        asset ??= ModContent.Request<Effect>("Mokou_Character/Assets/Effects/SacrificeFlame", AssetRequestMode.ImmediateLoad);
        Effect effect = asset.Value;
        effect.CurrentTechnique = effect.Techniques[technique];
        effect.Parameters["uTime"].SetValue(time);
        effect.Parameters["uProgress"].SetValue(progress);
        effect.Parameters["uDarkColor"].SetValue(dark.ToVector3());
        effect.Parameters["uLightColor"].SetValue(light.ToVector3());
        return effect;
    }

    internal void Sprite(string technique, Vector2 position, Vector2 size, Vector2 origin, float rotation,
        float time, float progress, Color dark, Color light, float opacity = 1f)
    {
        Effect effect = Prepare(technique, time, progress, dark, light);
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        Vector2 textureSize = new(pixel.Width, pixel.Height);
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
        try
        {
            Main.spriteBatch.Draw(pixel, position - Main.screenPosition, null, Color.White * opacity, rotation,
                textureSize * origin, size / textureSize, SpriteEffects.None, 0f);
        }
        finally
        {
            Main.spriteBatch.End();
            ResumeBatch();
        }
    }

    internal void Ribbon(VertexPositionColorTexture[] vertices, int count, float time, Color dark, Color light)
    {
        if (count < 4) return;
        Effect effect = Prepare("DashRibbon", time, 0f, dark, light);
        GraphicsDevice device = Main.instance.GraphicsDevice;
        effect.Parameters["uTransform"].SetValue(Main.GameViewMatrix.TransformationMatrix
            * Matrix.CreateOrthographicOffCenter(0, device.Viewport.Width, device.Viewport.Height, 0, 0, 1));
        Main.spriteBatch.End();
        var oldVertices = device.GetVertexBuffers();
        var oldIndices = device.Indices;
        var oldBlend = device.BlendState;
        var oldDepth = device.DepthStencilState;
        var oldRasterizer = device.RasterizerState;
        try
        {
            device.BlendState = BlendState.AlphaBlend;
            device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState = RasterizerState.CullNone;
            effect.CurrentTechnique.Passes[0].Apply();
            device.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices, 0, count - 2);
        }
        finally
        {
            device.SetVertexBuffers(oldVertices);
            device.Indices = oldIndices;
            device.BlendState = oldBlend;
            device.DepthStencilState = oldDepth;
            device.RasterizerState = oldRasterizer;
            ResumeBatch();
        }
    }

    private static void ResumeBatch() => Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
        Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
}
