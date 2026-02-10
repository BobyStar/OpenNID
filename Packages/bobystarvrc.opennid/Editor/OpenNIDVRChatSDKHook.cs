using UnityEditor;
using VRC.SDKBase.Editor.BuildPipeline;

namespace OpenNID
{
    public class OpenNIDVRChatSDKHook : IVRCSDKBuildRequestedCallback
    {
        public int callbackOrder => 100;

        public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
        {
            if (!OpenNIDManager.CheckForNetworkIDIssues())
                return true;

            int choice = EditorUtility.DisplayDialogComplex("Open NID - Network ID Conflict",
                "There are Network ID issues in the current scene. The build will be canceled if conflicts are not resolved.\n",
                "Auto Resolve", "Cancel Build", "Open NID tool");
            
            if (choice == 2)
                OpenNIDWindow.OpenToolWindow();
            else if (choice == 0 && OpenNIDManager.TryAutoResolveConflicts(true))
                return true;
            
            OpenNIDUtility.LogError("Network IDs Require Resolving");
            return false;
        }
    }
}