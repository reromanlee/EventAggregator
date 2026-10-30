using UnityEngine;

namespace reromanlee.EventAggregator.Tests
{
    /// <summary>A ScriptableObject listener used to verify that destroyed Unity objects are pruned.</summary>
    internal sealed class DestroyableAssetListener : ScriptableObject, IEventListener<Ping>
    {
        public int Count;

        public void Handle(Ping eventData)
        {
            Count++;
        }
    }
}
