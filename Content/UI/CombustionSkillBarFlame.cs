using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace Mokou_Character.Content.UI;

/// <summary>一个局部四边形完成燃烧与进度裁切，不申请屏幕 RT 或创建粒子。</summary>
internal sealed class CombustionSkillBarFlame
{
    private Asset<Effect> asset;
    internal const int HorizontalPadding = 28, VerticalPadding = 34;

    internal void Draw(SpriteBatch batch, Rectangle bar, Matrix transform, float time, float progress,
        Color dark, Color light)
    {
        asset ??= ModContent.Request<Effect>("Mokou_Character/Assets/Effects/CombustionSkillBar", AssetRequestMode.ImmediateLoad);
        Effect effect = asset.Value;
        effect.Parameters["uTime"].SetValue(time);
        effect.Parameters["uProgress"].SetValue(MathHelper.Clamp(progress, 0f, 1f));
        effect.Parameters["uBarSize"].SetValue(new Vector2(bar.Width, bar.Height));
        effect.Parameters["uPadding"].SetValue(new Vector2(HorizontalPadding, VerticalPadding));
        effect.Parameters["uDarkColor"].SetValue(dark.ToVector3());
        effect.Parameters["uLightColor"].SetValue(light.ToVector3());
        var canvas = new Rectangle(bar.X - HorizontalPadding, bar.Y - VerticalPadding,
            bar.Width + HorizontalPadding * 2, bar.Height + VerticalPadding * 2);
        batch.End();
        batch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, effect, transform);
        try
        {
            // 使用目标矩形，完整 UV 映射到画布，兼容实际为 1×1000 的 MagicPixel。
            batch.Draw(TextureAssets.MagicPixel.Value, canvas, Color.White);
        }
        finally
        {
            batch.End();
            batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, transform);
        }
    }

    internal void Unload() => asset = null;
}
