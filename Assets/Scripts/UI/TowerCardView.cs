using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    // One card of the tower selector: icon, cost and, on a level that offers several towers, the
    // name and the "n/2" counter. With a single card the name and the counter stay hidden.
    public class TowerCardView : MonoBehaviour
    {
        private const float SingleWidth = 262f, SeveralWidth = 340f, Height = 128f;

        [SerializeField] private RectTransform rect;
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private RectTransform coin;
        [SerializeField] private TMP_Text priceLabel;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text counterLabel;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Outline highlight;
        [SerializeField] private float greyedAlpha = 0.55f;
        [SerializeField] private Color greyedIcon = new Color(0.6f, 0.6f, 0.6f);

        public Button Button => button;
        public RectTransform Rect => rect;

        public void Bind(TowerSO tower, Sprite sprite, bool several)
        {
            gameObject.SetActive(true);
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            priceLabel.text = tower.cost.ToString();
            nameLabel.text = tower.displayName;
            nameLabel.gameObject.SetActive(several);
            counterLabel.gameObject.SetActive(several);
            rect.sizeDelta = new Vector2(several ? SeveralWidth : SingleWidth, Height);
            coin.anchoredPosition = several ? new Vector2(146f, -22f) : new Vector2(162f, 0f);
            ((RectTransform)priceLabel.transform).anchoredPosition = several ? new Vector2(188f, -22f) : new Vector2(204f, 0f);
        }

        public void Refresh(bool selected, bool greyed, string counter)
        {
            highlight.enabled = selected;
            group.alpha = greyed ? greyedAlpha : 1f;
            icon.color = greyed ? greyedIcon : Color.white;
            counterLabel.text = counter;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
