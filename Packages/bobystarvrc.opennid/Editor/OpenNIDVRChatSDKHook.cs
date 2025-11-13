using System;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor;
using VRC.SDK3.Editor;

namespace OpenNID
{
    public static class OpenNIDVRChatSDKHook
    {
        private static IVRCSdkBuilderApi builder;

        [InitializeOnLoadMethod]
        public static void RegisterSDKCallback()
        {
            VRCSdkControlPanel.OnSdkPanelEnable += AddBuildHook;
        }

        private static void AddBuildHook(object sender, EventArgs e)
        {
            if (VRCSdkControlPanel.TryGetBuilder(out builder))
            {
                builder.OnSdkBuildStart += OnBuildStarted;
            }
        }

        private static void OnBuildStarted(object sender, object target)
        {
            if (!OpenNIDManager.CheckForNetworkIDIssues())
                return;

            // TODO: Provide "Auto Fix" option. "Auto Fix" may require user input in the future (scene vs prefab modification for differentiating transform paths).
            int choice = EditorUtility.DisplayDialogComplex("Open NID - Network ID Conflict",
                "There are Network ID issues in the current scene. The build will be canceled if conflicts are not resolved.\n",
                "Auto Resolve", "Cancel Build", "Open NID tool");
            
            if (choice == 2)
            {
                OpenNIDWindow.OpenToolWindow();
            }
            if (choice > 0 || !OpenNIDManager.TryAutoResolveConflicts(true))
            {
                // TODO: Use Proper Cancel API (if one exists?) or find nicer exception method than a plain SystemException.
                throw new SystemException("Network IDs Require Resolving");
            }
        }
    }
}