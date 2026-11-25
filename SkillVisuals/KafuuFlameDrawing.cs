using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace Mokou_Character.SkillVisuals;

/// <summary>回旋月牙与飞踢彗星火焰共用绘制入口。只复用着色器和顶点，不分配 RT。</summary>
internal sealed class KafuuFlameDrawing
{
    private Asset<Effect> asset;

    private Effect Prepare(string technique, float time, Color dark, Color light)
    {
        asset ??= ModContent.Request<Effect>("Mokou_Character/Assets/Effects/KafuuFlameTrail", AssetRequestMode.ImmediateLoad);
        Effect effect = asset.Value;
        effect.CurrentTechnique = effect.Techniques[technique];
        effect.Parameters["uTime"].SetValue(time);
        effect.Parameters["uDarkColor"].SetValue(dark.ToVector3());
        effect.Parameters["uLightColor"].SetValue(light.ToVector3());
        return effect;
    }

    internal void DrawArc(VertexPositionColorTexture[] vertices, int count, float time, Color dark, Color light)
    {
        if (count < 4) return;
        Effect effect = Prepare("FlameTrail", time, dark, light);
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

    /// <summary>局部 X 正向为飞踢方向，脚尖位于火头，长尾沿反方向延伸，任意朝向均使用同一坐标系。</summary>
    internal void DrawDive(Vector2 foot, float rotation, float time, float strength, Color dark, Color light)
    {
        Effect effect = Prepare("DiveFlame", time, dark, light);
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        Vector2 textureSize = new(pixel.Width, pixel.Height);
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
        try
        {
            // 尾部约 180 像素，前端伸出脚尖约 24 像素；用完整 UV，兼容 1×1000 的 MagicPixel。
            Main.spriteBatch.Draw(pixel, foot - Main.screenPosition, null, Color.White * MathHelper.Clamp(strength, 0f, 1f),
                rotation, textureSize * new Vector2(0.79f, 0.5f), new Vector2(232f, 108f) / textureSize,
                SpriteEffects.None, 0f);
        }
        finally
        {
            Main.spriteBatch.End();
            ResumeBatch();
        }
    }

    private static void ResumeBatch() => Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
        Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
}
