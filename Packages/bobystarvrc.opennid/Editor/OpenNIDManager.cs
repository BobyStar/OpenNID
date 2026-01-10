using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.SDKBase.Network;
using Object = UnityEngine.Object;

namespace OpenNID
{
    static class OpenNIDManager
    {
        internal static VRC_SceneDescriptor targetSceneDescriptor;
        internal static VRCNetworkBehaviour[] sceneNetworkBehaviours;
        internal static Dictionary<GameObject, List<VRCNetworkBehaviour>> sceneNetworkObjects;

        internal const int MIN_NETWORK_ID = 10;
        internal const int MAX_NETWORK_ID = 1000000;

        // TODO: Create selector to select specific descriptor in scene(s)
        internal static VRC_SceneDescriptor GetCurrentSceneDescriptor()
        {
            if (targetSceneDescriptor)
                return targetSceneDescriptor;

            if (VRC_SceneDescriptor.Instance)
            {
                targetSceneDescriptor = VRC_SceneDescriptor.Instance;
                return VRC_SceneDescriptor.Instance;
            }

            VRC_SceneDescriptor sceneDescriptor = Object.FindObjectOfType<VRC_SceneDescriptor>();
            targetSceneDescriptor = sceneDescriptor;
            return sceneDescriptor;
        }
        
        internal static void PopulateSceneNetworkCollections()
        {
            sceneNetworkBehaviours = Object.FindObjectsOfType<VRCNetworkBehaviour>(true);
            sceneNetworkObjects = new Dictionary<GameObject, List<VRCNetworkBehaviour>>();
            
            foreach (VRCNetworkBehaviour networkBehaviour in sceneNetworkBehaviours)
            {
                if (sceneNetworkObjects.ContainsKey(networkBehaviour.gameObject))
                    continue;

                // Requires re-find of components on particular GameObject so component order is retained.
                sceneNetworkObjects.Add(networkBehaviour.gameObject, networkBehaviour.gameObject.GetComponents<VRCNetworkBehaviour>().ToList());
            }
        }

