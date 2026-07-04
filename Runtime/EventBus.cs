using System;
using System.Collections.Generic;
using UnityEngine;

namespace reromanlee.EventAggregator
{
    public class EventBus : IEventBus, IDisposable
    {
        private readonly Dictionary<Type, EventListeners> listenerTypes = new();
        private readonly object lockObject = new();
        private volatile bool isDisposed = false;

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }
            lock (lockObject)
            {
                if (isDisposed)
                {
                    return;
                }
                foreach (EventListeners eventListeners in listenerTypes.Values)
                {
                    eventListeners.Clear();
                }
                listenerTypes.Clear();
                isDisposed = true;
            }
        }

        public void Publish<E>(E eventData)
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(EventBus));
            }

            Type eventType = typeof(E);
            object[] targetListeners;

            lock (lockObject)
            {
                if (!listenerTypes.TryGetValue(eventType, out EventListeners eventListeners))
                {
                    Debug.LogWarning($"No subscribers for the event of type '{eventType.Name}'");
                    return;
                }
                targetListeners = eventListeners.ToArray();
            }

            foreach (object eventListener in targetListeners)
            {
                try
                {
                    ((IEventListener<E>)eventListener).Handle(eventData);
                }
                catch (Exception exception)
                {
                    Type listenerType = eventListener.GetType();
                    Debug.LogError($"Exception caught from '{listenerType.Name}' when handling '{eventType.Name}' with message '{exception.Message}'");
                }
            }
        }

        public void Subscribe<E>(IEventListener<E> listener)
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(EventBus));
            }

            Type eventType = typeof(E);

            lock (lockObject)
            {
                // Re-check under lock to prevent adding a listener after Dispose cleared everything.
                if (isDisposed)
                {
                    throw new ObjectDisposedException(nameof(EventAggregator));
                }
                if (!listenerTypes.TryGetValue(eventType, out EventListeners eventListeners))
                {
                    eventListeners = new();
                    listenerTypes.Add(eventType, eventListeners);
                }
                else if (eventListeners.Contains(listener))
                {
                    Type listenerType = listener.GetType();
                    Debug.LogWarning($"Listener '{listenerType.Name}' is already subscribed to the event of type '{eventType.Name}'");
                    return;
                }
                eventListeners.Add(listener);
            }
        }

        public void Unsubscribe<E>(IEventListener<E> listener)
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(EventBus));
            }

            Type eventType = typeof(E);

            lock (lockObject)
            {
                if (listenerTypes.TryGetValue(eventType, out EventListeners eventListeners))
                {
                    eventListeners.Remove(listener);
                }
            }
        }
    }
}