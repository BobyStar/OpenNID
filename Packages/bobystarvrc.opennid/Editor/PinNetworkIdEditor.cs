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
        private SerializedProperty propLocked;

        private void OnEnable()
        {
            propPinnedNetworkId = serializedObject.FindProperty("pinnedNetworkId");
            propLocked = serializedObject.FindProperty("locked");
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

            // We are probably adding a new PinNetworkId component to an objext that already has a Network ID assigned.
            // we should set the default pinnedNetworkId to the existing Network ID.
            if (propPinnedNetworkId.intValue == 0 && isNetworkIdAssigned)
            {
                propPinnedNetworkId.intValue = realNetworkId;
                serializedObject.ApplyModifiedProperties();
            }

            int pinnedNetworkId = propPinnedNetworkId.intValue;

            // If the pinnedNetworkId is not valid, we should unlock it if it is locked.
            bool isPinIdValid = OpenNIDUtility.IsValidNetworkID(pinnedNetworkId);
            if (!isPinIdValid && propLocked.boolValue == true)
            {
                propLocked.boolValue = false;
                serializedObject.ApplyModifiedProperties();
            }

            bool isLocked = propLocked.boolValue;
            if (isLocked)
            {
                // todo: show a warning if there are multiple PinNetworkId components in the scene with the same pinnedNetworkId
                container.Add(new Label($"Pinned Network ID: {pinnedNetworkId}"));

                container.Add(
                    new Label($"Actual Network ID: {formatActualNetworkId(realNetworkId, pinnedNetworkId)}"));


                if (pinnedNetworkId != realNetworkId && realNetworkId > 0)
                {
                    container.Add(new HelpBox()
                    {
                        messageType = HelpBoxMessageType.Error,
                        text = "The pinned Network ID does not match the actual Network ID.\n" +
                               "If this component has persistence enabled, it may lose all persistent data!\n" +
                               "Be careful about how to resolve this.\n" +
                               "If the world has already been published, " +
                               "the pinned Network ID needs to match the ID assigned in the published build.",
                        style = { marginTop = 8, }
                    });
                    // todo: add a button to fix this by setting the actual Network ID to the pinned Network ID with a confirmation dialog
                }

                // add Unlock button
                Button unlockButton = new Button(() =>
                {
                    propLocked.boolValue = false;
                    serializedObject.ApplyModifiedProperties();
                    BuildUI();
                })
                {
                    text = "Unlock",
                    style = { marginTop = 16, }
                };
                container.Add(unlockButton);

                return;
            }

            // add Pinned Network ID property
            IntegerField pinnedIdField = new IntegerField("Pinned Network ID")
            {
                isDelayed = true,
            };
            pinnedIdField.SetValueWithoutNotify(pinnedNetworkId);
            pinnedIdField.RegisterValueChangedCallback(evt =>
            {
                propPinnedNetworkId.intValue = evt.newValue;
                serializedObject.ApplyModifiedProperties();
                BuildUI();
            });
            container.Add(pinnedIdField);

            container.Add(new Label($"Actual Network ID: {formatActualNetworkId(realNetworkId, -1)}"));

            if (!isPinIdValid)
            {
                HelpBox helpBox = new HelpBox()
                {
                    messageType = HelpBoxMessageType.Warning,
                    text = "The Network ID you requested is invalid.\n" +
                           "Please enter a valid Network ID between " +
                           OpenNIDManager.MIN_NETWORK_ID + " and " +
                           OpenNIDManager.MAX_NETWORK_ID + ".",
                };
                container.Add(helpBox);
                return;
            }

            // add Lock button
            HelpBox helpBoxLock = new HelpBox()
            {
                messageType = HelpBoxMessageType.Info,
                // todo: better describe what happens when locking the Network ID
                text = "Locking the Network ID will prevent it from being changed.\n" +
                       "You can unlock it later to change the pinned Network ID.",
                style = { marginTop = 16, }
            };
            container.Add(helpBoxLock);
            Button lockButton = new Button(() =>
            {
                propLocked.boolValue = true;
                serializedObject.ApplyModifiedProperties();
                OpenNIDManager.AssignSceneNetworkObjectsNewNetworkIDs(networkBehaviours, OpenNIDWindow.currentWindow);
                BuildUI();
            })
            {
                text = "Lock"
            };
            container.Add(lockButton);

            serializedObject.ApplyModifiedProperties();
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