        internal static void ApplyAutoFixesToCurrentConflicts()
        {
            if (!GetCurrentSceneDescriptor())
                return;
            
            Undo.RecordObject(targetSceneDescriptor, "Auto Resolved Network ID Conflicts");

            // This could probably be done better.
            List<int> removeIndices = GetMissingNetworkIDPairs();
            if (removeIndices is { Count: > 0 })
            {
                int count = targetSceneDescriptor.NetworkIDCollection.Count - 1;
                for (int i = count; i >= 0; i--)
                {
                    if (i == removeIndices[^1])
                        targetSceneDescriptor.NetworkIDCollection.RemoveAt(i);
                }
            }

            foreach (NetworkIDPair pair in GetNetworkIDPairsWithMissingObjects())
                targetSceneDescriptor.NetworkIDCollection.Remove(pair);
            
            List<NetworkIDPair> mismatchedComponentPairs = GetNetworkIDPairsWithMismatchedComponents();
            mismatchedComponentPairs?.AddRange(GetNetworkIDPairsWithMissingSerializedTypeNames());

            foreach (NetworkIDPair pair in mismatchedComponentPairs)
                AssignSceneComponentsToFileComponentsOnObject(pair.gameObject);
            
            EditorUtility.SetDirty(targetSceneDescriptor.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(targetSceneDescriptor);
        }

        internal static void TryRemoveFileNetworkIDPair(NetworkIDPair pair)
        {
            if (EditorUtility.DisplayDialog("Open NID - Remove Network ID Pair", "Are you sure you want to remove this Network ID?", "Remove", "Cancel"))
                RemoveFileNetworkIDPair(pair);
        }
        
        internal static bool TryAutoResolveConflicts(bool isUponBuild = false)
        {
            List<int> missingNetworkIDPairs = OpenNIDManager.GetMissingNetworkIDPairs();
            List<NetworkIDPair> missingNetworkObjects = OpenNIDManager.GetNetworkIDPairsWithMissingObjects();
            List<NetworkIDPair> missingSerializedTypePairs = OpenNIDManager.GetNetworkIDPairsWithMissingSerializedTypeNames();
            List<NetworkIDPair> mismatchedComponents = OpenNIDManager.GetNetworkIDPairsWithMismatchedComponents();
            Dictionary<string, List<NetworkIDPair>> sharedNetworkObjectHierarchyPaths = OpenNIDManager.GetGroupsOfNetworkIDPairsWithSharedHierarchyPaths();
            List<NetworkIDPair> pinnedNetworkIdIssues = OpenNIDManager.GetNetworkIDPairsWithPinnedNetworkIdIssues();

            // These should not be auto-resolved, as the user should decide the soltion for each
            if (pinnedNetworkIdIssues is {Count: > 0})
            {
                string message = $"The following Network ID pairs have pinned Network ID issues:\n";
                foreach (NetworkIDPair pair in pinnedNetworkIdIssues)
                {
                    message += $"- {pair.ID} ({pair.gameObject.name})\n";
                }
                OpenNIDUtility.LogError(message);
                return false;
            }

            string detailsMessage = "";
            if (missingNetworkIDPairs is { Count: > 0 })
                detailsMessage = $"Remove {missingNetworkObjects.Count} null ID(s).";
            if (missingNetworkObjects is { Count: > 0 })
                detailsMessage += $"{(detailsMessage == "" ? "" : "\n")}Remove {missingNetworkObjects.Count} missing network object ID(s).";

            int count = missingSerializedTypePairs is { Count: > 0 } && mismatchedComponents is { Count: > 0 } ? missingSerializedTypePairs.Count + mismatchedComponents.Count :
                missingSerializedTypePairs is { Count: > 0 } ? missingSerializedTypePairs.Count :
                mismatchedComponents is { Count: > 0 } ? mismatchedComponents.Count : 0;
            
            if (count > 0)
                detailsMessage += $"{(detailsMessage == "" ? "" : "\n")}Apply {count} mismatched component(s) from scene.";

            if (detailsMessage == "")
            {
                OpenNIDUtility.LogError("Could not find anything to auto resolve!");
                return false;
            }

            if (!EditorUtility.DisplayDialog("Open NID - Auto Resolve Conflicts", 
                    $"Are you sure you want to automatically resolve all current network ID conflicts? {(isUponBuild? "The following actions will be performed. This cannot be undone!" : "The following actions will be performed and can be undone")}:\n\n{detailsMessage}", 
                    "Resolve Conflicts", "Cancel"))
                return false;
            
            ApplyAutoFixesToCurrentConflicts();
            
            if (sharedNetworkObjectHierarchyPaths is { Keys: { Count: > 0 } })
                OpenNIDUtility.LogWarning($"{sharedNetworkObjectHierarchyPaths.Keys.Count} path(s) point to multiple network objects. Issues may occur when Importing/Exporting IDs between platforms and scenes.");
            
            OpenNIDWindow.currentWindow?.Refresh();

            return true;
        }
        
        internal static NetworkIDPair GetNetworkIDPairFromGameObjectInFile(GameObject gameObject)
        {
            if (!GetCurrentSceneDescriptor())
                return null;

            foreach (NetworkIDPair pair in targetSceneDescriptor.NetworkIDCollection)
            {
                if (pair.gameObject == gameObject)
                    return pair;
            }

            return null;
        }

        #region Persistence
        internal static List<GameObject> GetCurrentNetworkObjectsUsingPersistence()
        {
            if (!GetCurrentSceneDescriptor() || targetSceneDescriptor.NetworkIDCollection == null)
                return null;

            return (from pair in targetSceneDescriptor.NetworkIDCollection where IsNetworkIDPairPersistenceEnabled(pair) select pair.gameObject).ToList();
        }
        
        internal static bool IsNetworkIDPairPersistenceEnabled(NetworkIDPair pair)
        {
            return pair.gameObject && pair.gameObject.GetComponentInParent<VRCPlayerObject>(true) && pair.gameObject.GetComponentInParent<VRCEnablePersistence>(true);
        }
        #endregion

        #region Scene Assignments
        internal static void ImportNetworkIDsToScene(List<NetworkIDPair> importNetworkIDPairs)
        {
            if (!GetCurrentSceneDescriptor() || importNetworkIDPairs?.Count == 0)
                return;

            Undo.RecordObject(targetSceneDescriptor, "Imported Network IDs");
            List<NetworkIDPair> scenePairs = targetSceneDescriptor.NetworkIDCollection;
            foreach (NetworkIDPair importPair in importNetworkIDPairs)
            {
                bool imported = false;
                for (int i = 0; i < scenePairs.Count; i++)
                {
                    NetworkIDPair scenePair = scenePairs[i];
                    if (scenePair == null)
                        continue;
                    if (scenePair.ID != importPair.ID && scenePair.gameObject != importPair.gameObject)
                        continue;
                    
                    // No changes, just transfer SerializedTypeNames collection.
                    if (scenePair.ID == importPair.ID && scenePair.gameObject == importPair.gameObject)
                        importPair.SerializedTypeNames = new List<string>(scenePair.SerializedTypeNames);
                    
                    scenePairs.Remove(scenePair);
                    scenePairs.Insert(i, importPair);
                    imported = true;
                }

                if (!imported)
                    scenePairs.Add(importPair);
            }
            
            EditorUtility.SetDirty(targetSceneDescriptor.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(targetSceneDescriptor);
        }
        
        internal static void ClearNetworkIDPairsExcluding(List<GameObject> networkObjectsToExcludeFromClear)
        {
            if (!GetCurrentSceneDescriptor() || targetSceneDescriptor.NetworkIDCollection == null)
                return;
            
            Undo.RecordObject(targetSceneDescriptor, "Cleared Scene Network IDs");
            if (networkObjectsToExcludeFromClear == null || networkObjectsToExcludeFromClear.Count == 0)
            {
                targetSceneDescriptor.NetworkIDCollection.Clear();
                EditorUtility.SetDirty(targetSceneDescriptor.gameObject);
                PrefabUtility.RecordPrefabInstancePropertyModifications(targetSceneDescriptor);
                OpenNIDWindow.currentWindow?.Refresh();
                return;
            }

            int count = targetSceneDescriptor.NetworkIDCollection.Count - 1;
            for (int i = count; i >= 0; i--)
            {
                if (targetSceneDescriptor.NetworkIDCollection[i] == null)
                {
                    targetSceneDescriptor.NetworkIDCollection.RemoveAt(i);
                    continue;
                }

                if (!targetSceneDescriptor.NetworkIDCollection[i].gameObject)
                {
                    targetSceneDescriptor.NetworkIDCollection.RemoveAt(i);
                    continue;
                }

                if (!networkObjectsToExcludeFromClear.Contains(targetSceneDescriptor.NetworkIDCollection[i].gameObject))
                {
                    targetSceneDescriptor.NetworkIDCollection.RemoveAt(i);
                    continue;
                }
            }
            
            EditorUtility.SetDirty(targetSceneDescriptor.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(targetSceneDescriptor);
            OpenNIDWindow.currentWindow?.Refresh();
        }
        
        internal static void RemoveFileNetworkIDPair(NetworkIDPair pair)
        {
            if (!GetCurrentSceneDescriptor())
                return;
            
            if (!targetSceneDescriptor.NetworkIDCollection.Contains(pair))
                return;
            
            Undo.RecordObject(targetSceneDescriptor, "Removed Network ID");
            targetSceneDescriptor.NetworkIDCollection.Remove(pair);
            PrefabUtility.RecordPrefabInstancePropertyModifications(targetSceneDescriptor);
            EditorUtility.SetDirty(targetSceneDescriptor.gameObject);
            
            OpenNIDWindow.currentWindow?.Refresh();
        }

        internal static GameObject GetGameObjectFromNetworkID(int networkID)
        {
            if (!GetCurrentSceneDescriptor())
                return null;

            foreach (NetworkIDPair pair in targetSceneDescriptor.NetworkIDCollection)
            {
                if (pair == null)
                    continue;
                if (pair.ID == networkID)
                    return pair.gameObject;
            }

            return null;
        }

        internal static int GetNextAvailableNetworkID()
        {
            if (!GetCurrentSceneDescriptor())
                return -1;

            targetSceneDescriptor.NetworkIDCollection ??= new List<NetworkIDPair>();

            HashSet<int> inUseNetworkIDs = new HashSet<int>(
                from pair in targetSceneDescriptor.NetworkIDCollection
                where pair != null
                select pair.ID
            );

            for (int i = MIN_NETWORK_ID; i < MAX_NETWORK_ID; i++)
            {
                if (!inUseNetworkIDs.Contains(i))
                    return i;
            }

            return -1;
        }

        internal static void AssignSceneNetworkObjectsNewNetworkIDs(List<VRCNetworkBehaviour> networkBehaviours, OpenNIDWindow refreshWindow = null)
        {
            if (!GetCurrentSceneDescriptor())
                return;

            Dictionary<GameObject, List<VRCNetworkBehaviour>> networkObjects = new Dictionary<GameObject, List<VRCNetworkBehaviour>>();
            Dictionary<GameObject, List<VRCNetworkBehaviour>> networkObjectsWithPin = new Dictionary<GameObject, List<VRCNetworkBehaviour>>();
            foreach (VRCNetworkBehaviour networkBehaviour in networkBehaviours)
            {
                if (!networkBehaviour || !networkBehaviour.gameObject)
                    continue;
                
                PinNetworkId pinNetworkId = networkBehaviour.gameObject.GetComponent<PinNetworkId>();
                if (pinNetworkId)
                {
                    if (!networkObjectsWithPin.TryAdd(networkBehaviour.gameObject, new List<VRCNetworkBehaviour>() { networkBehaviour }))
                        networkObjectsWithPin[networkBehaviour.gameObject].Add(networkBehaviour);
                }
                else
                {
                    if (!networkObjects.TryAdd(networkBehaviour.gameObject, new List<VRCNetworkBehaviour>() { networkBehaviour }))
                        networkObjects[networkBehaviour.gameObject].Add(networkBehaviour);
                }
            }

            // Get list of network IDs in use
            Undo.RecordObject(targetSceneDescriptor, "Regenerated Network IDs");
            targetSceneDescriptor.NetworkIDCollection ??= new List<NetworkIDPair>();
            List<int> inUseNetworkIDs = (from pair in targetSceneDescriptor.NetworkIDCollection where pair != null select pair.ID).ToList();
            List<GameObject> inFileNetworkObjects = (from pair in targetSceneDescriptor.NetworkIDCollection where pair != null && pair.gameObject select pair.gameObject).ToList();

            // process the pinned network objects first
            foreach (GameObject networkObjWithPin in networkObjectsWithPin.Keys)
            {
                if (inFileNetworkObjects.Contains(networkObjWithPin))
                    continue;

                PinNetworkId pinNetworkId = networkObjWithPin.GetComponent<PinNetworkId>();
                if (!pinNetworkId)
                {
                    continue;
                }

                int pinnedNetworkId = pinNetworkId.PinnedNetworkId;
                if (inUseNetworkIDs.Contains(pinnedNetworkId))
                {
                    OpenNIDUtility.LogError("Cannot assign Network ID to object because the pinned Network ID is already in use!");
                    refreshWindow?.Refresh();
                    return;
                }

                inUseNetworkIDs.Add(pinnedNetworkId);
                inFileNetworkObjects.Add(networkObjWithPin);
                targetSceneDescriptor.NetworkIDCollection.Add(new NetworkIDPair() { gameObject = networkObjWithPin, ID = pinnedNetworkId, });
                AssignSceneComponentsToFileComponentsOnObject(networkObjWithPin); // Marks Dirty Here
            }

            int networkID = MIN_NETWORK_ID;
            foreach (GameObject networkObject in networkObjects.Keys)
            {
                if (inFileNetworkObjects.Contains(networkObject))
                    continue;
                
                // Find next available network ID
                while (inUseNetworkIDs.Contains(networkID))
                {
                    networkID++;
                    if (networkID < MAX_NETWORK_ID)
                        continue;
                    
                    OpenNIDUtility.LogError("Ran out of Network IDs to assign!");
                    refreshWindow?.Refresh();
                    return;
                }
                
                inUseNetworkIDs.Add(networkID);
                inFileNetworkObjects.Add(networkObject);
                targetSceneDescriptor.NetworkIDCollection.Add(new NetworkIDPair() { gameObject = networkObject, ID = networkID, });
                AssignSceneComponentsToFileComponentsOnObject(networkObject); // Marks Dirty Here
            }
            
            refreshWindow?.Refresh();
        }
        
        internal static void AssignSceneObjectToFileNetworkObject(VRCNetworkBehaviour newNetworkObject, NetworkIDPair pair, bool forceAssign = false, OpenNIDNetworkIDPairElement refreshElement = null)
        {
            if (!newNetworkObject || pair == null)
                return;

            NetworkIDPair foundPair = GetNetworkIDPairFromGameObjectInFile(newNetworkObject.gameObject);
            if (foundPair != null)
            {
                OpenNIDUtility.LogError($"Cannot assign scene object \"{newNetworkObject.name}\" because it already has an assigned network ID: {foundPair.ID}!");
                refreshElement?.Refresh();
                return;
            }
            
            if (pair.gameObject)
            {
                if (!forceAssign)
                    OpenNIDUtility.LogError($"Cannot assign scene object \"{newNetworkObject.name}\" because the provided network ID {pair.ID} already has an object. This operation can be forced.");
                else
                {
                    Undo.RecordObject(targetSceneDescriptor, "Assigned New Network Object to Existing ID");
                    pair.gameObject = newNetworkObject.gameObject;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(targetSceneDescriptor);
                    EditorUtility.SetDirty(targetSceneDescriptor.gameObject);
                }
                
                refreshElement?.Refresh();
                return;
            }

            if (forceAssign)
            {
                Undo.RecordObject(targetSceneDescriptor, "Assigned New Network Object to Existing ID");
                pair.gameObject = newNetworkObject.gameObject;
                PrefabUtility.RecordPrefabInstancePropertyModifications(targetSceneDescriptor);
                EditorUtility.SetDirty(targetSceneDescriptor.gameObject);
                refreshElement?.Refresh();
                return;
            }
            
            // In case of "Missing" reference?
            GameObject prevGameObject = pair.gameObject;
            pair.gameObject = newNetworkObject.gameObject;
            bool hasComponentMismatchWithFile = HasComponentMismatchWithFile(pair);
            pair.gameObject = prevGameObject;
            if (hasComponentMismatchWithFile)
            {
                if (EditorUtility.DisplayDialog("Open NID - Network Object Assignment",
                        "The Network Object provided has a component mismatch with the file. Continue anyways?",
                        "Assign new Network Object", "Cancel"))
                {
                    Undo.RecordObject(targetSceneDescriptor, "Assigned New Network Object to Existing ID");
                    pair.gameObject = newNetworkObject.gameObject;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(targetSceneDescriptor);
                    EditorUtility.SetDirty(targetSceneDescriptor.gameObject);
                }
            }
            else
            {
                Undo.RecordObject(targetSceneDescriptor, "Assigned New Network Object to Existing ID");
                pair.gameObject = newNetworkObject.gameObject;
                PrefabUtility.RecordPrefabInstancePropertyModifications(targetSceneDescriptor);
                EditorUtility.SetDirty(targetSceneDescriptor.gameObject);
            }
            
            refreshElement?.Refresh();
        }
        
        internal static void AssignSceneComponentsToFileComponentsOnObject(GameObject networkObject, OpenNIDNetworkIDPairElement refreshElement = null)
        {
            if (sceneNetworkBehaviours == null || sceneNetworkObjects == null)
                PopulateSceneNetworkCollections();
            
            if (!targetSceneDescriptor || targetSceneDescriptor.NetworkIDCollection == null || sceneNetworkObjects == null)
                return;

            NetworkIDPair pair = OpenNIDUtility.GetNetworkIDPairFromGameObject(targetSceneDescriptor.NetworkIDCollection, networkObject);

            if (pair == null)
                return;

            Undo.RecordObject(targetSceneDescriptor, "Assigned Scene VRCNetworkBehaviour Components to File");
            pair.SerializedTypeNames ??= new List<string>();
            pair.SerializedTypeNames.Clear();

            if (sceneNetworkObjects.ContainsKey(networkObject))
            {
                for (int i = 0; i < sceneNetworkObjects[networkObject].Count; i++)
                    pair.SerializedTypeNames.Add(sceneNetworkObjects[networkObject][i].GetType().FullName);
            }

            PrefabUtility.RecordPrefabInstancePropertyModifications(targetSceneDescriptor);
            EditorUtility.SetDirty(targetSceneDescriptor.gameObject);
            refreshElement?.Refresh();
        }
        #endregion

        #region Import/Export File
        internal static List<NetworkIDPair> TryImportNetworkIDsFromFile(string filePath)
        {
            string importedJson = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(importedJson))
                return null;

            List<NetworkIDPair> importedPairs = new List<NetworkIDPair>();
            
            // Could be improved. Utility maybe?
            #region Manual JSON Parse
            string json = importedJson.Trim();
            if (!json.StartsWith('{'))
                return null;

            json = json.Remove(0, 1);
            json = json.Remove(json.Length - 1, 1);

            while (json.Length > 0)
            {
                NetworkIDPair pair = new NetworkIDPair();

                int cur = json.IndexOf('"');
                if (cur == -1)
                {
                    OpenNIDUtility.LogError($"Could not find current \" in file! Remaining text:\n{json}");
                    return null;
                }
                int next = json.IndexOf('"', cur + 1);
                if (next == -1)
                {
                    OpenNIDUtility.LogError($"Could not find next \" in file! Remaining text:\n{json}");
                    return null;
                }
                string numberText = json.Substring(cur + 1, (next - 1) - cur);
                if (!int.TryParse(numberText, out pair.ID))
                {
                    OpenNIDUtility.LogError($"Could not parse {numberText} as number! {cur} {next} Remaining text:\n{json}");
                    return null;
                }
                json = json.Remove(0, next + 1).Trim(); // Assuming ":
                
                cur = json.IndexOf('"');
                if (cur == -1)
                {
                    OpenNIDUtility.LogError($"Could not find current \" in file! Remaining text:\n{json}");
                    return null;
                }

                // Find next that isn't escaped.
                next = 1;
                while (true)
                {
                    if (next + 1 >= json.Length)
                    {
                        OpenNIDUtility.LogError($"Could not find next non-escaped \" in file, hit end of text! Remaining text:\n{json}");
                        return null;
                    }
                    next = json.IndexOf('"', next + 1);
                    if (next <= 1)
                    {
                        OpenNIDUtility.LogError($"Could not find next non-escaped \" in file! Remaining text:\n{json}");
                        return null;
                    }
                    
                    int backslashCount = 0;
                    for (int i = next - 1; i >= 0 && json[i] == '\\'; i--)
                        backslashCount++;
    
                    if (backslashCount % 2 == 0)
                        break;
                }
                
                pair.SerializedTypeNames = new List<string>() { Regex.Unescape(json.Substring(cur + 1, (next - 1) - cur)) };
                pair.gameObject = OpenNIDUtility.GetGameObjectFromHierarchyPath(pair.SerializedTypeNames[0]);
                importedPairs.Add(pair);
                json = json.Remove(0, next + 1).Trim();
            }
            #endregion
            
            return importedPairs;
        }
        
        internal static bool TryExportNetworkIDsToFile(List<NetworkIDPair> exportedIDPairs, string filePath)
        {
            if (exportedIDPairs == null || string.IsNullOrWhiteSpace(filePath))
                return false;

            string jsonText = "";
            foreach (NetworkIDPair pair in exportedIDPairs)
            {
                if (pair == null || !pair.gameObject)
                    continue;

                string text = $"\"{pair.ID}\":\"{OpenNIDUtility.GetHierarchyPath(pair.gameObject, true)}\"";
                if (string.IsNullOrWhiteSpace(jsonText))
                    jsonText = text;
                else
                    jsonText += $", {text}";
            }

            jsonText = $"{{{jsonText}}}";
            using StreamWriter sw = File.CreateText(filePath);
            sw.Write(jsonText);
            sw.Close();
            AssetDatabase.Refresh();
            
            return true;
        }
        #endregion
        
        #region Get Conflicts
        internal static List<int> GetMissingNetworkIDPairs()
        {
            if (!GetCurrentSceneDescriptor())
                return null;

            List<int> nullNetworkIDPairIndices = new List<int>();
            for (int i = 0; i < targetSceneDescriptor.NetworkIDCollection.Count; i++)
            {
                if (targetSceneDescriptor.NetworkIDCollection[i] == null)
                    nullNetworkIDPairIndices.Add(i);
            }

            return nullNetworkIDPairIndices;
        }

        internal static List<NetworkIDPair> GetNetworkIDPairsWithMissingObjects()
        {
            if (!GetCurrentSceneDescriptor())
                return null;

            return targetSceneDescriptor.NetworkIDCollection.Where(pair => !pair.gameObject).ToList();
        }

        internal static List<NetworkIDPair> GetNetworkIDPairsWithMissingSerializedTypeNames()
        {
            if (!GetCurrentSceneDescriptor())
                return null;

            return targetSceneDescriptor.NetworkIDCollection.Where(pair => pair.SerializedTypeNames == null).ToList();
        }
        
        internal static bool HasComponentMismatchWithFile(NetworkIDPair networkIDPair)
        {
            if (networkIDPair == null)
                return false;
            if (!networkIDPair.gameObject)
                return false;
            if (networkIDPair.SerializedTypeNames == null)
                return false;

            VRCNetworkBehaviour[] inSceneNetworkBehaviours = networkIDPair.gameObject.GetComponents<VRCNetworkBehaviour>();
            if ((inSceneNetworkBehaviours == null || inSceneNetworkBehaviours.Length == 0) && networkIDPair.SerializedTypeNames.Count == 0)
                return false;

            bool missingComponents = false;
            List<VRCNetworkBehaviour> matchedNetworkBehaviours = new List<VRCNetworkBehaviour>();
            List<VRCNetworkBehaviour> objectNetworkBehaviours = inSceneNetworkBehaviours?.ToList();
            foreach (string typeName in networkIDPair.SerializedTypeNames)
            {
                System.Type fileType = OpenNIDUtility.GetTypeFromAllAssemblies(typeName);
                if (fileType == null)
                    continue;
                
                Component[] foundNetworkBehaviours = networkIDPair.gameObject.GetComponents(fileType);
                if (objectNetworkBehaviours == null || objectNetworkBehaviours.Count == 0)
                {
                    missingComponents = true;
                    break;
                }
                
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

            // Check component order
            missingComponents |= matchedNetworkBehaviours.Count != networkIDPair.SerializedTypeNames.Count;
            if (!missingComponents && objectNetworkBehaviours.Count == 0)
            {
                for (int i = 0; i < matchedNetworkBehaviours.Count; i++)
                {
                    if (matchedNetworkBehaviours[i].GetType().FullName == networkIDPair.SerializedTypeNames[i])
                        continue;

                    missingComponents = true;
                    break;
                }
            }
            
            return missingComponents || objectNetworkBehaviours.Count > 0;
        }

        internal static bool HasPinnedNetworkIdMismatch(NetworkIDPair networkIDPair)
        {
            if (networkIDPair == null || !networkIDPair.gameObject)
                return false;

            PinNetworkId pinNetworkId = networkIDPair.gameObject.GetComponent<PinNetworkId>();
            if (!pinNetworkId || !pinNetworkId.IsLocked)
                return false;

            int pinnedNetworkId = pinNetworkId.PinnedNetworkId;
            if (!OpenNIDUtility.IsValidNetworkID(pinnedNetworkId))
                return true;

            return networkIDPair.ID != pinnedNetworkId;
        }

        internal static List<NetworkIDPair> GetNetworkIDPairsWithMismatchedComponents()
        {
            if (!GetCurrentSceneDescriptor())
                return null;

            return targetSceneDescriptor.NetworkIDCollection.Where(HasComponentMismatchWithFile).ToList();
        }

        internal static List<NetworkIDPair> GetNetworkIDPairsWithPinnedNetworkIdIssues()
        {
            if (!GetCurrentSceneDescriptor())
                return null;

            return targetSceneDescriptor.NetworkIDCollection.Where(HasPinnedNetworkIdMismatch).ToList();
        }

        internal static Dictionary<string, List<NetworkIDPair>> GetGroupsOfNetworkIDPairsWithSharedHierarchyPaths()
        {
            if (!GetCurrentSceneDescriptor())
                return null;

            Dictionary<string, List<NetworkIDPair>> duplicatePathsDictionary = new Dictionary<string, List<NetworkIDPair>>();
            foreach (NetworkIDPair pair in targetSceneDescriptor.NetworkIDCollection)
            {
                if (pair == null || !pair.gameObject)
                    continue;

                string networkObjectPath = targetSceneDescriptor.GetNetworkIDGameObjectPath(pair.gameObject);
                if (!duplicatePathsDictionary.TryAdd(networkObjectPath, new List<NetworkIDPair>() { pair }))
                    duplicatePathsDictionary[networkObjectPath].Add(pair);
            }

            string[] paths = duplicatePathsDictionary.Keys.ToArray();
            int count = duplicatePathsDictionary.Count - 1;
            for (int i = count; i >= 0; i--)
            {
                if (duplicatePathsDictionary[paths[i]].Count < 2)
                    duplicatePathsDictionary.Remove(paths[i]);
            }

            return duplicatePathsDictionary;
        }
        #endregion

        #region Checks
        internal static bool CheckForNetworkIDIssues(bool logs = true)
        {
            if (!GetCurrentSceneDescriptor())
                return true;
            PopulateSceneNetworkCollections();

            // TODO: Dialogue Popup to clean or ignore warning.
            if (logs && CheckForNetworkObjectDuplicateHierarchyPaths())
                OpenNIDUtility.LogWarning("Multiple network objects share the same transform hierarchy path. Check the Open NID tool for more details.");
                
            if (CheckForMissingNetworkIDPairs() || CheckForMissingNetworkObjects() || CheckForNetworkObjectComponentMismatches() || CheckForPinnedNetworkIdIssues())
                return true;
            
            return false;
        }

        private static bool CheckForMissingNetworkIDPairs()
        {
            return targetSceneDescriptor.NetworkIDCollection.Any(pair => pair == null);
        }
        
        private static bool CheckForMissingNetworkObjects()
        {
            return targetSceneDescriptor.NetworkIDCollection.Any(pair => !pair.gameObject);
        }
        
        private static bool CheckForNetworkObjectDuplicateHierarchyPaths()
        {
            // TODO: Return objects paired with paths that are duplicates. Should be treated as a warning and only problematic for import/export.
            Dictionary<string, List<GameObject>> pathNetworkObjectDictionary = new Dictionary<string, List<GameObject>>();
            foreach (NetworkIDPair pair in targetSceneDescriptor.NetworkIDCollection)
            {
                if (pair == null || !pair.gameObject)
                    continue;
                if (!pathNetworkObjectDictionary.TryAdd(targetSceneDescriptor.GetNetworkIDGameObjectPath(pair.gameObject), new List<GameObject>() { pair.gameObject }))
                    return true;
            }
            return false;
        }

        private static bool CheckForNetworkObjectComponentMismatches()
        {
            return targetSceneDescriptor.NetworkIDCollection.Any(HasComponentMismatchWithFile);
        }

        private static bool CheckForPinnedNetworkIdIssues()
        {
            List<NetworkIDPair> networkIDPairs = targetSceneDescriptor.NetworkIDCollection;

            List<PinNetworkId> pinNetworkIds = Object.FindObjectsOfType<PinNetworkId>(true).ToList();
            foreach (PinNetworkId pinNetworkId in pinNetworkIds)
            {
                if (!pinNetworkId || !pinNetworkId.gameObject)
                {
                    continue;
                }

                // todo: figure out what should really happen if we discover an unlocked PinNetworkId.
                if (!pinNetworkId.IsLocked)
                {
                    continue;
                }

                int pinnedNetworkId = pinNetworkId.PinnedNetworkId;

                if (!OpenNIDUtility.IsValidNetworkID(pinnedNetworkId))
                {
                    return true;
                }

                NetworkIDPair pair = OpenNIDUtility.GetNetworkIDPairFromGameObject(networkIDPairs, pinNetworkId.gameObject);
                if (pair == null)
                {
                    return true;
                }

                if (pair.ID != pinnedNetworkId)
                {
                    return true;
                }
            }

            return false;
        }
        #endregion
    }
}
