﻿using System.Collections.Generic;
using Lidgren.Network;
using UnityEngine;

namespace Networking.Packets
{
    public enum PacketType : byte
    {
        Connected = 0,
        PlayerPreferences = 5,
        PlayerExtraInfo = 6,
        PlayerConnected = 10,
        PlayerDisconnected = 11,
        PlayerState = 12,
        PlayerKill = 13,
        PlayerDeath = 14,
        PlayerShoot = 15,
        WorldState = 20
    }

    public interface IPacket
    {
        PacketType Type { get; }

        int SequenceChannel { get; }

        void Write(NetOutgoingMessage msg);
    }

    public static class Packet
    {
        public static NetOutgoingMessage Write<T>(NetPeer peer, T packet) where T : IPacket
        {
            var msg = peer.CreateMessage();
            msg.Write((byte) packet.Type);
            packet.Write(msg);
            return msg;
        }
    }

    /// <summary>
    ///     Sent to a player when they first connect. Contains some general server info and its current players.
    /// </summary>
    public struct Connected : IPacket
    {
        public PacketType Type => PacketType.Connected;
        public int SequenceChannel => 0;

        public byte playerId;
        public byte maxPlayers;

        public string levelName;

        public List<PlayerPreferences> currentPlayers;

        public List<PlayerExtraInfo> currentPlayersInfo;

        public static Connected Read(NetIncomingMessage msg)
        {
            return new Connected
            {
                playerId = msg.ReadByte(),
                maxPlayers = msg.ReadByte(),
                levelName = msg.ReadString(),
                currentPlayers = ReadPlayerList(msg),
                currentPlayersInfo = ReadPlayerInfo(msg)
            };
        }

        private static List<PlayerPreferences> ReadPlayerList(NetIncomingMessage msg)
        {
            var length = msg.ReadByte();
            var list = new List<PlayerPreferences>(length);
            for (var i = 0; i < length; i++)
                list.Add(PlayerPreferences.Read(msg));
            return list;
        }

        private static List<PlayerExtraInfo> ReadPlayerInfo(NetIncomingMessage msg)
        {
            var length = msg.ReadByte();
            var list = new List<PlayerExtraInfo>(length);
            for (var i = 0; i < length; i++)
                list.Add(PlayerExtraInfo.Read(msg));
            return list;
        }

        public void Write(NetOutgoingMessage msg)
        {
            msg.Write(playerId);
            msg.Write(maxPlayers);
            msg.Write(levelName);

            msg.Write((byte) currentPlayers.Count);
            foreach (var pl in currentPlayers) pl.Write(msg);

            msg.Write((byte) currentPlayersInfo.Count);
            foreach (var pl in currentPlayersInfo) pl.Write(msg);
        }
    }

    /// <summary>
    ///     Sent by connecting player to server, and to other clients by the server. Contains player name and
    ///     other possible personal preferences like player model.
    /// </summary>
    public struct PlayerPreferences : IPacket
    {
        public PacketType Type => PacketType.PlayerPreferences;
        public int SequenceChannel => 0;

        public byte playerId;
        public string name;
        public Color color;

        public static PlayerPreferences Read(NetIncomingMessage msg)
        {
            return new PlayerPreferences
            {
                playerId = msg.ReadByte(),
                name = msg.ReadString(),
                color = msg.ReadColor(false)
            };
        }

        public void Write(NetOutgoingMessage msg)
        {
            msg.Write(playerId);
            msg.Write(name);
            msg.Write(color, false);
        }
    }

    /// <summary>
    ///     Sent by server to players. Contains extra information about a player (k/d at the moment).
    ///     Currently only sent inside Connected packet.
    /// </summary>
    public struct PlayerExtraInfo : IPacket
    {
        public PacketType Type => PacketType.PlayerExtraInfo;
        public int SequenceChannel => 10;

        public byte playerId;

        public short kills;
        public short deaths;

        public static PlayerExtraInfo Read(NetIncomingMessage msg)
        {
            return new PlayerExtraInfo
            {
                playerId = msg.ReadByte(),
                kills = msg.ReadInt16(),
                deaths = msg.ReadInt16()
            };
        }

        public void Write(NetOutgoingMessage msg)
        {
            msg.Write(playerId);
            msg.Write(kills);
            msg.Write(deaths);
        }
    }

