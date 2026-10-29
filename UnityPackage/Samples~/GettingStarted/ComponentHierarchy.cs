using UnityEngine;

namespace reromanlee.EventAggregator.Samples.GettingStarted
{
    /// <summary>
    /// Builds GameObjects so that no OnEnable runs before its component has received its buses: create an inactive
    /// root, add and inject the components, then activate the root once.
    /// </summary>
    public static class ComponentHierarchy
    {
        /// <summary>Creates an inactive GameObject under <paramref name="parent"/>.</summary>
        public static GameObject CreateInactiveRoot(string name, Transform parent)
        {
            GameObject root = new(name);
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            return root;
        }

        /// <summary>Adds the component on a new child of <paramref name="root"/>. It wakes up when the root is activated.</summary>
        public static T AddChild<T>(GameObject root, string name) where T : Component
        {
            GameObject child = new(name);
            child.transform.SetParent(root.transform, false);
            return child.AddComponent<T>();
        }
    }
}
