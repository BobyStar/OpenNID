using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDKBase.Network;

namespace OpenNID
{
    public class OpenNIDNetworkIDPairElement : Box
    {
        [Flags]
        public enum Status { Normal, NetworkIDPairMissing, NetworkObjectMissing, SerializedTypeNamesMissing = 4, ComponentMismatch = 8, PersistenceEnabled = 16, }
        public Status statusFlags { get; private set; }
        public bool HasIssue => HasStatus(Status.NetworkIDPairMissing) || HasStatus(Status.NetworkObjectMissing) || HasStatus(Status.SerializedTypeNamesMissing) || HasStatus(Status.ComponentMismatch);

        internal NetworkIDPair networkIDPair;

        private bool hasCreatedGUI;
        internal Foldout mainFoldout;
        internal ObjectField objectField;
        internal VisualElement componentInfoContainer;

        internal Color customBoxColor;
        
        // Managed Externally by Import Window
        internal bool importMode;
        internal bool importSelected;
        internal bool importWasObjectMissing;
        internal Button importModeSelectButton;

        public bool isExpanded
        {
            get => mainFoldout?.value ?? false;
            set
            {
                if (mainFoldout != null)
                    mainFoldout.value = value;
            }
        }

        public event EventHandler<bool> onExpandValueChange; 

        public OpenNIDNetworkIDPairElement(NetworkIDPair networkIDPair)
        {
            this.networkIDPair = networkIDPair;
            Refresh();
        }

        internal bool HasStatus(Status statusToCheck)
        {
            return (statusFlags & statusToCheck) == statusToCheck;
        }
        
        public Status GetPrimaryStatus()
        {
            if (importMode || statusFlags == Status.Normal)
                return Status.Normal;
            
            // Issues
            if (HasStatus(Status.NetworkIDPairMissing))
                return Status.NetworkIDPairMissing;
            if (HasStatus(Status.NetworkObjectMissing))
                return Status.NetworkObjectMissing;
            if (HasStatus(Status.SerializedTypeNamesMissing))
                return Status.SerializedTypeNamesMissing;
            if (HasStatus(Status.ComponentMismatch))
                return Status.ComponentMismatch;
            
            // No Issues
            if (HasStatus(Status.PersistenceEnabled))
                return Status.PersistenceEnabled;

            return statusFlags;
        }
        
        private void CreateGUI()
        {
            if (hasCreatedGUI || networkIDPair == null)
                return;
            hasCreatedGUI = true;
            
            RefreshBoxColor();
            
            mainFoldout = new Foldout() { value = false };
            mainFoldout.RegisterValueChangedCallback((b) => onExpandValueChange?.Invoke(this, b.newValue));
            RefreshFoldoutName();
            Add(mainFoldout);
            
            objectField = new ObjectField() { allowSceneObjects = true };
            objectField.RegisterValueChangedCallback(evt =>
            {
                if (importMode && importWasObjectMissing)
                    networkIDPair.gameObject = (evt.newValue as VRCNetworkBehaviour)?.gameObject;
                else
                    OpenNIDManager.AssignSceneObjectToFileNetworkObject(evt.newValue as VRCNetworkBehaviour, networkIDPair, false, this);
            });
            RefreshObjectField();
            mainFoldout.Add(objectField);
            
            RefreshComponentInfo();
        }

        public void Refresh()
        {
            statusFlags = Status.Normal;
            
            if (importMode)
            {
                if (OpenNIDManager.IsNetworkIDPairPersistenceEnabled(networkIDPair))
                    statusFlags |= Status.PersistenceEnabled;
                
                if (!hasCreatedGUI)
                {
                    CreateGUI();
                    return;
                }
                
                RefreshBoxColor();
                RefreshFoldoutName();
                RefreshObjectField();
                
                // temp
                if (componentInfoContainer != null)
                {
                    componentInfoContainer.Clear();
                    if (mainFoldout.Contains(componentInfoContainer))
                        mainFoldout.Remove(componentInfoContainer);
                    componentInfoContainer = null;
                }
                return;
            }
            
            if (networkIDPair == null)
                statusFlags = Status.NetworkIDPairMissing;
            else
            {
                if (networkIDPair != null && !networkIDPair.gameObject)
                    statusFlags |= Status.NetworkObjectMissing;
                if (networkIDPair != null && networkIDPair.SerializedTypeNames == null)
                    statusFlags |= Status.SerializedTypeNamesMissing;
                if (OpenNIDManager.HasComponentMismatchWithFile(networkIDPair))
                    statusFlags |= Status.ComponentMismatch;

                // Non-Error
                if (OpenNIDManager.IsNetworkIDPairPersistenceEnabled(networkIDPair))
                    statusFlags |= Status.PersistenceEnabled;
            }

            if (!hasCreatedGUI)
            {
                CreateGUI();
                return;
            }
            
            RefreshBoxColor();
            RefreshFoldoutName();
            RefreshObjectField();
            RefreshComponentInfo();
        }

