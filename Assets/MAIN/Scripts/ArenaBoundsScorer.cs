using UnityEngine;
using System.Collections.Generic;

// put this on a large trigger collider around the arena
// when an enemy leaves this zone, it gets removed and scored
[RequireComponent(typeof(Collider))]
public class ArenaBoundsScorer : MonoBehaviour
{
    [SerializeField] private bool scoreWhenEnemyExits = true;
    private readonly HashSet<int> handledEnemyIds = new HashSet<int>();

    void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void OnValidate()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (GameManagerX.Instance == null || GameManagerX.Instance.isGameOver) return;

        // handles child colliders too by walking up to the enemy root
        EnemyX enemy = other.GetComponentInParent<EnemyX>();
        if (enemy == null) return;
        int enemyId = enemy.gameObject.GetInstanceID();
        if (handledEnemyIds.Contains(enemyId)) return;
        handledEnemyIds.Add(enemyId);

        if (scoreWhenEnemyExits)
            GameManagerX.Instance.EnemyScored(enemy);

        Destroy(enemy.gameObject);
    }
}
