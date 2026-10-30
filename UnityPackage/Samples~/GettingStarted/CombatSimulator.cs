using System.Collections.Generic;
using UnityEngine;

namespace reromanlee.EventAggregator.Samples.GettingStarted
{
    /// <summary>A publisher standing in for real gameplay: spawns an enemy on a fixed interval and defeats each one a moment later.</summary>
    public sealed class CombatSimulator : MonoBehaviour
    {
        private readonly struct Enemy
        {
            public readonly int Id;
            public readonly float DefeatTime;

            public Enemy(int id, float defeatTime)
            {
                Id = id;
                DefeatTime = defeatTime;
            }
        }

        [SerializeField] private float spawnInterval = 0.6f;
        [SerializeField] private float enemyLifetime = 1.5f;
        [SerializeField] private int minimumPoints = 10;
        [SerializeField] private int maximumPoints = 30;

        // Every enemy lives equally long, so they are defeated in spawn order and a queue is all the simulation needs.
        private readonly Queue<Enemy> aliveEnemies = new();
        private IEventBus eventBus;
        private int nextEnemyId;
        private float nextSpawnTime;

        public void Construct(IEventBus bus)
        {
            eventBus = bus;
        }

        private void Update()
        {
            float time = Time.time;
            if (time >= nextSpawnTime)
            {
                nextSpawnTime = time + spawnInterval;
                int enemyId = ++nextEnemyId;
                aliveEnemies.Enqueue(new Enemy(enemyId, time + enemyLifetime));
                eventBus.Publish(new EnemySpawned(enemyId));
            }

            while (aliveEnemies.Count > 0 && aliveEnemies.Peek().DefeatTime <= time)
            {
                Enemy enemy = aliveEnemies.Dequeue();
                eventBus.Publish(new EnemyDefeated(enemy.Id, Random.Range(minimumPoints, maximumPoints + 1)));
            }
        }
    }
}