        private void RefreshBoxColor()
        {
            OpenNIDUtility.SetBorderRadiusOfElement(this, 4);
            // TODO: Create lookup table for colors and support for light theme "Unity Pro Theme"
            Color backgroundColor = new Color(.2f, .2f, .2f);
            Color borderColor = new Color(.128f, .128f, .128f);
            if (HasIssue && !importMode)
            {
                backgroundColor = new Color(.15f, 0, 0);
                borderColor = Color.red;
            }
            else if (HasStatus(Status.PersistenceEnabled) && !importMode)
            {
                backgroundColor = new Color(0.15f, 0.2f, 0.25f);
                borderColor = new Color(0.1f, 0.5f, 0.5f);
            }

            if (customBoxColor != Color.clear)
            {
                Color.RGBToHSV(customBoxColor, out float h, out float s, out float v);
                v *= .2f;

                backgroundColor = Color.HSVToRGB(h, s, v);
                borderColor = customBoxColor;
            }

            style.backgroundColor = backgroundColor;
            OpenNIDUtility.SetBorderColorOfElement(this, borderColor);
        }

        private void RefreshFoldoutName()
        {
            mainFoldout.text = $"<b>{networkIDPair.ID}</b>";

            if (importMode && !networkIDPair?.gameObject && networkIDPair.SerializedTypeNames is { Count: > 0 } && networkIDPair.SerializedTypeNames[0][0] == '/')
                mainFoldout.text += $" - Path: {networkIDPair.SerializedTypeNames[0]}";
            else if (HasStatus(Status.NetworkObjectMissing))
                mainFoldout.text += "<b> - Missing Object!</b>";
            else if (networkIDPair.gameObject)
                mainFoldout.text += $" - {networkIDPair.gameObject.name}";
        }
        
        private void RefreshObjectField()
        {
            if (importMode)
            {
                if (importWasObjectMissing && networkIDPair?.SerializedTypeNames is { Count: > 0 } && networkIDPair?.SerializedTypeNames[0][0] == '/')
                {
                    objectField.objectType = typeof(VRCNetworkBehaviour);
                    objectField.SetEnabled(true);
                }
                else if (!importWasObjectMissing)
                {
                    objectField.objectType = typeof(GameObject);
                    objectField.SetValueWithoutNotify(networkIDPair?.gameObject);
                    objectField.SetEnabled(false);
                }
                    
                return;
            }
            
            objectField.objectType = HasStatus(Status.NetworkObjectMissing) ? typeof(VRCNetworkBehaviour) : typeof(GameObject);
            objectField.SetValueWithoutNotify(networkIDPair?.gameObject);
            objectField.SetEnabled(HasStatus(Status.NetworkObjectMissing));
        }
        
