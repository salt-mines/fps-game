﻿using System.Collections.Generic;
using Lidgren.Network;
using Networking.Packets;
using TMPro;
using UnityEngine;

namespace Networking
{
    internal class Client : Peer
    {
        private readonly NetClient client;
        private readonly Dictionary<byte, NetworkActor> networkActors = new Dictionary<byte, NetworkActor>();
        private readonly LinkedList<TimedWorldState> stateBuffer = new LinkedList<TimedWorldState>();
        private TimedWorldState currentState;
        private float timeSinceLastState;

        public Client()
        {
#if UNITY_EDITOR
            peerConfig.SimulatedMinimumLatency = 0.08f;
            peerConfig.SimulatedRandomLatency = 0.02f;
#endif

            peer = client = new NetClient(peerConfig);
        }

        public float Interpolation { get; set; } = 0.1f;

        public byte PlayerId { get; private set; }
        private GameObject LocalActor { get; set; }

        public GameObject LocalPlayerPrefab { get; set; }

        public bool Connected => client.ConnectionStatus == NetConnectionStatus.Connected;

        public void Connect(string host, int port)
        {
            client.Connect(host, port);
        }

        protected override void OnDataMessage(NetIncomingMessage msg)
        {
            var type = (PacketType) msg.ReadByte();
            var packet = Packet.GetPacketFromType(type).Read(msg);

            //Debug.LogFormat("Packet [{0}]: {1}", this, type);

            switch (type)
            {
                case PacketType.Connected:
                    OnConnected((Connected) packet);
                    break;
                case PacketType.WorldState:
                    var ws = (WorldState) packet;
                    AddWorldState(new TimedWorldState
                    {
                        time = Time.time,
                        serverTime = ws.time,
                        worldState = ws.worldState
                    });
                    break;
            }
        }

        public override void Update()
        {
            if (!Connected) return;
            if (client.ServerConnection == null) return;

            var msBox = GameObject.Find("Latency");
            if (msBox == null) return;

            var value = msBox.transform.GetChild(1).GetComponent<TextMeshProUGUI>();
            if (value == null) return;


            value.text = $"{client.ServerConnection.AverageRoundtripTime / 1000:F0} ms";
        }

        public override void FixedUpdate()
        {
            if (!Connected) return;
            if (!LocalActor) return;
            
            UpdateWorldState();

            client.SendMessage(PlayerMove.Write(
                client.CreateMessage(),
                PlayerId,
                LocalActor.transform.position,
                LocalActor.transform.rotation
            ), NetDeliveryMethod.UnreliableSequenced);
        }

        private void OnConnected(Connected packet)
        {
            PlayerId = packet.playerId;

            LocalActor = Object.Instantiate(LocalPlayerPrefab);
        }

        private void AddWorldState(TimedWorldState state)
        {
            if (stateBuffer.Count > 0 && stateBuffer.Last.Value.time > state.time)
            {
                return;
            }

            stateBuffer.AddLast(state);
        }

        private PlayerState LerpPlayerState(PlayerState a, PlayerState b, float ratio)
        {
            return new PlayerState
            {
                playerId = a.playerId,
                position = Vector3.Lerp(a.position, b.position, ratio),
                rotation = Quaternion.Lerp(a.rotation, b.rotation, ratio)
            };
        }

        private void UpdatePlayerState(PlayerState state)
        {
            if (!networkActors.TryGetValue(state.playerId, out var actor))
            {
                Debug.LogFormat("Player {0} at {1}, rotated {2}", state.playerId, state.position, state.rotation);
                actor = Object.Instantiate(NetworkPlayerPrefab);
                networkActors.Add(state.playerId, actor);
            }

            actor.transform.SetPositionAndRotation(state.position, state.rotation);
        }

        private void UpdateWorldState()
        {
            var interpTime = Time.time - Interpolation;

            var from = stateBuffer.First;
            var to = stateBuffer.Last;

            while (to != null && to.Value.time <= interpTime)
            {
                from = to;
                to = from.Next;
                stateBuffer.RemoveFirst();
            }
            
            //var ratio = 
        }

        private struct TimedWorldState
        {
            public float time;
            public float serverTime;
            public List<PlayerState> worldState;
        }
    }
}