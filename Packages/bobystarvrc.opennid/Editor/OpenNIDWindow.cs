using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDKBase;
using VRC.SDKBase.Network;

namespace OpenNID
{
    public class OpenNIDWindow : EditorWindow
    {
        public static OpenNIDWindow currentWindow;
        private bool createdGUI;
        private float lastRefreshDuration;
        private List<OpenNIDNetworkIDPairElement> networkIDPairElements = new List<OpenNIDNetworkIDPairElement>();
        private ScrollView networkIDCollectionScrollView;
        private Dictionary<OpenNIDNetworkIDPairElement.Status, Foldout> sortedStatusFoldouts;
        private VisualElement networkIDCountContainer;
        private Button autoResolveButton;
        
        public enum SortMethod { Status, NetworkID, File, }
        internal SortMethod sortMode; // TODO: Hook into EditorPrefs Manager
        
        [MenuItem("Tools/Open NID")]
        public static void OpenToolWindow()
        {
            currentWindow ??= GetWindow<OpenNIDWindow>();
            currentWindow.titleContent = new GUIContent("Open NID Tool");
            currentWindow.minSize = new Vector2(284, 128);
            currentWindow.Focus();
        }

        [MenuItem("GameObject/Show Network IDs", false)]
        public static void PresentSelectedNetworkObjects()
        {
            ExpandAndShowNetworkObjects(Selection.gameObjects.ToList());
        }
        
        [MenuItem("GameObject/Show Network IDs", true)]
        public static bool PresentSelectedNetworkObjectsValidation()
        {
            return Selection.GetFiltered<VRCNetworkBehaviour>(SelectionMode.Editable).Length > 0;
        }

        public static void ExpandAndShowNetworkObjects(List<GameObject> networkObjects)
        {
            OpenToolWindow();

            if (!currentWindow)
            {
                OpenNIDUtility.LogError("Failed to find Open NID Window!");
                return;
            }
            
            currentWindow.PresentNetworkPairElementsFromObjects(networkObjects);
        }
        
        private void OnEnable()
        {
            Undo.undoRedoPerformed += Refresh;
        }

        private void OnDisable()
        {
            createdGUI = false;
            rootVisualElement.Clear();
            Undo.undoRedoPerformed -= Refresh;

            if (currentWindow == this)
                currentWindow = null;
        }

        private void OnDestroy()
        {
            if (currentWindow == this)
                currentWindow = null;
        }

        private void OnFocus()
        {
            // UX adjustment if list is long or slower hardware. Switches refresh only upon user intention if last was slow.
            if (lastRefreshDuration < .25f)
                Refresh();
        }

        public void CreateGUI()
        {
            if (createdGUI)
                return;

            if (rootVisualElement == null)
                return;
            
            if (!OpenNIDManager.GetCurrentSceneDescriptor())
            {
                OpenNIDUtility.LogError("No Scene Descriptor found!");
                GUIDrawErrorCannotFindSceneDescriptor(rootVisualElement);
                return;
            }

            createdGUI = true;
            GUIDrawHeader(rootVisualElement);
            networkIDPairElements.Clear();
            GUIDrawNetworkObjectElements(rootVisualElement, OpenNIDManager.targetSceneDescriptor.NetworkIDCollection);
            
            Refresh();
        }

        private void GUIDrawErrorCannotFindSceneDescriptor(VisualElement root)
        {
            root.Add(new HelpBox("No Scene Descriptor was found in the current scene!", HelpBoxMessageType.Error));
        }

