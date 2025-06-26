using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDKBase.Network;

namespace OpenNID
{
    public class OpenNIDImportWindow : EditorWindow
    {
        public static OpenNIDImportWindow instance;
        private List<NetworkIDPair> importPairs;
        private bool createdGUI;
        
        private List<OpenNIDNetworkIDPairElement> scenePairElements = new List<OpenNIDNetworkIDPairElement>();
        private List<OpenNIDNetworkIDPairElement> importPairElements = new List<OpenNIDNetworkIDPairElement>();
        private ScrollView sceneNetworkIDScrollView;
        private ScrollView importNetworkIDScrollView;
        private Label importDetailsLabel;
        
        public static void OpenImportWindow(List<NetworkIDPair> pairs)
        {
            if (instance)
                instance.Close();

            instance = GetWindow<OpenNIDImportWindow>();
            instance.titleContent = new GUIContent("Open NID - Import Tool");
            instance.minSize = new Vector2(600, 128);
            instance.importPairs = pairs;
            instance.Refresh();
        }
        
        private void OnEnable()
        {
            Undo.undoRedoPerformed += Refresh;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Refresh;
        }

        private void OnFocus()
        {
            Refresh();
        }

        private void CreateGUI()
        {
            sceneNetworkIDScrollView = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden, style = { flexGrow = 1 } };
            importNetworkIDScrollView = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden, style = { flexGrow = 1 } };
            sceneNetworkIDScrollView.verticalScroller.valueChanged += OnSceneScrollViewChange;
            importNetworkIDScrollView.verticalScroller.valueChanged += OnImportScrollViewChange;

            VisualElement scenePairContainer = new VisualElement() { style = { flexGrow = 1 } };
            scenePairContainer.Add(new Label("Matched Scene Network IDs:"));
            scenePairContainer.Add(sceneNetworkIDScrollView);
            VisualElement importPairContainer = new VisualElement() { style = { flexGrow = 1 } };
            importPairContainer.Add(new Label("Imported Network IDs:"));
            importPairContainer.Add(importNetworkIDScrollView);
            
            VisualElement sceneImportScrollViewContainer = new VisualElement() { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };
            sceneImportScrollViewContainer.Add(scenePairContainer);
            sceneImportScrollViewContainer.Add(new Label("▶") { style = { unityTextAlign = TextAnchor.MiddleCenter, width = 32 }});
            sceneImportScrollViewContainer.Add(importPairContainer);
            
            rootVisualElement.Add(sceneImportScrollViewContainer);

            VisualElement importDetailsContainer = new VisualElement() { style = { flexDirection = FlexDirection.Row, flexGrow = 1, minHeight = 38 } };
            importDetailsLabel = new Label() { style = { flexGrow = 1, unityTextAlign = TextAnchor.LowerLeft, marginBottom = 12, marginLeft = 8 } };
            importDetailsContainer.Add(importDetailsLabel);
            importDetailsContainer.Add(new Button(() => TryImport()) 
                { text = "Import", style = { width = 64, height = 22, alignSelf = Align.FlexEnd, marginTop = 8, marginBottom = 8, marginLeft = 16, marginRight = 8 }});
            rootVisualElement.Add(importDetailsContainer);
            
