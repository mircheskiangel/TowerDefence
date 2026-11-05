using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    // Visual only: lets one SpriteRenderer or UI Image take its sprite from a SkinSet slot in the
    // isometric view. The sprite it was built with is remembered as the fallback, used in
    // top-down and whenever the slot is empty, so an empty skin changes nothing.
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

        private Sprite fallback;
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

        // Picks the sprite for 'gridManager's current projection.
        public void Apply(GridManager gridManager)
        {
            Capture();
            bool iso = gridManager != null && gridManager.ProjectionType == GridProjectionType.Isometric;
            Sprite sprite = iso ? SkinSet.Resolve(skin, slot, fallback, variant) : fallback;
            if (spriteRenderer != null && spriteRenderer.sprite != sprite) spriteRenderer.sprite = sprite;
            if (image != null && image.sprite != sprite) image.sprite = sprite;
        }

        private void Refresh()
        {
            Apply(grid);
        }

        private void Capture()
        {
            if (captured) return;
            if (spriteRenderer != null) fallback = spriteRenderer.sprite;
            else if (image != null) fallback = image.sprite;
            captured = true;
        }
    }
}