        private void GUIDrawHeader(VisualElement root)
        {
            if (!OpenNIDManager.GetCurrentSceneDescriptor())
                return;
            
            GUIDrawNetworkIDCount(root, OpenNIDManager.targetSceneDescriptor.NetworkIDCollection.Count);

            // Import/Export Options
            VisualElement importExportNetworkIDsContainer = new VisualElement() { style = { flexDirection = FlexDirection.Row, minHeight = 22 } };
            Button importButton = new Button(() => TryImportNetworkIDs()) { text = "Import IDs", style = { flexGrow = 1 } };
            Button exportButton = new Button(() => TryExportNetworkIDs()) { text = "Export IDs", style = { flexGrow = 1 } };
            importExportNetworkIDsContainer.Add(importButton);
            importExportNetworkIDsContainer.Add(exportButton);
            root.Add(importExportNetworkIDsContainer);
            
            // Scene ID Options
            VisualElement networkIDLargeActionsContainer = new VisualElement() { style = { flexDirection = FlexDirection.Row, minHeight = 22 } };
            Button clearSceneIDsButton = new Button(() => TryClearSceneIDs()) { text = "Clear Network IDs", style = { flexGrow = 1 } };
            networkIDLargeActionsContainer.Add(clearSceneIDsButton);
            Button regenerateSceneIDsButton = new Button(() => TryRegenerateSceneIDs()) { text = "Regenerate Network IDs", style = { flexGrow = 1 } };
            networkIDLargeActionsContainer.Add(regenerateSceneIDsButton);
            root.Add(networkIDLargeActionsContainer);
            
            // View Options
            Button refreshButton = new Button(Refresh) { text = "Refresh" };
            root.Add(refreshButton);
            
            DropdownField sortDropdown = new DropdownField("Sort By:", Enum.GetNames(typeof(SortMethod)).ToList(), (int)sortMode);
            Label sortDropdownLabel = sortDropdown.Q<Label>();
            if (sortDropdownLabel != null)
                sortDropdownLabel.style.minWidth = 60;
            sortDropdown.RegisterValueChangedCallback(OnSortModeOptionChanged);
            root.Add(sortDropdown);
            
            ToolbarSearchField searchField = new ToolbarSearchField() { name = "Search", style = { width = StyleKeyword.Auto, maxHeight = 22 }};
            searchField.RegisterValueChangedCallback(OnSearchTermChanged);
            root.Add(searchField);
            
            // Auto Resolve Conflicts
            autoResolveButton = new Button(() => OpenNIDManager.TryAutoResolveConflicts()) { text = "Auto Resolve Conflicts" };
            root.Add(autoResolveButton);
        }
        public bool TryImportNetworkIDs()
        {
            string path = EditorUtility.OpenFilePanelWithFilters("Open NID - Import Network IDs", Application.dataPath, new string[] { "txt", "json" });
            if (string.IsNullOrWhiteSpace(path))
                return false;

            List<NetworkIDPair> importedNetworkIDs = OpenNIDManager.TryImportNetworkIDsFromFile(path);
            if (importedNetworkIDs == null)
            {
                OpenNIDUtility.LogError($"Failed to import Network IDs from file \"{path}\".");
                return false;
            }
            
            OpenNIDImportWindow.OpenImportWindow(importedNetworkIDs);
            return true;
        }

        public bool TryExportNetworkIDs()
        {
            if (!OpenNIDManager.GetCurrentSceneDescriptor())
                return false;

            if (OpenNIDManager.targetSceneDescriptor.NetworkIDCollection == null || OpenNIDManager.targetSceneDescriptor.NetworkIDCollection.Count == 0)
            {
                OpenNIDUtility.LogMessage("No network IDs in scene to export!");
                return false;
            }
            
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string path = EditorUtility.SaveFilePanelInProject("Open NID - Export Network IDs", $"{sceneName}_NetworkIDs", "json", "Save the current Network IDs to a file.");
            if (string.IsNullOrWhiteSpace(path))
                return false;

            OpenNIDManager.TryExportNetworkIDsToFile(OpenNIDManager.targetSceneDescriptor.NetworkIDCollection, path);
            
            return true;
        }

        public bool TryClearSceneIDs()
        {
            List<GameObject> persistenceNetworkGameObjects = OpenNIDManager.GetCurrentNetworkObjectsUsingPersistence();
            
            if (persistenceNetworkGameObjects is { Count: > 0 })
            {
                int choice = EditorUtility.DisplayDialogComplex("Open NID - Clear Network IDs",
                    "Are you sure you want to clear all network IDs in the current scene? This action can be undone. VRC Player Object Enabled Persistence Data could be lost.\n\n",
                    "All Network IDs", "Cancel", "Only Non-Persistent Network IDs");
                
                if (choice == 1)
                    return false;
                if (choice == 0)
                    persistenceNetworkGameObjects.Clear();
            }
            else if (!EditorUtility.DisplayDialog("Open NID - Clear Network IDs",
                         "Are you sure you want to clear all network IDs in the current scene? This action can be undone.\n\n",
                         "Clear Network IDs", "Cancel"))
            {
                return false;
            }
            
            OpenNIDManager.ClearNetworkIDPairsExcluding(persistenceNetworkGameObjects);
            OpenNIDManager.PopulateSceneNetworkCollections();

            return true;
        }
        