            createdGUI = true;
            Refresh();
        }

        public void Refresh()
        {
            if (!createdGUI)
            {
                rootVisualElement.Clear();
                CreateGUI();
                return;
            }
            
            // TODO: Fix race condition issue causing window to lockup in editor with only Alt+F4 to close.
            // Likely recompilation
            if (sceneNetworkIDScrollView == null || importPairElements == null)
            {
                OnDisable();
                Close();
                return;
            }
                
            if (!OpenNIDManager.GetCurrentSceneDescriptor())
                return;

            DrawNetworkIDPairElements(GetRelevantScenePairs(), scenePairElements, sceneNetworkIDScrollView, false);
            DrawNetworkIDPairElements(importPairs, importPairElements, importNetworkIDScrollView, true);
            UpdateElementImportDetails();
            UpdateImportDetails();
        }

        public bool TryImport()
        {
            if (GetNetworkIDChangeCount() > 0 || GetNetworkObjectChangeCount() > 0)
            {
                if (!EditorUtility.DisplayDialog("Open NID - Confirm Import", 
                        "Some imported network IDs will change already existing scene network IDs. This could cause possible VRC PlayerObject Enabled Persistence data loss and/or break cross-platform networking." +
                        "\n\nAre you sure you want to overwrite?", "Import", "Cancel"))
                    return false;
            }

            List<NetworkIDPair> selectedPairs = GetSelectedNetworkIDPairsForImport();
            if (selectedPairs == null)
            {
                OpenNIDUtility.LogError("Failed to import network IDs! Could not retrieve selected network ID pairs for import.");
                return false;
            }

            int importCount = GetNewImportIDsAdded();
            int changeCount = GetNetworkIDChangeCount() + GetNetworkObjectChangeCount();
            
            OpenNIDManager.ImportNetworkIDsToScene(selectedPairs);

            string importDetailsText = "Successfully imported ";
            if (importCount > 0)
                importDetailsText += $"{importCount} new network ID{(importCount == 1 ? "" : "s")}";
            if (changeCount > 0)
                importDetailsText += string.IsNullOrWhiteSpace(importDetailsText) ? $"{changeCount} network ID change{(changeCount == 1 ? "" : "s")}" : $" and {changeCount} network ID change{(changeCount == 1 ? "" : "s")}";

            if (string.IsNullOrWhiteSpace(importDetailsText))
                importDetailsText = "Did not import any changes.";
            else
                importDetailsText += ".";
            
            OpenNIDUtility.LogMessage(importDetailsText);
            OpenNIDWindow.currentWindow?.Refresh();
            
            Close();
            return true;
        }

        private List<NetworkIDPair> GetRelevantScenePairs()
        {
            if (!OpenNIDManager.GetCurrentSceneDescriptor())
                return null;

            List<NetworkIDPair> scenePairs = OpenNIDManager.targetSceneDescriptor.NetworkIDCollection;
            importPairs ??= new List<NetworkIDPair>();

            List<NetworkIDPair> relevantScenePairs = new List<NetworkIDPair>();
            foreach (NetworkIDPair importPair in importPairs)
            {
                bool foundMatch = false;
                foreach (NetworkIDPair scenePair in scenePairs)
                {
                    if (importPair.ID == scenePair.ID || (importPair.gameObject && importPair.gameObject == scenePair.gameObject))
                    {
                        relevantScenePairs.Add(scenePair);
                        foundMatch = true;
                        break;
                    }
                }

                if (!foundMatch)
                    relevantScenePairs.Add(new NetworkIDPair() { ID = importPair.ID });
            }
            
            return relevantScenePairs;
        }

        private void DrawNetworkIDPairElements(List<NetworkIDPair> networkIDPairElements, List<OpenNIDNetworkIDPairElement> currentPairElements, VisualElement root, bool isSelectedForImport)
        {
            networkIDPairElements ??= new List<NetworkIDPair>();
            currentPairElements ??= new List<OpenNIDNetworkIDPairElement>();
            if (networkIDPairElements.Count > currentPairElements.Count)
            {
                List<NetworkIDPair> remainingNetworkIDPairs = networkIDPairElements.GetRange(currentPairElements.Count, networkIDPairElements.Count - currentPairElements.Count);
                foreach (NetworkIDPair pair in remainingNetworkIDPairs)
                {
                    OpenNIDNetworkIDPairElement pairElement = new OpenNIDNetworkIDPairElement(pair);
                    pairElement.onExpandValueChange += (o, b) => OnNetworkIDElementChange(pairElement);
                    pairElement.importMode = true;
                    pairElement.importSelected = isSelectedForImport;
                    pairElement.Refresh();
                    pairElement.importWasObjectMissing = !pairElement.objectField.value && pairElement.networkIDPair?.SerializedTypeNames is { Count: > 0 } && pairElement.networkIDPair.SerializedTypeNames[0][0] == '/';
                    pairElement.importModeSelectButton = new Button(() => OnImportSelectNetworkIDPairElement(pairElement)) 
                        { text = isSelectedForImport ? "Selected" : "Select", style = { display = DisplayStyle.None } };
                    pairElement.importModeSelectButton.SetEnabled(!isSelectedForImport);
                    pairElement.mainFoldout.Add(pairElement.importModeSelectButton);
                    root.Add(pairElement);
                    currentPairElements.Add(pairElement);
                }
            }
            
            for (int i = 0; i < currentPairElements.Count; i++)
            {
                if (i < networkIDPairElements.Count)
                {
                    currentPairElements[i].style.display = DisplayStyle.Flex;
                    if (currentPairElements[i].networkIDPair == null || networkIDPairElements.IndexOf(currentPairElements[i].networkIDPair) != i)
                        currentPairElements[i].networkIDPair = networkIDPairElements[i];
                    
                    currentPairElements[i].BringToFront();
                    currentPairElements[i].Refresh();
                    continue;
                }

                currentPairElements[i].style.display = DisplayStyle.None;
            }
        }

        private void OnImportSelectNetworkIDPairElement(OpenNIDNetworkIDPairElement pairElement)
        {
            if (scenePairElements == null || importPairElements == null || scenePairElements.Count != importPairElements.Count)
                return;

            int index = scenePairElements.IndexOf(pairElement);
            if (index >= 0 && index < scenePairElements.Count)
            {
                pairElement.importModeSelectButton.SetEnabled(false);
                pairElement.importModeSelectButton.text = "Selected";
                pairElement.importSelected = true;
                importPairElements[index].importModeSelectButton.SetEnabled(true);
                importPairElements[index].importModeSelectButton.text = "Select";
                importPairElements[index].importSelected = false;
                Refresh();
                return;
            }
            
            index = importPairElements.IndexOf(pairElement);
            if (index >= 0 && index < importPairElements.Count)
            {
                pairElement.importModeSelectButton.SetEnabled(false);
                pairElement.importModeSelectButton.text = "Selected";
                pairElement.importSelected = true;
                scenePairElements[index].importModeSelectButton.SetEnabled(true);
                scenePairElements[index].importModeSelectButton.text = "Select";
                scenePairElements[index].importSelected = false;
                Refresh();
            }
        }

        private List<NetworkIDPair> GetSelectedNetworkIDPairsForImport()
        {
            if (scenePairElements == null || importPairElements == null || scenePairElements.Count != importPairElements.Count)
                return null;

            return importPairElements.Select((t, i) => t.importSelected ? t.networkIDPair : scenePairElements[i].networkIDPair).ToList();
        }
        
        private void UpdateElementImportDetails()
        {
            if (scenePairElements == null || importPairElements == null || scenePairElements.Count != importPairElements.Count)
                return;

            for (int i = 0; i < scenePairElements.Count; i++)
            {
                NetworkIDPair sPair = scenePairElements[i].networkIDPair;
                NetworkIDPair iPair = importPairElements[i].networkIDPair;

                Color customColor = Color.clear;
                
                if (sPair.gameObject && sPair.gameObject != iPair.gameObject)
                    customColor = Color.red;
                else if (!sPair.gameObject)
                    customColor = new Color(0.1f, 0.5f, 0.5f);
                else if (sPair.gameObject && sPair.ID != iPair.ID)
                    customColor = Color.yellow;

                scenePairElements[i].customBoxColor = customColor;
                importPairElements[i].customBoxColor = customColor;

                scenePairElements[i].importModeSelectButton.style.display = customColor == Color.clear ? DisplayStyle.None : DisplayStyle.Flex;
                importPairElements[i].importModeSelectButton.style.display = customColor == Color.clear ? DisplayStyle.None : DisplayStyle.Flex;

                // Edge case during scene manipulation and Undos
                if (customColor == Color.clear && (scenePairElements[i].importSelected || !importPairElements[i].importSelected))
                    OnImportSelectNetworkIDPairElement(importPairElements[i]);
                
                scenePairElements[i].Refresh();
                importPairElements[i].Refresh();
            }
        }
        
        private void UpdateImportDetails()
        {
            if (importDetailsLabel == null)
                return;

            int newIDs = GetNewImportIDsAdded();
            int changedIDs = GetNetworkIDChangeCount();
            int changedObjs = GetNetworkObjectChangeCount();
            int missingImportedObjs = GetMissingImportedNetworkObjectCount();

            if (newIDs == 0 && changedIDs == 0 && changedObjs == 0 && missingImportedObjs == 0)
            {
                importDetailsLabel.text = "No changes detected.";
                return;
            }

            string changesText = "";
            if (newIDs > 0)
                changesText += $"{newIDs} new ID{(newIDs == 1 ? "" : "s")}";
            if (changedIDs > 0)
                changesText += string.IsNullOrWhiteSpace(changesText) ? $"{changedIDs} changed ID{(changedIDs == 1 ? "" : "s")}" : $", {changedIDs} changed ID{(changedIDs == 1 ? "" : "s")}";
            if (changedObjs > 0)
                changesText += string.IsNullOrWhiteSpace(changesText) ? $"{changedObjs} ID conflict{(changedObjs == 1 ? "" : "s")}" : $", {changedObjs} ID conflict{(changedObjs == 1 ? "" : "s")}";
            if (missingImportedObjs > 0)
                changesText += string.IsNullOrWhiteSpace(changesText) ? $"{missingImportedObjs} missing imported object{(missingImportedObjs == 1 ? "" : "s")}" : $", {missingImportedObjs} missing imported object{(missingImportedObjs == 1 ? "" : "s")}";
            if (!string.IsNullOrWhiteSpace(changesText))
                changesText += ".";

            int keepCount = GetKeepingSceneNetworkIDPairCount();
            if (keepCount > 0)
                changesText += $" - Keeping {keepCount} Scene ID{(keepCount == 1 ? "" : "s")}.";

            importDetailsLabel.text = changesText;
        }

        private int GetNewImportIDsAdded()
        {
            if (scenePairElements == null || importPairElements == null || scenePairElements.Count != importPairElements.Count)
                return 0;

            return scenePairElements.Count(pairElement => !pairElement.importSelected && !pairElement.networkIDPair.gameObject);
        }

        private int GetNetworkIDChangeCount()
        {
            if (scenePairElements == null || importPairElements == null || scenePairElements.Count != importPairElements.Count)
                return 0;

            return scenePairElements.Where((pairElement, i) => !pairElement.importSelected && pairElement.networkIDPair.ID != importPairElements[i].networkIDPair.ID && pairElement.networkIDPair.gameObject == importPairElements[i].networkIDPair.gameObject).Count();
        }

        private int GetNetworkObjectChangeCount()
        {
            if (scenePairElements == null || importPairElements == null || scenePairElements.Count != importPairElements.Count)
                return 0;

            return scenePairElements.Where((pairElement, i) => !pairElement.importSelected && pairElement.networkIDPair.gameObject && pairElement.networkIDPair.gameObject != importPairElements[i].networkIDPair.gameObject).Count();
        }

        private int GetMissingImportedNetworkObjectCount()
        {
            if (scenePairElements == null || importPairElements == null || scenePairElements.Count != importPairElements.Count)
                return 0;

            return importPairElements.Count(pairElement => pairElement.importSelected && !pairElement.networkIDPair.gameObject && !pairElement.objectField.value);
        }

        private int GetKeepingSceneNetworkIDPairCount()
        {
            if (scenePairElements == null)
                return 0;

            return scenePairElements.Count(pairElement => pairElement.importSelected);
        }

        private bool isAutoScroll;
        private void OnSceneScrollViewChange(float scrollValue)
        {
            if (isAutoScroll || importNetworkIDScrollView == null)
                return;

            isAutoScroll = true;
            importNetworkIDScrollView.verticalScroller.value = scrollValue;
            isAutoScroll = false;
        }
        
        private void OnImportScrollViewChange(float scrollValue)
        {
            if (isAutoScroll || sceneNetworkIDScrollView == null)
                return;
            
            isAutoScroll = true;
            sceneNetworkIDScrollView.verticalScroller.value = scrollValue;
            isAutoScroll = false;
        }

        private bool isAutoSetExpand;
        private void OnNetworkIDElementChange(OpenNIDNetworkIDPairElement pairElement)
        {
            if (isAutoSetExpand)
                return;

            int index = scenePairElements.IndexOf(pairElement);
            if (index >= 0 && index < importPairElements.Count && importPairElements[index] != null)
            {
                isAutoSetExpand = true;
                importPairElements[index].isExpanded = pairElement.isExpanded;
                isAutoSetExpand = false;
                return;
            }
            
            index = importPairElements.IndexOf(pairElement);
            if (index >= 0 && index < scenePairElements.Count && scenePairElements[index] != null)
            {
                isAutoSetExpand = true;
                scenePairElements[index].isExpanded = pairElement.isExpanded;
                isAutoSetExpand = false;
            }
        }
    }
}
