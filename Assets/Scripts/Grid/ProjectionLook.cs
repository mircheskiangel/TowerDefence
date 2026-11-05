using UnityEngine;

namespace TowerDefense
{
    // Visual only: picks the sprite for the current projection (e.g. a flat square tower in
    // top-down, a standing tower in isometric) and shows parts that exist only in isometric
    // (e.g. an enemy's ground shadow). The owner calls Apply() at spawn and after a V switch.
    // In isometric the target's sprite comes from the SkinSet slot when it is filled, and the
    // 'skinned' parts (e.g. a tower's shadow) are refreshed from their own slots.
    //
    // A skinned (pixel-art) sprite brings its own colours, so it is drawn untinted (white),
    // optionally at its native pixel size (64 px = 1 unit, like the terrain) whatever the
    // parent's scale, and 'hideWhenSkinned' parts of the plain look (e.g. the grey base plate)
    // are hidden. Without a skin sprite everything is exactly the plain look (tint, scale).
    public class ProjectionLook : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private Sprite topDownSprite;
        [SerializeField] private Sprite isometricSprite;
        [SerializeField] private GameObject[] isometricOnly = new GameObject[0];
        [SerializeField] private SkinSet skin;
        [SerializeField] private SkinSlot isometricSlot = SkinSlot.None;
        [SerializeField] private SkinnedSprite[] skinned = new SkinnedSprite[0];
        [Tooltip("Skinned: draw the target at its native pixel size, cancelling the parents' scale.")]
        [SerializeField] private bool nativeSizeWhenSkinned;
        [Tooltip("Hidden while the target is skinned (parts the skin art already contains).")]
        [SerializeField] private GameObject[] hideWhenSkinned = new GameObject[0];

        private Color tint = Color.white;
        private bool hasTint;
        private Vector3 restScale = Vector3.one;
        private bool restCaptured;

        // True when the target currently shows a skin sprite (isometric with the slot filled).
        public bool IsSkinned { get; private set; }

        // Shared prefabs (one Enemy prefab for every enemy type) choose their slot at spawn.
        public void SetIsometricSlot(SkinSlot slot)
        {
            isometricSlot = slot;
        }

        // Color of the plain look (e.g. the EnemySO / TowerSO color); a skin sprite is untinted.
        public void SetTint(Color color)
        {
            tint = color;
            hasTint = true;
        }

        public void Apply(GridManager grid)
        {
            bool iso = grid != null && grid.ProjectionType == GridProjectionType.Isometric;
            Sprite skinSprite = iso ? SkinSet.Resolve(skin, isometricSlot, null) : null;
            IsSkinned = skinSprite != null;
            if (target != null)
            {
                if (!restCaptured)
                {
                    restScale = target.transform.localScale;
                    restCaptured = true;
                }
                Sprite sprite = iso ? (skinSprite != null ? skinSprite : isometricSprite) : topDownSprite;
                if (sprite != null) target.sprite = sprite;
                if (IsSkinned) target.color = Color.white;
                else if (hasTint) target.color = tint;
                target.transform.localScale = IsSkinned && nativeSizeWhenSkinned
                    ? NativeScale(target.transform, grid.CellSize)
                    : restScale;
            }
            foreach (GameObject go in isometricOnly)
                if (go != null && go.activeSelf != iso) go.SetActive(iso);
            foreach (GameObject go in hideWhenSkinned)
                if (go != null && IsSkinned && go.activeSelf) go.SetActive(false);
            foreach (SkinnedSprite part in skinned)
                if (part != null) part.Apply(grid);
        }

        // Local scale that makes 't' draw at 1 world unit per 64 sprite pixels (times cellSize).
        public static Vector3 NativeScale(Transform t, float cellSize)
        {
            Vector3 p = t.parent != null ? t.parent.lossyScale : Vector3.one;
            return new Vector3(cellSize / NonZero(p.x), cellSize / NonZero(p.y), 1f);
        }

        private static float NonZero(float v) => Mathf.Approximately(v, 0f) ? 1f : v;
    }
}
