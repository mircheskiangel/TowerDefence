using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    // Visual only: lets one SpriteRenderer or UI Image take its sprite from a SkinSet slot in the
    // isometric view. The sprite it was built with is remembered as the fallback, used in
    // top-down and whenever the slot is empty, so an empty skin changes nothing.
    // Optionally a skinned sprite is drawn untinted (its pixel art has its own colours) and at its
    // native pixel size (64 px = 1 unit, cancelling the parents' scale); both are undone when the
    // fallback is shown again.
    // Prefab parts are refreshed by their owner (ProjectionLook / Projectile call Apply).
    // Scene objects (HUD) get a GridManager reference and refresh themselves on a V switch.
    public class SkinnedSprite : MonoBehaviour
    {
        [SerializeField] private SkinSet skin;
        [SerializeField] private SkinSlot slot = SkinSlot.None;
        [SerializeField] private int variant;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Image image;
        [Tooltip("Optional (scene objects): refresh automatically when the projection changes.")]
        [SerializeField] private GridManager grid;
        [Tooltip("Skinned: draw the sprite renderer white (untinted).")]
        [SerializeField] private bool whiteWhenSkinned;
        [Tooltip("Skinned: draw at native pixel size, cancelling the parents' scale.")]
        [SerializeField] private bool nativeSize;

        private Sprite fallback;
        private Color fallbackColor = Color.white;
        private Vector3 fallbackScale = Vector3.one;
        private bool captured;
        private GridManager subscribed;

        private void Awake()
        {
            Capture();
        }

        private void OnEnable()
        {
            if (grid == null) return;
            grid.ProjectionChanged += Refresh;
            subscribed = grid;
            Refresh();
        }

        private void OnDisable()
        {
            if (subscribed != null) subscribed.ProjectionChanged -= Refresh;
            subscribed = null;
        }

        // Shared prefabs (e.g. the enemy contact shadow) choose their slot at spawn.
        public void SetSlot(SkinSlot newSlot)
        {
            slot = newSlot;
        }

        // Picks the sprite for 'gridManager's current projection.
        public void Apply(GridManager gridManager)
        {
            Capture();
            bool iso = gridManager != null && gridManager.ProjectionType == GridProjectionType.Isometric;
            Sprite skinSprite = iso ? SkinSet.Resolve(skin, slot, null, variant) : null;
            Sprite sprite = skinSprite != null ? skinSprite : fallback;
            bool skinnedNow = skinSprite != null;
            if (spriteRenderer != null)
            {
                if (spriteRenderer.sprite != sprite) spriteRenderer.sprite = sprite;
                if (whiteWhenSkinned) spriteRenderer.color = skinnedNow ? Color.white : fallbackColor;
                if (nativeSize)
                    spriteRenderer.transform.localScale = skinnedNow
                        ? ProjectionLook.NativeScale(spriteRenderer.transform, gridManager.CellSize)
                        : fallbackScale;
            }
            if (image != null && image.sprite != sprite) image.sprite = sprite;
        }

        private void Refresh()
        {
            Apply(grid);
        }

        private void Capture()
        {
            if (captured) return;
            if (spriteRenderer != null)
            {
                fallback = spriteRenderer.sprite;
                fallbackColor = spriteRenderer.color;
                fallbackScale = spriteRenderer.transform.localScale;
            }
            else if (image != null) fallback = image.sprite;
            captured = true;
        }
    }
}
