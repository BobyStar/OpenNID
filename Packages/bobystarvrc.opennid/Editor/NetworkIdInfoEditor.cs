using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDKBase;
using VRC.SDKBase.Network;

namespace OpenNID
{
    [CustomEditor(typeof(NetworkIdInfo))]
    public class NetworkIdInfoEditor : HasNetworkIDBaseEditor
    {
        protected override void BuildUI()
        {
            container.Clear();
            serializedObject.Update();

            NetworkIdInfo networkIdInfo = (NetworkIdInfo)target;
            int networkId = -1;

            NetworkIDPair pair = OpenNIDUtility.GetNetworkIDPairFromGameObject(sceneDescriptor.NetworkIDCollection,
                networkIdInfo.gameObject);
            bool showAssignButton = false;

            if (pair != null)
            {
                networkId = pair.ID;
            }
            else
            {
                showAssignButton = true;
            }

            Label infoLabel = new Label($"Network ID: {formatNetworkId(networkId)}");
            container.Add(infoLabel);

            if (showAssignButton)
            {
                Button assignButton = new Button(() =>
                {
                    if (!showAssignButton)
                    {
                        Debug.LogWarning("Network ID already assigned to this GameObject.");
                        return;
                    }

                    if (networkBehaviours.Count == 0)
                    {
                        Debug.LogWarning("No VRCNetworkBehaviour found on this GameObject.");
                        return;
                    }

                    OpenNIDManager.AssignSceneNetworkObjectsNewNetworkIDs(networkBehaviours,
                        OpenNIDWindow.currentWindow);
                    EditorUtility.SetDirty(sceneDescriptor);
                    AssetDatabase.SaveAssets();
                    BuildUI();
                })
                {
                    text = "Assign Network ID"
                };
                container.Add(assignButton);
            }
        }

        private string formatNetworkId(int networkId)
        {
            if (networkId <= 0)
            {
                return "<color=yellow>Not assigned</color>";
            }

            return networkId.ToString();
        }
    }
}
