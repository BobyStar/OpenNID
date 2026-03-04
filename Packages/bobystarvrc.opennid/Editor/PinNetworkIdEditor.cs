using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Components;
using VRC.SDKBase.Network;

namespace OpenNID
{
    [CustomEditor(typeof(PinNetworkId))]
    public class PinNetworkIdEditor : HasNetworkIDBaseEditor
    {
        private SerializedProperty propPinnedNetworkId;

        private void OnEnable()
        {
            propPinnedNetworkId = serializedObject.FindProperty("pinnedNetworkId");
        }

        protected override void BuildUI()
        {
            container.Clear();
            serializedObject.Update();

            PinNetworkId pinNetworkId = (PinNetworkId)target;

            VRCEnablePersistence vrcEnablePersistence = pinNetworkId.GetComponent<VRCEnablePersistence>();
            if (!vrcEnablePersistence)
            {
                HelpBox helpBox = new HelpBox();
                helpBox.text = "This GameObject does not have a VRCEnablePersistence component.\n" +
                               "Usually, network ID pinning is not needed for non-persistent objects.";
                helpBox.messageType = HelpBoxMessageType.Info;
                container.Add(helpBox);
            }


            int realNetworkId = -1;
            bool isNetworkIdAssigned = false;
            NetworkIDPair pair = OpenNIDUtility.GetNetworkIDPairFromGameObject(sceneDescriptor.NetworkIDCollection,
                pinNetworkId.gameObject);
            if (pair != null)
            {
                realNetworkId = pair.ID;
                isNetworkIdAssigned = true;
            }

            // We are probably adding a new PinNetworkId component to an object that already has a Network ID assigned.
            // we should set the default pinnedNetworkId to the existing Network ID.
            if (propPinnedNetworkId.intValue == 0 && isNetworkIdAssigned)
            {
                propPinnedNetworkId.intValue = realNetworkId;
                serializedObject.ApplyModifiedProperties();
            }

            // We are probably adding a new PinNetworkId component to a new object that does not have a Network ID assigned yet.
            // In this case, just assign the next available Network ID.
            if (propPinnedNetworkId.intValue == 0 && !isNetworkIdAssigned)
            {
                propPinnedNetworkId.intValue = OpenNIDManager.GetNextAvailableNetworkID();
                serializedObject.ApplyModifiedProperties();
                // Automatically assign the Pinned Network ID to the object.
                OpenNIDManager.AssignSceneNetworkObjectsNewNetworkIDs(networkBehaviours, OpenNIDWindow.currentWindow);
                OpenNIDManager.AssignSceneComponentsToFileComponentsOnObject(pinNetworkId.gameObject);
                BuildUI();
                return;
            }

            int pinnedNetworkId = propPinnedNetworkId.intValue;

            // If the pinnedNetworkId is not valid, warn the user. This should be impossible though.
            bool isPinIdValid = OpenNIDUtility.IsValidNetworkID(pinnedNetworkId);
            if (!isPinIdValid)
            {
                container.Add(new HelpBox()
                {
                    messageType = HelpBoxMessageType.Error,
                    text = $"This object has an invalid Pinned Network ID: {pinnedNetworkId}.\n" +
                           "This should not be possible. Please remove this component.",
                    style = { marginTop = 8, }
                });
                return;
            }

            container.Add(new Label($"Pinned Network ID: {pinnedNetworkId}"));

            container.Add(new Label($"Actual Network ID: {formatActualNetworkId(realNetworkId, pinnedNetworkId)}"));

            bool isMismatched = pinnedNetworkId != realNetworkId;

            GameObject existingObject = OpenNIDManager.GetGameObjectFromNetworkID(pinnedNetworkId);
            bool alreadyInUse = existingObject != null && existingObject != pinNetworkId.gameObject;

            if (isMismatched && alreadyInUse)
            {
                container.Add(new HelpBox()
                {
                    messageType = HelpBoxMessageType.Error,
                    text = $"The pinned Network ID is already in use by another object ({existingObject.name})\n" +
                           "You will need to resolve this manually to avoid persistent data loss!\n\n" +
                           "Either this or the other object needs to have its Network ID changed.",
                    style = { marginTop = 8, }
                });
            }

            if (isMismatched && realNetworkId > 0)
            {
                container.Add(new HelpBox()
                {
                    messageType = HelpBoxMessageType.Error,
                    text = "The pinned Network ID does not match the actual Network ID.\n" +
                           "If this component has persistence enabled, it may lose all persistent data!\n\n" +
                           "Be careful about how to resolve this.\n" +
                           "If the world has already been published, " +
                           "the pinned Network ID needs to match the ID assigned in the published build.",
                    style = { marginTop = 8, }
                });

                Button showInTool = new Button(() =>
                {
                    OpenNIDWindow.ExpandAndShowNetworkObjects(new List<GameObject> { pinNetworkId.gameObject });
                })
                {
                    text = "Show in Open NID Tool",
                    style = { marginTop = 8, }
                };
                container.Add(showInTool);
            }
        }

        private string formatActualNetworkId(int networkId, int pinnedId)
        {
            if (networkId <= 0)
            {
                return "<color=yellow>Not assigned</color>";
            }

            if (pinnedId > 0)
            {
                if (networkId != pinnedId)
                {
                    return $"<color=red>{networkId}</color>";
                }

                return $"<color=green>{networkId}</color>";
            }

            return networkId.ToString();
        }
    }
}
