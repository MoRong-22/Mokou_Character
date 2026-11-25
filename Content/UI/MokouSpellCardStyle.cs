using System;
using System.Collections.Generic;
using CharacterRely.Content.SkillSystem;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mokou_Character.Character;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using CharacterSkill = CharacterRely.Content.SkillSystem.Skill;

namespace Mokou_Character.Content.UI;

/// <summary>只为妹红注册焦边残符；装配、按键和完整介绍继续由角色库处理。</summary>
[Autoload(Side = ModSide.Client)]
public sealed class MokouSpellCardUISystem : ModSystem
{
    private readonly MokouSpellCardStyle style = new();
    public override void Load() => SkillBarDrawing.RegisterStyle<Mokou>(style);
    public override void Unload()
    {
        SkillBarDrawing.UnregisterStyle<Mokou>();
        style.Unload();
    }
}

/// <summary>短符、焦边、图形和两字标题。技能仍在释放时燃烧，冷却数字与标题分区。</summary>
internal sealed class MokouSpellCardStyle : ISkillBarStyle
{
    private readonly MokouSpellCardDrawing drawing = new();
    private readonly Dictionary<string, Asset<Texture2D>> customIcons = new();
    private Asset<Texture2D> glyphs;
    public Vector2 Size => MokouSpellCardLayout.BarSize;
    public int HitTest(Rectangle bounds, Vector2 mouse) => MokouSpellCardLayout.HitTest(bounds, mouse);

    public void Draw(SkillBarDrawContext context)
    {
        if (context.Character is not Mokou character) return;
        glyphs ??= ModContent.Request<Texture2D>("Mokou_Character/Assets/UI/AshSpellCardGlyphs", AssetRequestMode.ImmediateLoad);
        for (int slot = 0; slot < 4; slot++)
        {
            var skill = character.GetEquippedSkill(slot);
            float cooldown = skill?.CooldownRemaining > 0
                ? skill.CooldownRemaining / (float)Math.Max(1, skill.Info.CooldownTicks) : 0f;
            drawing.Draw(context.SpriteBatch, MokouSpellCardLayout.Center(context.Bounds, slot),
                MokouSpellCardLayout.CardSize, MokouSpellCardLayout.Rotation(slot), context.TransformMatrix,
                Main.GlobalTimeWrappedHourly, skill?.IsActive == true, context.HoveredSlot == slot,
                cooldown, character.fire_darkColor, character.fire_lightColor, slot);
            DrawContents(context, slot, skill, character.fire_lightColor);
        }
    }

    private void DrawContents(SkillBarDrawContext context, int slot, CharacterSkill skill, Color flame)
    {
        bool active = skill?.IsActive == true;
        bool cooling = !active && skill?.CooldownRemaining > 0;
        Color ink = skill == null ? new Color(115, 99, 81) : new Color(103, 46, 35);
        var content = MokouSpellCardContent.For(skill);
        DrawKey(context, slot);
        if (content is { } known)
        {
            Sprite(context, slot, glyphs.Value, known.Icon, new Vector2(0, -23), new Vector2(54), ink * (cooling ? 0.17f : 1f));
            Sprite(context, slot, glyphs.Value, known.Title, new Vector2(0, 21), new Vector2(60, 30), ink);
            // 使用技能中的实际常量，不把预览页面的耗血数字写死到绘制里。
            Text(context, slot, known.LifeCost + " HP", new Vector2(3, 46), 0.40f, 49, new Color(130, 75, 46));
            Quad(context, slot, new Vector2(-22, 46), new Vector2(4), new Color(169, 55, 35), MathHelper.PiOver4);
        }
        else
        {
            // 未登记的技能可用自己的图标，保留名称；空槽不会伪装成任何内置技能。
            if (!string.IsNullOrEmpty(skill?.IconPath))
            {
                if (!customIcons.TryGetValue(skill.IconPath, out var icon))
                    customIcons.Add(skill.IconPath, icon = ModContent.Request<Texture2D>(skill.IconPath, AssetRequestMode.ImmediateLoad));
                Texture2D texture = icon.Value;
                float ratio = 46f / Math.Max(texture.Width, texture.Height);
                Sprite(context, slot, texture, null, new Vector2(0, -23), texture.Size() * ratio, Color.White * (cooling ? 0.2f : 1f));
            }
            else
                Text(context, slot, skill == null ? "＋" : "符", new Vector2(0, -23), 0.95f, 46, ink * (cooling ? 0.2f : 1f));
            Text(context, slot, skill?.Name ?? "未装配", new Vector2(0, 21), 0.61f, 62, ink);
        }
        if (cooling)
            Text(context, slot, (skill.CooldownRemaining / 60f).ToString("0.0"), new Vector2(0, -20), 0.92f, 60, new Color(91, 43, 30));
        if (skill != null)
        {
            string state = active ? "释放中" : cooling ? "冷却中" : "就绪";
            Text(context, slot, state, new Vector2(0, 74), 0.43f, 63,
                active ? Color.Lerp(flame, Color.White, 0.35f) : new Color(226, 209, 177), shadow: true);
            if (skill.MaxCount > 1)
                Text(context, slot, skill.Count + "/" + skill.MaxCount, new Vector2(20, -47), 0.37f, 30, ink);
        }
    }

