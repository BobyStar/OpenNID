using UnityEditor;
using UnityEngine;

namespace OpenNID
{
    public class EditNetworkIDWizard : ScriptableWizard
    {
        public int NewNetworkID;
        private System.Action<int> OnApply;
        private bool _focusSet = false;
        private const string ctrlName = "NewNetworkID";

        public static void Create(string objName, int currentID, System.Action<int> onApply)
        {
            var wizard = DisplayWizard<EditNetworkIDWizard>($"Edit Network ID - {objName}", "Apply");
            wizard.NewNetworkID = currentID;
            wizard.OnApply = onApply;

            wizard.minSize = new Vector2(300, 120);
            wizard.maxSize = new Vector2(300, 120);
        }

        protected override bool DrawWizardGUI()
        {
            GUI.SetNextControlName(ctrlName);
            NewNetworkID = EditorGUILayout.IntField("New Network ID", NewNetworkID);
            if (!_focusSet)
            {
                GUI.FocusControl(ctrlName);
                _focusSet = true;
            }

            return true;
        }

        // This runs when the "Apply" button is clicked
        private void OnWizardCreate()
        {
            OnApply?.Invoke(NewNetworkID);
        }
    }
}
