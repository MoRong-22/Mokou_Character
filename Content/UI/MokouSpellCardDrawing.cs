using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace Mokou_Character.Content.UI;

/// <summary>符纸和使用中的燃烧边缘共用一次局部绘制，不创建纹理或 RT。</summary>
internal sealed class MokouSpellCardDrawing
{
    private Asset<Effect> asset;
    internal const float Padding = 25f;

    internal void Draw(SpriteBatch batch, Vector2 center, Vector2 size, float rotation, Matrix transform,
        float time, bool active, bool hovered, float cooldown, Color dark, Color light, float seed = 0f)
    {
        asset ??= ModContent.Request<Effect>("Mokou_Character/Assets/Effects/MokouSpellCard", AssetRequestMode.ImmediateLoad);
        Effect effect = asset.Value;
        effect.Parameters["uTime"].SetValue(time);
        effect.Parameters["uSeed"].SetValue(seed);
        effect.Parameters["uActive"].SetValue(active ? 1f : 0f);
        effect.Parameters["uHovered"].SetValue(hovered ? 1f : 0f);
        effect.Parameters["uCooldown"].SetValue(MathHelper.Clamp(cooldown, 0f, 1f));
        effect.Parameters["uCardSize"].SetValue(size);
        effect.Parameters["uPadding"].SetValue(Padding);
        effect.Parameters["uDarkColor"].SetValue(dark.ToVector3());
        effect.Parameters["uLightColor"].SetValue(light.ToVector3());
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        Vector2 textureSize = new(pixel.Width, pixel.Height);
        batch.End();
        batch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, effect, transform);
        try
        {
            batch.Draw(pixel, center, null, Color.White, rotation, textureSize * 0.5f,
                (size + new Vector2(Padding * 2f)) / textureSize, SpriteEffects.None, 0f);
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
