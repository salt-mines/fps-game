﻿using System;
using Lidgren.Network;
using Networking.Packets;
using UnityEngine;
using Debug = System.Diagnostics.Debug;

namespace Networking
{
    internal sealed class NetworkClient : Client
    {
        private readonly NetClient client;

        internal NetworkClient(Loader loader)
        {
            Loader = loader;
            LevelManager = Loader.LevelManager;
            
            Loader.LevelLoaded += (o, s) => LevelLoaded(s);
            
            client = new NetClient(new NetPeerConfiguration(Constants.AppName)
            {
#if UNITY_EDITOR
                ConnectionTimeout = 600
#endif
            });

            client.Start();
        }
        
        private Loader Loader { get; }
        private LevelManager LevelManager { get; }

        public event EventHandler<StatusChangeEvent> StatusChanged;

        public override void Connect(string host, int port = Constants.AppPort)
        {
            client.Connect(host, port);
        }

        public override void Shutdown()
        {
            client.Shutdown("Bye");

            base.Shutdown();
        }

        protected override void InitializeFromServer(byte playerId, byte maxPlayers, string level)
        {
            base.InitializeFromServer(playerId, maxPlayers, level);

            LevelManager.ChangeLevel(level);
        }

        private void LevelLoaded(string level)
        {
            Debug.Assert(PlayerId != null, nameof(PlayerId) + " != null");

            Loaded = true;
            CreatePlayer(PlayerId.Value, true);
        }

        protected override PlayerInfo CreatePlayer(byte id, bool local = false)
        {
            var ply = base.CreatePlayer(id, local);
            ply.PlayerObject = NetworkManager.CreatePlayer(ply, local);
            return ply;
        }

        protected override void RemovePlayer(byte id)
        {
            if (Players[id] != null)
                NetworkManager.RemovePlayer(Players[id].PlayerObject);

            base.RemovePlayer(id);
        }

        private void Send<T>(T packet, NetDeliveryMethod method) where T : IPacket
        {
            client.SendMessage(Packet.Write(client, packet), method);
        }

        protected override void ProcessMessages()
        {
            NetIncomingMessage msg;
            while ((msg = client.ReadMessage()) != null)
            {
                switch (msg.MessageType)
                {
                    case NetIncomingMessageType.Data:
                        OnDataMessage(msg);
                        break;
                    case NetIncomingMessageType.StatusChanged:
                        OnStatusMessage(msg);
                        break;
                    case NetIncomingMessageType.VerboseDebugMessage:
                    case NetIncomingMessageType.DebugMessage:
                    case NetIncomingMessageType.WarningMessage:
                    case NetIncomingMessageType.ErrorMessage:
                        NetworkLog.HandleMessage("Client", msg);
                        break;
                }

                client.Recycle(msg);
            }
        }

        protected override void SendState()
        {
            Debug.Assert(PlayerId != null, nameof(PlayerId) + " != null");
            var ply = Players[PlayerId.Value];

            Send(ply.CreateMove(), NetDeliveryMethod.UnreliableSequenced);
        }

        public override void PlayerShoot(byte targetId)
        {
            Debug.Assert(PlayerId != null, nameof(PlayerId) + " != null");
            Send(new PlayerShoot
            {
                shooterId = PlayerId.Value,
                targetId = targetId
            }, NetDeliveryMethod.ReliableUnordered);
        }

        private void OnStatusMessage(NetIncomingMessage msg)
        {
            StatusChanged?.Invoke(this, new StatusChangeEvent
            {
                Status = (NetConnectionStatus) msg.ReadByte(),
                Reason = msg.ReadString()
            });
        }

        private void OnDataMessage(NetIncomingMessage msg)
        {
            var type = (PacketType) msg.ReadByte();

            switch (type)
            {
                case PacketType.Connected:
                    InitializeFromServer(Packets.Connected.Read(msg));
                    break;
                case PacketType.PlayerDisconnected:
                    OnPlayerDisconnected(PlayerDisconnected.Read(msg));
                    break;
                case PacketType.WorldState:
                    AddWorldState(WorldState.Read(msg).worldState);
                    break;
                case PacketType.PlayerDeath:
                    OnPlayerDeath(PlayerDeath.Read(msg));
                    break;
            }
        }

        private void OnPlayerDisconnected(PlayerDisconnected packet)
        {
            RemovePlayer(packet.playerId);
        }

        private void OnPlayerDeath(PlayerDeath packet)
        {
            Debug.Assert(PlayerId != null, nameof(PlayerId) + " != null");
            if (packet.playerId == PlayerId.Value) Players[PlayerId.Value].PlayerObject.Kill();

            UnityEngine.Debug.LogFormat("Player {0} killed Player {1}", packet.killerId, packet.playerId);
        }

        internal override void OnGUI(float x, float y)
        {
            GUI.Box(new Rect(x, y += 20, 140, 100), "Client");
            var rtt = 0;
            if (client.ServerConnection != null)
                rtt = Mathf.RoundToInt(client.ServerConnection.AverageRoundtripTime * 1000);

            GUI.Label(new Rect(x + 5, y += 20, 140, 20),
                $"Lag: {rtt} ms");
            GUI.Label(new Rect(x + 5, y += 20, 140, 20), $"Interp: {Interpolation * 1000} ms");
            if (PlayerId.HasValue && Players[PlayerId.Value] != null)
            {
                var ply = Players[PlayerId.Value];
                GUI.Label(new Rect(x + 5, y += 20, 140, 20), $"Pos: {ply.Position}");
                GUI.Label(new Rect(x + 5, y += 20, 140, 20), $"Rot: {ply.Rotation.eulerAngles}");
            }
        }

        public class StatusChangeEvent : EventArgs
        {
            public NetConnectionStatus Status { get; set; }
            public string Reason { get; set; }
        }
    }
}