    private static void DrawKey(SkillBarDrawContext context, int slot)
    {
        string key = "—";
        if (slot < SkillKeybindSystem.SlotKeys.Count)
        {
            var keys = SkillKeybindSystem.SlotKeys[slot].GetAssignedKeys();
            if (keys.Count > 0) key = string.Join(" / ", keys);
        }
        float width = Math.Clamp(FontAssets.MouseText.Value.MeasureString(key).X * 0.53f + 10f, 22f, MokouSpellCardLayout.KeySize.X);
        Vector2 center = MokouSpellCardLayout.KeyCenter;
        Quad(context, slot, center, new Vector2(width, 20), new Color(148, 120, 80));
        Quad(context, slot, center, new Vector2(width - 2, 18), new Color(31, 32, 30));
        Text(context, slot, key, center, 0.53f, width - 5, new Color(231, 210, 170));
    }

    private static void Quad(SkillBarDrawContext context, int slot, Vector2 local, Vector2 size, Color color, float angle = 0f)
    {
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        context.SpriteBatch.Draw(pixel, MokouSpellCardLayout.ToUI(context.Bounds, slot, local), null, color,
            MokouSpellCardLayout.Rotation(slot) + angle, pixel.Size() * 0.5f, size / pixel.Size(), SpriteEffects.None, 0f);
    }

    private static void Sprite(SkillBarDrawContext context, int slot, Texture2D texture, Rectangle? source,
        Vector2 local, Vector2 size, Color color)
    {
        Vector2 dimensions = source is { } region ? new Vector2(region.Width, region.Height) : texture.Size();
        context.SpriteBatch.Draw(texture, MokouSpellCardLayout.ToUI(context.Bounds, slot, local), source, color,
            MokouSpellCardLayout.Rotation(slot), dimensions * 0.5f, size / dimensions, SpriteEffects.None, 0f);
    }

    private static void Text(SkillBarDrawContext context, int slot, string text, Vector2 local,
        float scale, float maxWidth, Color color, bool shadow = false)
    {
        var font = FontAssets.MouseText.Value;
        Vector2 measure = font.MeasureString(text);
        scale = Math.Min(scale, maxWidth / Math.Max(1f, measure.X));
        Vector2 position = MokouSpellCardLayout.ToUI(context.Bounds, slot, local);
        float rotation = MokouSpellCardLayout.Rotation(slot);
        if (shadow)
            context.SpriteBatch.DrawString(font, text, position + Vector2.One, new Color(22, 17, 13, 210), rotation,
                measure * 0.5f, scale, SpriteEffects.None, 0f);
        context.SpriteBatch.DrawString(font, text, position, color, rotation, measure * 0.5f, scale, SpriteEffects.None, 0f);
    }

    internal void Unload()
    {
        drawing.Unload();
        glyphs = null;
        customIcons.Clear();
    }
}
