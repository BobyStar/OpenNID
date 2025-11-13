using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace OpenNID
{
    public class PinNetworkId : MonoBehaviour, IEditorOnly
    {
        [SerializeField] private int pinnedNetworkId = 0;
        [SerializeField] private bool locked = false;

        public int PinnedNetworkId => pinnedNetworkId;
        public bool IsLocked => locked;
    }
}