        // Maybe too much text to the user?
        public bool TryRegenerateSceneIDs()
        {
            List<GameObject> persistenceNetworkGameObjects = OpenNIDManager.GetCurrentNetworkObjectsUsingPersistence();
            
            if (persistenceNetworkGameObjects is { Count: > 0 })
            {
                int choice = EditorUtility.DisplayDialogComplex("Open NID - Regenerate Network IDs",
                    "Are you sure you want to clear and regenerate all network IDs in the current scene? This action can be undone. VRC Player Object Enabled Persistence Data could be lost.\n\n" +
                    "When making a cross-platform world, ensure to upload to all supported platforms to preserve cross-platform networking. " +
                    "You may need to import these network IDs to other scene(s) if you use different scenes for the same world.",
                    "All Network IDs", "Cancel", "Only Non-Persistent Network IDs");
                
                if (choice == 1)
                    return false;
                if (choice == 0)
                    persistenceNetworkGameObjects.Clear();
            }
            else if (!EditorUtility.DisplayDialog("Open NID - Regenerate Network IDs",
                    "Are you sure you want to clear and regenerate all network IDs in the current scene? This action can be undone.\n\n" +
                    "When making a cross-platform world, ensure to upload to all supported platforms to preserve cross-platform networking. " +
                    "You may need to import these network IDs to other scene(s) if you use different scenes for the same world.",
                    "Regenerate Network IDs", "Cancel"))
            {
                return false;
            }
            
            OpenNIDManager.ClearNetworkIDPairsExcluding(persistenceNetworkGameObjects);
            OpenNIDManager.PopulateSceneNetworkCollections();
            OpenNIDManager.AssignSceneNetworkObjectsNewNetworkIDs(OpenNIDManager.sceneNetworkBehaviours.ToList(), this);
            
            return true;
        }
        
        private void OnSortModeOptionChanged(ChangeEvent<string> evt)
        {
            if (string.IsNullOrWhiteSpace(evt.newValue) || !Enum.TryParse(evt.newValue, out SortMethod newSortMode))
                return;

            sortMode = newSortMode;
            Refresh();
        }
        
        private void OnSearchTermChanged(ChangeEvent<string> evt)
        {
            // TODO: Implement Filter Function
        }

        internal void PresentNetworkPairElementsFromObjects(List<GameObject> presentingNetworkObjects)
        {
            if (networkIDPairElements == null || networkIDPairElements.Count == 0)
                Refresh();
            
            List<OpenNIDNetworkIDPairElement> presentingElements = new List<OpenNIDNetworkIDPairElement>();
            foreach (OpenNIDNetworkIDPairElement pairElement in networkIDPairElements.Where(pairElement => pairElement.networkIDPair != null))
                presentingElements.AddRange(from networkObject in presentingNetworkObjects where networkObject == pairElement.networkIDPair.gameObject select pairElement);

            foreach (OpenNIDNetworkIDPairElement presentingElement in presentingElements)
            {
                presentingElement.isExpanded = true;
                if (sortMode == SortMethod.Status)
                {
                    if (sortedStatusFoldouts.ContainsKey(presentingElement.GetPrimaryStatus()))
                        sortedStatusFoldouts[presentingElement.GetPrimaryStatus()].value = true;
                }
            }

            if (presentingElements.Count == 0)
                return;
            
            networkIDCollectionScrollView.MarkDirtyRepaint();
            networkIDCollectionScrollView.schedule.Execute(() => networkIDCollectionScrollView.ScrollTo(presentingElements[0]));
        }
        
