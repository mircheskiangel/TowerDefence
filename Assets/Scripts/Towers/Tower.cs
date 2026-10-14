using UnityEngine;

namespace TowerDefense
{
    // A placed tower: remembers its data (TowerSO) and the grid cell it occupies.
    // Targeting and shooting are added in M4.
    public class Tower : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;

        public TowerSO Data { get; private set; }
        public Vector2Int Cell { get; private set; }

        public void Initialize(TowerSO data, Vector2Int cell)
        {
            Data = data;
            Cell = cell;
            name = $"{data.displayName} ({cell.x},{cell.y})";
            if (body == null) body = GetComponent<SpriteRenderer>();
            if (body != null) body.color = data.color;
        }
    }
}
