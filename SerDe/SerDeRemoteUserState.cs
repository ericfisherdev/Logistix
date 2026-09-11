using System.Collections.Generic;
using Logistix.ModPlayer;
using Logistix.Scripts;
using Logistix.Util;

namespace Logistix.SerDe
{
    /// <summary> remote user state  </summary>
    public class SerDeRemoteUserState : TocBasedSerDe
    {
        private readonly PlogPlayer _remotePlayer;

        public SerDeRemoteUserState(PlogPlayer remotePlayer)
        {
            _remotePlayer = remotePlayer;
            skipWritingVersion = true;
        }

        public override List<InstanceSerializer> GetSections()
        {
            var result = new List<InstanceSerializer>
            {
                _remotePlayer.personalLogisticManager,
                _remotePlayer.shippingManager,
                _remotePlayer.inventoryManager,
            };
            if (_remotePlayer.recycleWindowPersistence != null)
            {
                result.Add(_remotePlayer.recycleWindowPersistence);
            }
            else
            {
                Log.Debug($"Recycle window persistence not initted for remote player");
                result.Add(new RecycleWindowPersistence(_remotePlayer.playerId));
            }

            return result;
        }

        protected override int GetVersion() => -1;
    }
}