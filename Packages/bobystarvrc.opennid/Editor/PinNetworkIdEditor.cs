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

            // We are probably adding a new PinNetworkId component to an object that already has a Network ID assigned.
            // we should set the default pinnedNetworkId to the existing Network ID.
            if (propPinnedNetworkId.intValue == 0 && isNetworkIdAssigned)
            {
                propPinnedNetworkId.intValue = realNetworkId;
                serializedObject.ApplyModifiedProperties();
            }

            // We are probably adding a new PinNetworkId component to a new object that does not have a Network ID assigned yet.
            // In this case, just suggest the next available Network ID.
            if (propPinnedNetworkId.intValue == 0 && !isNetworkIdAssigned)
            {
                propPinnedNetworkId.intValue = OpenNIDManager.GetNextAvailableNetworkID();
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
                container.Add(new Label($"Pinned Network ID: {pinnedNetworkId}"));

                container.Add(
                    new Label($"Actual Network ID: {formatActualNetworkId(realNetworkId, pinnedNetworkId)}"));

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

                    Button autoFixButton = new Button(() =>
                    {
                        if (!EditorUtility.DisplayDialog(
                                "Confirm Network ID Change",
                                $"Are you sure you want to set the Actual Network ID to {pinnedNetworkId}?\n\n" +
                                "If this world has already been uploaded, this object will lose persistent data unless its Actual Network ID is the same as in the uploaded version.\n\n" +
                                "If you already pinned the Network ID, you should change the Actual Network ID to match the Pinned Network ID. \n\n" +
                                "If you are adding this component for the first time, double-check that you are setting the correct Pinned Network ID.",
                                "Apply Pinned Network ID",
                                "Cancel"
                            ))
                        {
                            return;
                        }

                        OpenNIDManager.RemoveFileNetworkIDPair(pair);
                        OpenNIDManager.AssignSceneNetworkObjectsNewNetworkIDs(networkBehaviours,
                            OpenNIDWindow.currentWindow);
                        OpenNIDManager.AssignSceneComponentsToFileComponentsOnObject(pinNetworkId.gameObject);
                        BuildUI();
                    })
                    {
                        text = "Set Actual Network ID to Pinned Network ID",
                        style = { marginTop = 8, }
                    };
                    container.Add(autoFixButton);
                }

                // add Unlock button
                Button unlockButton = new Button(() =>
                {
                    if (!EditorUtility.DisplayDialog(
                            "Are you sure you want to unlock?",
                            "Usually you should not unlock the Pinned Network ID unless you pinned the wrong value by mistake.\n\n" +
                            "Once a world is published, changing the Network ID will cause persistent data loss for this object.",
                            "Unlock",
                            "Cancel"
                        ))
                    {
                        return;
                    }

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

            bool needsAssignment = realNetworkId != pinnedNetworkId;

            // add Lock button
            HelpBox helpBoxLock = new HelpBox()
            {
                messageType = HelpBoxMessageType.Info,
                text = "Locking the Network ID will prevent the Pinned ID from being changed.\n" +
                       "It will also prevent the world from being built if the Actual Network ID does not match the Pinned ID." +
                       (needsAssignment ? "\n\nLocking the component will attempt to assign the Pinned ID." : ""),
                style = { marginTop = 16, }
            };
            container.Add(helpBoxLock);
            Button lockButton = new Button(() =>
            {
                GameObject existingObject = OpenNIDManager.GetGameObjectFromNetworkID(pinnedNetworkId);
                bool alreadyInUse = existingObject != null && existingObject != pinNetworkId.gameObject;
                if (needsAssignment && alreadyInUse)
                {
                    EditorUtility.DisplayDialog(
                        "Pinned Network ID Already In Use",
                        $"The Pinned Network ID {pinnedNetworkId} is already in use by the GameObject \"{existingObject.name}\".\n" +
                        "This component can not be locked until the conflict is resolved.\n\n" +
                        "Either unassign the other game object or choose a different Pinned Network ID.\n\n" +
                        "If this world was already uploaded, be sure not to change the Network ID of any object that has persistent data.",
                        "Ok"
                    );
                    return;
                }

                propLocked.boolValue = true;
                serializedObject.ApplyModifiedProperties();
                OpenNIDManager.AssignSceneNetworkObjectsNewNetworkIDs(networkBehaviours, OpenNIDWindow.currentWindow);
                OpenNIDManager.AssignSceneComponentsToFileComponentsOnObject(pinNetworkId.gameObject);
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
