using UnityEngine;

namespace reromanlee.EventAggregator.Tests
{
    /// <summary>A MonoBehaviour listener used to verify that destroyed Unity objects are pruned.</summary>
    [AddComponentMenu("")]
    internal sealed class DestroyableBehaviourListener : MonoBehaviour, IEventListener<Ping>
    {
        public int Count;

        public void Handle(Ping eventData)
        {
            Count++;
        }
    }
}
