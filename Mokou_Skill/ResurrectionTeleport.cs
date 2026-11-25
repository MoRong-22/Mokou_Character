using System;
using System.IO;
using Microsoft.Xna.Framework;
using Mokou_Character.Character;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Mokou_Character.Skill;

/// <summary>传送目标检查与联机确认；远距离传送先由服务器发送目标地图区块。</summary>
internal static class ResurrectionTeleport
{
    internal const byte RequestPacket = 1, ResultPacket = 2;
    private const int SearchRadiusTiles = 6;

    internal static bool WithinWorld(Vector2 center)
        => float.IsFinite(center.X) && float.IsFinite(center.Y)
            && center.X >= 16f * 10f && center.Y >= 16f * 10f
            && center.X < (Main.maxTilesX - 10) * 16f && center.Y < (Main.maxTilesY - 10) * 16f;

    internal static bool TryFindPosition(Player player, Vector2 center, out Vector2 position)
    {
        position = center - player.Size * 0.5f;
        if (!WithinWorld(center)) return false;
        if (Fits(player, position)) return true;
        // 只在鼠标周围六格找最近空位，不把空中目标强行吸到地面。
        Vector2 best = default;
        float bestDistance = float.MaxValue;
        bool found = false;
        for (int y = -SearchRadiusTiles; y <= SearchRadiusTiles; y++)
        for (int x = -SearchRadiusTiles; x <= SearchRadiusTiles; x++)
        {
            float distance = x * x + y * y;
            if (distance >= bestDistance || distance > SearchRadiusTiles * SearchRadiusTiles) continue;
            Vector2 candidate = position + new Vector2(x, y) * 16f;
            if (!Fits(player, candidate)) continue;
            best = candidate;
            bestDistance = distance;
            found = true;
        }
        if (found) position = best;
        return found;
    }

    private static bool Fits(Player player, Vector2 position)
        => WithinWorld(position) && WithinWorld(position + player.Size)
            && !Collision.SolidCollision(position, player.width, player.height)
            && !Collision.LavaCollision(position, player.width, player.height);

    internal static void Request(Mokou character, int serial, Vector2 center)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            bool valid = TryFindPosition(character.Player, center, out Vector2 position);
            if (valid) character.Player.Teleport(position, TeleportationStyleID.DebugTeleport);
            character.ResurrectionSkill.ReceiveResult(serial, valid, position);
            return;
        }
        ModPacket packet = character.Mod.GetPacket();
        packet.Write(RequestPacket);
        packet.Write(serial);
        packet.Write(center.X);
        packet.Write(center.Y);
        packet.Send();
    }

    internal static void Handle(Mod mod, byte message, BinaryReader reader, int sender)
    {
        int serial = reader.ReadInt32();
        if (message == RequestPacket)
        {
            Vector2 center = new(reader.ReadSingle(), reader.ReadSingle());
            if (Main.netMode != NetmodeID.Server || (uint)sender >= Main.maxPlayers) return;
            Player player = Main.player[sender];
            Mokou character = player.GetModPlayer<Mokou>();
            Vector2 destination = player.position;
            bool valid = player.active && !player.dead && !player.CCed && character.IsSelected && !character.IsNirvana
                && character.CanAcceptResurrectionTeleport()
                && TryFindPosition(player, center, out destination);
            if (valid)
            {
                character.RecordResurrectionTeleport();
                RemoteClient.CheckSection(sender, destination);
                player.Teleport(destination, TeleportationStyleID.DebugTeleport);
                player.velocity = Vector2.Zero;
                // 拥有者由确认包处理；其余客户端接收原版传送同步。
                NetMessage.SendData(MessageID.TeleportEntity, -1, sender, null,
                    0, sender, destination.X, destination.Y, TeleportationStyleID.DebugTeleport);
            }
            ModPacket reply = mod.GetPacket();
            reply.Write(ResultPacket);
            reply.Write(serial);
            reply.Write(valid);
            reply.Write(destination.X);
            reply.Write(destination.Y);
            reply.Send(sender);
        }
        else
        {
            bool valid = reader.ReadBoolean();
            Vector2 destination = new(reader.ReadSingle(), reader.ReadSingle());
            if (Main.netMode == NetmodeID.MultiplayerClient)
                Main.LocalPlayer.GetModPlayer<Mokou>().ResurrectionSkill.ReceiveResult(serial, valid, destination);
        }
    }
}
