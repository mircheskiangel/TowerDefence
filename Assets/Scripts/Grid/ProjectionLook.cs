using UnityEngine;

namespace TowerDefense
{
    // Visual only: picks the sprite for the current projection (e.g. a flat square tower in
    // top-down, a standing tower in isometric) and shows parts that exist only in isometric
    // (e.g. an enemy's ground shadow). The owner calls Apply() at spawn and after a V switch.
    public class ProjectionLook : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private Sprite topDownSprite;
        [SerializeField] private Sprite isometricSprite;
        [SerializeField] private GameObject[] isometricOnly = new GameObject[0];

        public void Apply(GridManager grid)
        {
            bool iso = grid != null && grid.ProjectionType == GridProjectionType.Isometric;
            if (target != null)
            {
                Sprite sprite = iso ? isometricSprite : topDownSprite;
                if (sprite != null) target.sprite = sprite;
            }
            foreach (GameObject go in isometricOnly)
                if (go != null && go.activeSelf != iso) go.SetActive(iso);
        }
    }
}
