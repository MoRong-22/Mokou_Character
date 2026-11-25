using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mokou_Character.Character;
using ReLogic.Content;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace Mokou_Character.Rendering;

/// <summary>重生/吟唱周围的热浪：复用原版两张场景 RT，读写分离并交换。</summary>
public sealed class NirvanaHeatWaveSystem : ModSystem
{
    // 固定容量，每帧从头填写，不积累请求，也不持有玩家实例。
    private readonly int[] playerIndices = new int[Main.maxPlayers];
    private int playerCount;
    private bool captureRequested;
    private Asset<Effect> heatWaveEffect;

    public override void Load()
    {
        if (Main.dedServ) return;
        Main.OnPreDraw += PrepareCapture;
        On_FilterManager.EndCapture += ApplyHeatWaves;
    }

    private void PrepareCapture(GameTime gameTime)
    {
        playerCount = 0;
        // 原版复古/迷幻光照直接画到后备缓冲，不能采样当前场景。
        if (!Main.gameMenu && !Main.mapFullscreen && !Main.drawToScreen && Lighting.NotRetro)
        {
            for (int i = 0; i < Main.player.Length && i < playerIndices.Length; i++)
            {
                Player player = Main.player[i];
                if (player != null && player.active && !player.dead &&
                    player.TryGetModPlayer<Mokou>(out var mokou) &&
                    mokou.TryGetNirvanaHeatWave(out _, out _, out _))
                {
                    playerIndices[playerCount++] = i;
                }
            }
        }

        SetCaptureRequired(playerCount > 0);
    }

    private void SetCaptureRequired(bool required)
    {
        if (captureRequested == required) return;
        // 只请求原版提供场景 RT；不注册屏幕滤镜。
        if (required) Filters.Scene.OnPostDraw += KeepSceneCapture;
        else Filters.Scene.OnPostDraw -= KeepSceneCapture;
        captureRequested = required;
    }

    private static void KeepSceneCapture() { }

    private void ApplyHeatWaves(On_FilterManager.orig_EndCapture orig, FilterManager self,
        RenderTarget2D finalTexture, RenderTarget2D screenTarget1, RenderTarget2D screenTarget2,
        Color clearColor)
    {
        if (ReferenceEquals(self, Filters.Scene) && playerCount > 0 &&
            screenTarget1 != null && screenTarget2 != null &&
            !screenTarget1.IsDisposed && !screenTarget2.IsDisposed &&
            !ReferenceEquals(screenTarget1, screenTarget2) &&
            screenTarget1.Width == screenTarget2.Width && screenTarget1.Height == screenTarget2.Height)
        {
            for (int i = 0; i < playerCount; i++)
            {
                Player player = Main.player[playerIndices[i]];
                if (player == null || !player.active || player.dead || !player.TryGetModPlayer<Mokou>(out var mokou))
                    continue;

                Vector2 size = new(screenTarget1.Width, screenTarget1.Height);
                Effect effect;
                if (mokou.TryGetNirvanaHeatWave(out Vector2 center, out Vector2 halfSize, out float elapsed))
                {
                    heatWaveEffect ??= ModContent.Request<Effect>(
                        "Mokou_Character/Assets/Effects/NirvanaHeatWave", AssetRequestMode.ImmediateLoad);
                    effect = heatWaveEffect.Value;
                    effect.Parameters["uTime"].SetValue(elapsed);
                    effect.Parameters["uDuration"].SetValue(mokou.PhoenixEffectDuration);
                    effect.Parameters["uViewportSize"].SetValue(size);
                    effect.Parameters["uSourceSize"].SetValue(size);
                    effect.Parameters["uCenterPixels"].SetValue(center);
                    effect.Parameters["uHalfSizePixels"].SetValue(halfSize);
                    effect.Parameters["uStrengthPixels"].SetValue(4f);
                    effect.Parameters["uDarkColor"].SetValue(mokou.fire_darkColor.ToVector3());
                    effect.Parameters["uLightColor"].SetValue(mokou.fire_lightColor.ToVector3());
                    effect.Parameters["uBurnStrength"].SetValue(0f);
                }
                else continue;

                GraphicsDevice device = screenTarget1.GraphicsDevice;
                SpriteBatch batch = Main.spriteBatch;
                // 角色绘制已经结束，没有活动批次。直接读 current、写 next，省去备份和回填。
                device.Textures[0] = null;
                device.SetRenderTarget(screenTarget2);
                batch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, effect, Matrix.Identity);
                try
                {
                    batch.Draw(screenTarget1, new Rectangle(0, 0, screenTarget2.Width, screenTarget2.Height), Color.White);
                }
                finally { batch.End(); }
                device.Textures[0] = null;

                RenderTarget2D swap = screenTarget1;
                screenTarget1 = screenTarget2;
                screenTarget2 = swap;
            }
        }

        // 把处理后的 RT 作为当前输入，空闲 RT 作为备用，原版继续执行其余合成。
        // 不重新绑定旧目标：DiscardContents 的 RT 被重新绑定会丢失刚画好的内容。
        orig(self, finalTexture, screenTarget1, screenTarget2, clearColor);
    }

    public override void Unload()
    {
        if (Main.dedServ) return;
        Main.OnPreDraw -= PrepareCapture;
        On_FilterManager.EndCapture -= ApplyHeatWaves;
        SetCaptureRequired(false);
        playerCount = 0;
        // Effect 属于资源系统，RT 属于原版；这里不释放借用的资源。
        heatWaveEffect = null;
    }
}
