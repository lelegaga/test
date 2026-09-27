using System;
using Sanguo.Network;
using UnityEngine;

namespace Sanguo.App
{
    /// <summary>
    /// Unity adapter for LAN play. The rules for hosting, joining and leaving live in the engine-free
    /// <see cref="LanController"/> (unit tested); this component only drives it every frame, feeds it
    /// app lifecycle events and supplies the Android multicast lock.
    /// </summary>
    public sealed class NetworkManager : MonoBehaviour
    {
        private LanController _lan;

        public LanController Lan => _lan ?? (_lan = Create());
        public LanRoomService Rooms => Lan.Rooms as LanRoomService;
        public GameClient Client => _lan?.Client;
        public bool IsHosting => _lan != null && _lan.IsHosting;

        public event Action<GameClient> ClientCreated;

        private LanController Create()
        {
            var gm = GameManager.Instance;
            var clock = new UnityClock();
            var transport = new TcpNetworkTransport();
            var controller = new LanController(transport, clock, new LanRoomService(transport, gm.Content, gm.Modes, clock), new AndroidNetworkLock());
            controller.ClientCreated += c => ClientCreated?.Invoke(c);
            return controller;
        }

        public void StartBrowsing() => Lan.StartBrowsing();

        public void StopBrowsing() => _lan?.StopBrowsing();

        /// <summary>Creates a room on this device and joins it as the host.</summary>
        public GameClient HostRoom(RoomSettings settings, string nickname, int avatarId) => Lan.HostRoom(settings, nickname, avatarId);

        public GameClient Join(string host, int port, string nickname, int avatarId, bool spectator) => Lan.Join(host, port, nickname, avatarId, spectator);

        /// <summary>Returns null for an invalid invite code.</summary>
        public GameClient JoinByInviteCode(string code, string nickname, int avatarId) => Lan.JoinByInviteCode(code, nickname, avatarId);

        public void LeaveCurrent() => _lan?.LeaveCurrent();

        private void Update() => _lan?.Update();

        private void OnApplicationPause(bool paused)
        {
            if (!paused) _lan?.OnResume();
        }

        private void OnDestroy()
        {
            _lan?.Dispose();
            _lan = null;
        }
    }
}
