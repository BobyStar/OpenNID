using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using VRC.SDK3.Editor;
using VRC.SDKBase.Editor;
using VRC.SDKBase.Network;

namespace OpenNID
{
    public static class OpenNIDUtility
    {
        internal const string prefix = "[Open NID]";
        
        internal static void LogMessage(object message) => Debug.Log($"{prefix} {message}");
        internal static void LogWarning(object message) => Debug.LogWarning($"{prefix} {message}");
        internal static void LogError(object message) => Debug.LogError($"{prefix} {message}");

        internal static Type GetTypeFromAllAssemblies(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return null;
            
            Type type = Type.GetType(typeName);
            if (type != null)
                return type;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null)
                    return type;
            }

            return null;
        }
        
        internal static string EscapeStringInput(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "null";

            StringBuilder sb = new StringBuilder(input.Length);

            foreach (char c in input)
            {
                switch (c)
                {
                    case '\\':
                        sb.Append(@"\\");
                        break;
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (c is <= '\u001F' or '\u007F')
                            sb.AppendFormat("\\u{0:X4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }
		
            return sb.ToString();
        }
        
        internal static string GetHierarchyPath(Transform target, bool includeEscapeCharacters = false)
        {
            if (!target)
                return null;

            string path = target.name;
            if (includeEscapeCharacters)
                path = EscapeStringInput(path);
            while (target)
            {
                target = target.parent;
                if (!target)
                    break;

                path = includeEscapeCharacters ? $"{EscapeStringInput(target.name)}/{path}" : $"{target.name}/{path}";
            }

            return $"/{path}";
        }
        
        internal static string GetHierarchyPath(GameObject target, bool includeEscapeCharacters = false) => !target ? null : GetHierarchyPath(target.transform, includeEscapeCharacters);
        
        internal static GameObject GetGameObjectFromHierarchyPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            path = path.Trim();
            if (path.Length > 1 && path[0] == '/')
                path = path.Remove(0, 1);

            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject rootObject in roots)
            {
                if (!rootObject || !rootObject.transform)
                    continue;

                if (rootObject.name == path)
                    return rootObject;
                
                string subPath = path.Replace(rootObject.name + "/", "");
                Transform t = rootObject.transform.Find(subPath);
                if (t)
                    return t.gameObject;
            }

            return null;
        }
        
        /// <summary>
        /// UX filter for presenting a more user-friendly name.
        /// </summary>
        /// <param name="componentName">The name of the component to be filtered.</param>
        /// <returns>A component name filtered for displaying to the user.</returns>
        internal static string FilterComponentNameForUI(string componentName)
        {
            if (componentName.Contains("VRC."))
                componentName = componentName.Remove(0, componentName.LastIndexOf('.') + 1);
            
            return componentName;
        }

        /// <summary>
        /// Creates a comma separated string containing the serialized names of the provided components.
        /// </summary>
        /// <param name="components">The components to get the type names for. Null components are skipped.</param>
        /// <param name="uxFilter">Applies the UX filter for UI to each component name.</param>
        /// <returns>A comma separated list of the provided components and optionally unfiltered. Ex. "UnityEngine.GameObject, UnityEngine.Transform"</returns>
        internal static string GetCommaSeparatedComponentNames(Component[] components, bool uxFilter = true)
        {
            string componentNames = "";
            bool isFirst = true;
            foreach (Component c in components)
            {
                if (!c)
                    continue;
                string componentName = c.GetType().FullName;
                if (uxFilter)
                    componentName = FilterComponentNameForUI(componentName);

                if (!isFirst)
                    componentNames += $", {componentName}";
                else
                {
                    isFirst = false;
                    componentNames = componentName;
                }
            }

            return componentNames;
        }
        
        internal static void SetBorderColorOfElement(VisualElement element, Color color)
        {
            if (element == null)
                return;

            IStyle elementStyle = element.style;
            elementStyle.borderBottomColor = color;
            elementStyle.borderRightColor = color;
            elementStyle.borderLeftColor = color;
            elementStyle.borderTopColor = color;
        }

        internal static void SetBorderRadiusOfElement(VisualElement element, float radius)
        {
            if (element == null)
                return;

            IStyle elementStyle = element.style;
            elementStyle.borderBottomLeftRadius = radius;
            elementStyle.borderBottomRightRadius = radius;
            elementStyle.borderTopLeftRadius = radius;
            elementStyle.borderTopRightRadius = radius;
        }

        internal static NetworkIDPair GetNetworkIDPairFromGameObject(List<NetworkIDPair> networkIDs, GameObject target)
        {
            if (networkIDs == null || !target)
                return null;

            return networkIDs.FirstOrDefault(pair => pair.gameObject == target);
        }
    }
}