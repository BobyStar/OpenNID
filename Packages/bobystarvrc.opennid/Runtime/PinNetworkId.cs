using UnityEngine;
using VRC.SDKBase;

namespace OpenNID
{
    public class PinNetworkId : MonoBehaviour, IEditorOnly
    {
        [SerializeField] private int pinnedNetworkId = 0;

        public int PinnedNetworkId => pinnedNetworkId;
    }
}
