using System.Reflection;
using NebulaAPI;
using NebulaAPI.DataStructures;
using NebulaAPI.GameState;
using NebulaAPI.Interfaces;
using NebulaAPI.Networking;
using NebulaAPI.Packets;
using Logistix.ModPlayer;
using Logistix.Nebula.Client;
using Logistix.Util;

namespace Logistix.Nebula
{
    public class NebulaLoadState
    {
        public static NebulaLoadState instance;
        private static bool _isRegistered;
        private bool _clientStateLoadedFromServer;
        private bool _clientStateRequested;

        public static bool IsMultiplayerClient()
        {
            if (!NebulaModAPI.NebulaIsInstalled || NebulaModAPI.MultiplayerSession == null || NebulaModAPI.MultiplayerSession.LocalPlayer == null || !NebulaModAPI.IsMultiplayerActive)
            {
                return false;
            }

            return NebulaModAPI.MultiplayerSession.LocalPlayer.IsClient;
        }
        public static bool IsMultiplayerHost()
        {
            if (!NebulaModAPI.NebulaIsInstalled || NebulaModAPI.MultiplayerSession == null || NebulaModAPI.MultiplayerSession.LocalPlayer == null || !NebulaModAPI.IsMultiplayerActive)
            {
                return false;
            }

            return NebulaModAPI.MultiplayerSession.LocalPlayer.IsHost;
        }


        private const int ExpectedRegisteredPacketProcessorCount = 11;

        public static void Register()
        {
            if (_isRegistered)
                return;

            NebulaModAPI.RegisterPackets(Assembly.GetExecutingAssembly());
            _isRegistered = true;
            LogRegisteredPacketProcessorCount();
        }

        /// <summary>
        /// A silent drop in the registered <see cref="RegisterPacketProcessorAttribute"/> count
        /// is how a reflection-registration regression against a future Nebula API would present
        /// itself, so it's cheap to assert on load rather than discover during a session.
        /// </summary>
        private static void LogRegisteredPacketProcessorCount()
        {
            var registeredCount = 0;
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (type.IsDefined(typeof(RegisterPacketProcessorAttribute), false))
                    registeredCount++;
            }

            if (registeredCount == ExpectedRegisteredPacketProcessorCount)
            {
                Log.Info($"(NebulaLoadState) registered {registeredCount} packet processors as expected");
            }
            else
            {
                Log.Warn($"(NebulaLoadState) registered {registeredCount} packet processors, expected {ExpectedRegisteredPacketProcessorCount}");
            }
        }

        public static void Reset()
        {
            if (instance == null)
                return;
            instance._clientStateRequested = false;
            instance._clientStateLoadedFromServer = false;
            instance = null;
        }

        public bool IsWaitingClient()
        {
            if (!IsMultiplayerClient())
                return false;
            return !_clientStateLoadedFromServer;
        }

        public void SetClientStateLoaded()
        {
            _clientStateLoadedFromServer = true;
        }


        public void RequestStateFromHost()
        {
            if (_clientStateRequested || !IsMultiplayerClient())
                return;
            if (!NebulaModAPI.MultiplayerSession.IsGameLoaded)
                return;

            Log.Debug($"Requesting state from host {PlogPlayerId.ComputeLocalPlayerId()}");
            RequestClient.RequestStateFromHost();
            _clientStateRequested = true;
        }
    }
}
