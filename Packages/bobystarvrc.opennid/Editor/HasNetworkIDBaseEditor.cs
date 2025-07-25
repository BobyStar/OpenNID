using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDKBase;
using VRC.SDKBase.Network;

namespace OpenNID
{
    public abstract class HasNetworkIDBaseEditor : UnityEditor.Editor
    {
        protected VisualElement container;
        protected List<VRCNetworkBehaviour> networkBehaviours;
        protected VRC_SceneDescriptor sceneDescriptor;

        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            var root = new VisualElement();
            container = new VisualElement();
            root.Add(container);
            if (InitialChecks())
            {
                BuildUI();
            }

            return root;
        }

        protected bool InitialChecks()
        {
            networkBehaviours = new List<VRCNetworkBehaviour>();
            ((Component)target).GetComponents(networkBehaviours);
            if (networkBehaviours.Count == 0)
            {
                HelpBox helpBox = new HelpBox();
                helpBox.text = "No VRCNetworkBehaviour found on this GameObject.\n" +
                               "This GameObject does not need a Network ID.";
                helpBox.messageType = HelpBoxMessageType.Info;
                container.Clear();
                container.Add(helpBox);
                return false;
            }

            sceneDescriptor = OpenNIDManager.GetCurrentSceneDescriptor();
            if (!sceneDescriptor)
            {
                HelpBox helpBox = new HelpBox();
                helpBox.text = "No VRC_SceneDescriptor found in the scene.\n" +
                               "Network IDs will not be assigned without a scene descriptor.";
                helpBox.messageType = HelpBoxMessageType.Warning;
                container.Clear();
                container.Add(helpBox);
                return false;
            }

            return true;
        }

        protected abstract void BuildUI();
    }
}