        internal void Refresh()
        {
            Stopwatch refreshWatch = Stopwatch.StartNew();
            OpenNIDManager.GetCurrentSceneDescriptor();

            if (!currentWindow)
                currentWindow = this;
            
            if (createdGUI && OpenNIDManager.targetSceneDescriptor?.NetworkIDCollection != null)
            {
                SortNetworkObjectElements(sortMode);
                List<NetworkIDPair> networkIDCollection = OpenNIDManager.targetSceneDescriptor.NetworkIDCollection;
                
                if (networkIDCollection.Count > networkIDPairElements.Count)
                {
                    List<NetworkIDPair> remainingNetworkIDPairs = networkIDCollection.GetRange(networkIDPairElements.Count, networkIDCollection.Count - networkIDPairElements.Count);
                    GUIDrawNetworkObjectElements(networkIDCollectionScrollView, remainingNetworkIDPairs);
                }
                
                for (int i = 0; i < networkIDPairElements.Count; i++)
                {
                    if (i < networkIDCollection.Count)
                    {
                        networkIDPairElements[i].style.display = DisplayStyle.Flex;
                        if (networkIDPairElements[i].networkIDPair == null || !networkIDCollection.Contains(networkIDPairElements[i].networkIDPair))
                            networkIDPairElements[i].networkIDPair = networkIDCollection[i];
                        networkIDPairElements[i].Refresh();

                        if (sortMode == SortMethod.Status && sortedStatusFoldouts.ContainsKey(networkIDPairElements[i].GetPrimaryStatus()))
                            sortedStatusFoldouts[networkIDPairElements[i].GetPrimaryStatus()].Add(networkIDPairElements[i]);
                        else
                            networkIDCollectionScrollView.Add(networkIDPairElements[i]);
                        
                        networkIDPairElements[i].BringToFront();
                        continue;
                    }

                    networkIDPairElements[i].style.display = DisplayStyle.None;
                }
            }
            else
            {
                refreshWatch.Stop();
                rootVisualElement.Clear();
                CreateGUI(); // Refresh recalled here.
                return;
            }

            GUIDrawNetworkIDCount(rootVisualElement, OpenNIDManager.targetSceneDescriptor.NetworkIDCollection.Count);
            autoResolveButton.style.display = OpenNIDManager.CheckForNetworkIDIssues(false) ? DisplayStyle.Flex : DisplayStyle.None;

            if (sortMode != SortMethod.Status || sortedStatusFoldouts == null)
            {
                refreshWatch.Stop();
                lastRefreshDuration = (float)refreshWatch.Elapsed.TotalSeconds; // Unlikely to be long enough for significant precision loss.
                return;
            }
            
            foreach (OpenNIDNetworkIDPairElement.Status statusType in sortedStatusFoldouts.Keys)
            {
                sortedStatusFoldouts[statusType].style.display = DisplayStyle.None;
                if (sortedStatusFoldouts[statusType].childCount <= 0)
                    continue;
                    
                foreach (VisualElement element in sortedStatusFoldouts[statusType].Children())
                {
                    if (element.style.display == DisplayStyle.Flex)
                        sortedStatusFoldouts[statusType].style.display = DisplayStyle.Flex;
                }
            }
            
            refreshWatch.Stop();
            lastRefreshDuration = (float)refreshWatch.Elapsed.TotalSeconds; // Unlikely to be long enough for significant precision loss.
        }
        
        private void GUIDrawNetworkIDCount(VisualElement root, int count)
        {
            if (networkIDCountContainer == null)
            {
                networkIDCountContainer = new Label();
                root.Add(networkIDCountContainer);
            }

            (networkIDCountContainer as Label).text = $"{count} Network ID{(count == 1 ? "" : "s")}";
        }

        private void SortNetworkObjectElements(SortMethod sortMethod)
        {
            RefreshSortedStatusFoldouts();
            switch (sortMethod)
            {
                case SortMethod.Status:
                    break;
                case SortMethod.NetworkID:
                    SortNetworkObjectElementsByNetworkID();
                    break;
                case SortMethod.File:
                    SortNetworkObjectElementsByFileIndices();
                    break;
            }
        }