        private void RefreshComponentInfo()
        {
            if (componentInfoContainer != null)
            {
                // Possible memory leak? Maybe disable & recycle Labels instead?
                componentInfoContainer.Clear();
                mainFoldout.Remove(componentInfoContainer);
                componentInfoContainer = null;
            }
            
            if (networkIDPair.SerializedTypeNames == null && networkIDPair.gameObject)
            {
                componentInfoContainer = new Label("<b>Missing Component Names List!</b>");
                mainFoldout.Add(componentInfoContainer);
                mainFoldout.Add(new Button(() => OpenNIDManager.AssignSceneComponentsToFileComponentsOnObject(networkIDPair.gameObject, this)) { text = "Apply Scene Components" });
            }
            else
            {
                bool hasGameObject = networkIDPair.gameObject;
                List<VRCNetworkBehaviour> currentNetworkBehaviours = hasGameObject ? networkIDPair.gameObject.GetComponents<VRCNetworkBehaviour>().ToList() : null;
                List<VRCNetworkBehaviour> objectNetworkBehaviours = currentNetworkBehaviours != null ? new List<VRCNetworkBehaviour>(currentNetworkBehaviours) : null;
                List<VRCNetworkBehaviour> matchedNetworkBehaviours = new List<VRCNetworkBehaviour>();
                string componentNames = "";
                for (int i = 0; i < networkIDPair.SerializedTypeNames.Count; i++)
                {
                    // Compare Serialized to Current Scene
                    if (hasGameObject)
                    {
                        if (objectNetworkBehaviours.Count != 0)
                        {
                            Type fileType = OpenNIDUtility.GetTypeFromAllAssemblies(networkIDPair.SerializedTypeNames[i]);
                            if (fileType == null)
                                continue;
                            
                            Component[] foundNetworkBehaviours = networkIDPair.gameObject.GetComponents(fileType);
                            foreach (Component component in foundNetworkBehaviours)
                            {
                                VRCNetworkBehaviour networkBehaviour = component as VRCNetworkBehaviour;
                                
                                if (!networkBehaviour || matchedNetworkBehaviours.Contains(networkBehaviour) || !objectNetworkBehaviours.Contains(networkBehaviour))
                                    continue;
                                
                                matchedNetworkBehaviours.Add(networkBehaviour);
                                objectNetworkBehaviours.Remove(networkBehaviour);
                                break;
                            }
                        }
                    }
                    
                    string componentName = OpenNIDUtility.FilterComponentNameForUI(networkIDPair.SerializedTypeNames[i]);

                    if (i == 0)
                        componentNames = componentName;
                    else
                        componentNames += $", {componentName}";
                }

                if (string.IsNullOrWhiteSpace(componentNames))
                    componentNames = "No Components Found!";

                string sceneComponentNames = currentNetworkBehaviours != null ? OpenNIDUtility.GetCommaSeparatedComponentNames(currentNetworkBehaviours.ToArray() as Component[]) : "";
                if (string.IsNullOrWhiteSpace(sceneComponentNames))
                    sceneComponentNames = "No Components Found!";
                bool componentMismatch = OpenNIDManager.HasComponentMismatchWithFile(networkIDPair);
                if (componentMismatch)
                {
                    componentInfoContainer = new VisualElement();
                    componentInfoContainer.Add(new Label("<b>Component Mismatch!</b>"));
                    VisualElement fileComponentsLabel = new VisualElement();
                    fileComponentsLabel.style.flexDirection = FlexDirection.Row;
                    fileComponentsLabel.Add(new Label("File: ") { style = { width = 48 } });
                    fileComponentsLabel.Add(new Label(componentNames) { style = { whiteSpace = WhiteSpace.Normal }});

                    VisualElement sceneComponentsLabel = new VisualElement();
                    sceneComponentsLabel.style.flexDirection = FlexDirection.Row;
                    sceneComponentsLabel.Add(new Label("Scene: ") { style = { width = 48 } });
                    sceneComponentsLabel.Add(new Label(sceneComponentNames) { style = { whiteSpace = WhiteSpace.Normal }});
                    
                    componentInfoContainer.Add(fileComponentsLabel);
                    componentInfoContainer.Add(sceneComponentsLabel);
                    componentInfoContainer.Add(new Button(() => OpenNIDManager.AssignSceneComponentsToFileComponentsOnObject(networkIDPair.gameObject, this)) { text = "Use Scene Components" });
                }
                else
                {
                    componentInfoContainer = new VisualElement();
                    if (!HasIssue && HasStatus(Status.PersistenceEnabled))
                        componentInfoContainer.Add(new Label("<u>VRC Player Object Persistence Enabled</u>"));
                    componentInfoContainer.Add(new Label(HasStatus(Status.NetworkObjectMissing) ? $"File: {componentNames}" : componentNames) { style = { whiteSpace = WhiteSpace.Normal } });

                    if (!HasIssue && componentNames == "No Components Found!")
                        componentInfoContainer.Add(new Button(() => OpenNIDManager.TryRemoveFileNetworkIDPair(networkIDPair)) { text = "Remove Network ID Pair" });
                }
                
                if (componentInfoContainer != null)
                    mainFoldout.Add(componentInfoContainer);
            }
        }
    }
}