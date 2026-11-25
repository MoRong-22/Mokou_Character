using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Mokou_Character.Character;
using Mokou_Character.SkillVisuals;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Mokou_Character.Skill;

/// <summary>传送起点/终点的无伤害焰光；只保留两团局部火焰，不抓取整屏 RT。</summary>
public sealed class MokouResurrectionFlameProjectile : ModProjectile
{
    private Color dark, light;
    private Vector2 size;
    private int age;
    private readonly ResurrectionFlameDrawing drawing = new();
    internal bool Arriving => Projectile.ai[0] == 1f;
    internal float BodyOpacity => ResurrectionRules.BodyOpacity(age, Arriving);
    private int Lifetime => Arriving ? ResurrectionRules.AppearTicks + 16 : ResurrectionRules.BurnTicks + 20;
    public override string Texture => "Terraria/Images/MagicPixel";

    public override void SetDefaults()
    {
        Projectile.width = 120;
        Projectile.height = 160;
        Projectile.timeLeft = 90;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.netImportant = true;
        Projectile.hide = true;
    }

    internal static Projectile Spawn(Player player, bool arriving, Color dark, Color light)
    {
        Vector2 size = new(Math.Max(96f, player.width * 3.4f), Math.Max(124f, player.height * 2.6f));
        int index = Projectile.NewProjectile(player.GetSource_Misc("ResurrectionVisual"),
            player.Bottom - new Vector2(0f, size.Y * 0.5f - 8f), Vector2.Zero,
            ModContent.ProjectileType<MokouResurrectionFlameProjectile>(), 0, 0, player.whoAmI,
            arriving ? 1f : 0f);
        if ((uint)index >= Main.maxProjectiles) return null;
        Projectile projectile = Main.projectile[index];
        if (projectile.ModProjectile is not MokouResurrectionFlameProjectile visual) return null;
        visual.dark = dark;
        visual.light = light;
        visual.size = size;
        projectile.timeLeft = visual.Lifetime;
        projectile.netUpdate = true;
        player.GetModPlayer<Mokou>().ResurrectionVisual = visual;
        return projectile;
    }

    public override void OnSpawn(IEntitySource source)
    {
        size = new Vector2(96f, 124f);
        if ((uint)Projectile.owner < Main.maxPlayers)
        {
            var character = Main.player[Projectile.owner].GetModPlayer<Mokou>();
            dark = character.fire_darkColor;
            light = character.fire_lightColor;
        }
    }

    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => false;

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(dark.PackedValue); writer.Write(light.PackedValue);
        writer.Write(size.X); writer.Write(size.Y); writer.Write(age);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        dark = new Color { PackedValue = reader.ReadUInt32() };
        light = new Color { PackedValue = reader.ReadUInt32() };
        size = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        age = reader.ReadInt32();
        Projectile.timeLeft = Math.Max(1, Lifetime - age);
    }

    internal bool ControlsBody(Player player)
    {
        if (player.dead || !player.active || !Projectile.active || !ReferenceEquals(Projectile.ModProjectile, this)) return false;
        Vector2 feet = Projectile.Center + new Vector2(0f, size.Y * 0.5f - 8f);
        return Vector2.DistanceSquared(feet, player.Bottom) < 48f * 48f
            && (Arriving ? age < ResurrectionRules.AppearTicks : age <= ResurrectionRules.BurnTicks + 4);
    }

    public override void AI()
    {
        if ((uint)Projectile.owner >= Main.maxPlayers) { Projectile.Kill(); return; }
        Player player = Main.player[Projectile.owner];
        var character = player.GetModPlayer<Mokou>();
        if (!player.active || player.dead || !character.IsSelected || character.IsNirvana)
        { Projectile.Kill(); return; }
        age++;
        if (ControlsBody(player))
        {
            // 目标离起点很近时，重现阶段优先，避免仍存活的起点火焰把本体重新隐藏。
            var old = character.ResurrectionVisual;
            if (Arriving || old?.Projectile.active != true || !old.Arriving)
                character.ResurrectionVisual = this;
        }
        if (!Main.dedServ)
            Lighting.AddLight(Projectile.Center, light.ToVector3() * (1f - age / (float)Lifetime) * 0.7f);
    }

    public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
        List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
        => overPlayers.Add(index);

    public override void OnKill(int timeLeft)
    {
        if ((uint)Projectile.owner >= Main.maxPlayers) return;
        var character = Main.player[Projectile.owner].GetModPlayer<Mokou>();
        if (ReferenceEquals(character.ResurrectionVisual, this)) character.ResurrectionVisual = null;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        drawing.Draw(Projectile.Center, size, age / 60f, age / (float)Lifetime, Arriving, dark, light);
        return false;
    }
}
