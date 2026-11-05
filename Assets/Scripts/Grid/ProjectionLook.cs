using UnityEngine;

namespace TowerDefense
{
    // Visual only: picks the sprite for the current projection (e.g. a flat square tower in
    // top-down, a standing tower in isometric) and shows parts that exist only in isometric
    // (e.g. an enemy's ground shadow). The owner calls Apply() at spawn and after a V switch.
    // In isometric the target's sprite comes from the SkinSet slot when it is filled, and the
    // 'skinned' parts (e.g. a tower's base plate) are refreshed from their own slots.
    public class ProjectionLook : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private Sprite topDownSprite;
        [SerializeField] private Sprite isometricSprite;
        [SerializeField] private GameObject[] isometricOnly = new GameObject[0];
        [SerializeField] private SkinSet skin;
        [SerializeField] private SkinSlot isometricSlot = SkinSlot.None;
        [SerializeField] private SkinnedSprite[] skinned = new SkinnedSprite[0];

        // Shared prefabs (one Enemy prefab for every enemy type) choose their slot at spawn.
        public void SetIsometricSlot(SkinSlot slot)
        {
            isometricSlot = slot;
        }

        public void Apply(GridManager grid)
        {
            bool iso = grid != null && grid.ProjectionType == GridProjectionType.Isometric;
            if (target != null)
            {
                Sprite sprite = iso ? SkinSet.Resolve(skin, isometricSlot, isometricSprite) : topDownSprite;
                if (sprite != null) target.sprite = sprite;
            }
            foreach (GameObject go in isometricOnly)
                if (go != null && go.activeSelf != iso) go.SetActive(iso);
            foreach (SkinnedSprite part in skinned)
                if (part != null) part.Apply(grid);
        }
    }
}