        private void RefreshSortedStatusFoldouts()
        {
            if (sortedStatusFoldouts == null)
            {
                // Some may be very unlikely or impossible to see due to NetworkIDCollection being serialized.
                sortedStatusFoldouts = new Dictionary<OpenNIDNetworkIDPairElement.Status, Foldout>
                {
                    { OpenNIDNetworkIDPairElement.Status.Normal, new Foldout() { text = "Resolved", value = false } },
                    { OpenNIDNetworkIDPairElement.Status.NetworkIDPairMissing, new Foldout() { text = "<b>Empty Network ID Pair!</b>", value = false } },
                    { OpenNIDNetworkIDPairElement.Status.NetworkObjectMissing, new Foldout() { text = "<b>Missing Object!</b>", value = false } },
                    { OpenNIDNetworkIDPairElement.Status.ComponentMismatch, new Foldout() { text = "<b>Component Mismatch!</b>", value = false } },
                    { OpenNIDNetworkIDPairElement.Status.PersistenceEnabled, new Foldout() { text = "Persistence Enabled", value = false } },
                };
                sortedStatusFoldouts.Add(OpenNIDNetworkIDPairElement.Status.SerializedTypeNamesMissing, sortedStatusFoldouts[OpenNIDNetworkIDPairElement.Status.ComponentMismatch]);
                
                networkIDCollectionScrollView.Add(sortedStatusFoldouts[OpenNIDNetworkIDPairElement.Status.NetworkIDPairMissing]);
                networkIDCollectionScrollView.Add(sortedStatusFoldouts[OpenNIDNetworkIDPairElement.Status.NetworkObjectMissing]);
                networkIDCollectionScrollView.Add(sortedStatusFoldouts[OpenNIDNetworkIDPairElement.Status.ComponentMismatch]);
                networkIDCollectionScrollView.Add(sortedStatusFoldouts[OpenNIDNetworkIDPairElement.Status.PersistenceEnabled]);
                networkIDCollectionScrollView.Add(sortedStatusFoldouts[OpenNIDNetworkIDPairElement.Status.Normal]);
            }

            foreach (OpenNIDNetworkIDPairElement.Status statusType in sortedStatusFoldouts.Keys)
                sortedStatusFoldouts[statusType].style.display = sortMode == SortMethod.Status ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void SortNetworkObjectElementsByNetworkID()
        {
            if (networkIDPairElements == null || networkIDPairElements.Count == 0)
                return;

            networkIDPairElements.Sort(delegate(OpenNIDNetworkIDPairElement a, OpenNIDNetworkIDPairElement b)
            {
                if (a.networkIDPair == b.networkIDPair)
                    return 0;
                if (a.networkIDPair == null)
                    return -1;
                if (b.networkIDPair == null)
                    return 1;

                return a.networkIDPair.ID.CompareTo(b.networkIDPair.ID);
            });
        }

        private void SortNetworkObjectElementsByFileIndices()
        {
            if (networkIDPairElements == null || networkIDPairElements.Count == 0 || !OpenNIDManager.GetCurrentSceneDescriptor())
                return;

            List<OpenNIDNetworkIDPairElement> newSet = new List<OpenNIDNetworkIDPairElement>();
            foreach (NetworkIDPair pair in OpenNIDManager.targetSceneDescriptor.NetworkIDCollection)
            {
                foreach (OpenNIDNetworkIDPairElement networkIDPairElement in networkIDPairElements)
                {
                    if (networkIDPairElement.networkIDPair == pair)
                    {
                        newSet.Add(networkIDPairElement);
                        break;
                    }
                } 
            }

            networkIDPairElements = newSet;
        }
        
        private void GUIDrawNetworkObjectElements(VisualElement root, List<NetworkIDPair> pairs)
        {
            networkIDCollectionScrollView = new ScrollView(ScrollViewMode.Vertical);
            networkIDCollectionScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            root.Add(networkIDCollectionScrollView);
            
            foreach (NetworkIDPair pair in pairs)
                GUIDrawNetworkObjectElement(networkIDCollectionScrollView, pair);
        }
        
        private void GUIDrawNetworkObjectElement(VisualElement root, NetworkIDPair pair)
        {
            if (pair == null)
            {
                root.Add(new Label("<b>Null Network ID Pair!</b>"));
                return;
            }

            OpenNIDNetworkIDPairElement networkIDPairElement = new OpenNIDNetworkIDPairElement(pair);
            root.Add(networkIDPairElement);
            networkIDPairElements.Add(networkIDPairElement);
        }
    }    
}