    /// <summary>
    ///     Sent by server to all clients when a new player connects to server, containing only their id.
    /// </summary>
    public struct PlayerConnected : IPacket
    {
        public PacketType Type => PacketType.PlayerConnected;
        public int SequenceChannel => 0;

        public byte playerId;

        public static PlayerConnected Read(NetIncomingMessage msg)
        {
            return new PlayerConnected
            {
                playerId = msg.ReadByte()
            };
        }

        public void Write(NetOutgoingMessage msg)
        {
            msg.Write(playerId);
        }
    }

    /// <summary>
    ///     Sent by server to all clients when a player disconnects.
    /// </summary>
    public struct PlayerDisconnected : IPacket
    {
        public PacketType Type => PacketType.PlayerDisconnected;
        public int SequenceChannel => 0;

        public byte playerId;

        public static PlayerDisconnected Read(NetIncomingMessage msg)
        {
            return new PlayerDisconnected
            {
                playerId = msg.ReadByte()
            };
        }

        public void Write(NetOutgoingMessage msg)
        {
            msg.Write(playerId);
        }
    }

    /// <summary>
    ///     Sent by client to server when client kills a player.
    /// </summary>
    public struct PlayerKill : IPacket
    {
        public PacketType Type => PacketType.PlayerKill;
        public int SequenceChannel => 0;

        public byte killerId;
        public byte targetId;

        public static PlayerKill Read(NetIncomingMessage msg)
        {
            return new PlayerKill
            {
                killerId = msg.ReadByte(),
                targetId = msg.ReadByte()
            };
        }

        public void Write(NetOutgoingMessage msg)
        {
            msg.Write(killerId);
            msg.Write(targetId);
        }
    }

    /// <summary>
    ///     Sent by server to all clients when a player dies, their killer (can be themselves) and updated k/d for
    ///     respective players.
    /// </summary>
    public struct PlayerDeath : IPacket
    {
        public PacketType Type => PacketType.PlayerDeath;
        public int SequenceChannel => 0;

        public byte playerId;
        public byte killerId;

        public short playerDeaths;
        public short killerKills;

        public static PlayerDeath Read(NetIncomingMessage msg)
        {
            return new PlayerDeath
            {
                playerId = msg.ReadByte(),
                killerId = msg.ReadByte(),
                playerDeaths = msg.ReadInt16(),
                killerKills = msg.ReadInt16()
            };
        }

        public void Write(NetOutgoingMessage msg)
        {
            msg.Write(playerId);
            msg.Write(killerId);
            msg.Write(playerDeaths);
            msg.Write(killerKills);
        }
    }

    /// <summary>
    ///     Sent by client to server when they fire a shot. Then sent by server to all clients for shot visuals/sounds.
    /// </summary>
    public struct PlayerShoot : IPacket
    {
        public PacketType Type => PacketType.PlayerShoot;
        public int SequenceChannel => 0;

        public byte playerId;
        public Vector3 from;
        public Vector3 to;

        public static PlayerShoot Read(NetIncomingMessage msg)
        {
            return new PlayerShoot
            {
                playerId = msg.ReadByte(),
                from = msg.ReadVector3(),
                to = msg.ReadVector3()
            };
        }

        public void Write(NetOutgoingMessage msg)
        {
            msg.Write(playerId);
            msg.Write(from);
            msg.Write(to);
        }
    }

    /// <summary>
    ///     Sent by server to all clients. Contains current state of all players.
    /// </summary>
    public struct WorldState : IPacket
    {
        public PacketType Type => PacketType.WorldState;
        public int SequenceChannel => 1;

        public PlayerState?[] worldState;

        public static WorldState Read(NetIncomingMessage msg)
        {
            var length = msg.ReadByte();
            var worldState = new PlayerState?[length];

            for (var i = 0; i < length; i++)
            {
                if (!msg.ReadBoolean()) continue;

                var ps = PlayerState.Read(msg);
                worldState[ps.playerId] = ps;
            }

            return new WorldState
            {
                worldState = worldState
            };
        }

        public void Write(NetOutgoingMessage msg)
        {
            msg.Write((byte) worldState.Length);
            foreach (var state in worldState)
                if (!state.HasValue)
                {
                    msg.Write(false);
                }
                else
                {
                    msg.Write(true);
                    state.Value.Write(msg);
                }
        }
    }
}