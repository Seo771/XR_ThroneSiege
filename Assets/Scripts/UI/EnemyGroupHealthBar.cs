using ThroneSiege.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace ThroneSiege.UI
{
    [DisallowMultipleComponent]
    public sealed class EnemyGroupHealthBar : MonoBehaviour
    {
        [SerializeField] private BattleManager battle;
        [SerializeField] private Image fill;
        private RectTransform dividers;
        private int sectionCount = -1;

        private void OnEnable()
        {
            if (battle != null) battle.RemainingEnemiesChanged += Refresh;
            Refresh(battle != null ? battle.RemainingEnemies : 0);
        }
        private void Start() => Refresh(battle != null ? battle.RemainingEnemies : 0);
        private void OnDisable()
        {
            if (battle != null) battle.RemainingEnemiesChanged -= Refresh;
        }
        private void Refresh(int remaining)
        {
            if (battle == null || fill == null) return;
            int total = battle.TotalEnemies;
            fill.fillAmount = total > 0 ? Mathf.Clamp01((float)remaining / total) : 0f;
            if (sectionCount == total) return;
            sectionCount = total;
            if (dividers != null) { dividers.gameObject.SetActive(false); Destroy(dividers.gameObject); }
            dividers = new GameObject("EnemySections", typeof(RectTransform)).GetComponent<RectTransform>();
            dividers.SetParent(fill.rectTransform, false);
            dividers.anchorMin = Vector2.zero; dividers.anchorMax = Vector2.one;
            dividers.offsetMin = dividers.offsetMax = Vector2.zero;
            for (int i = 1; i < total; i++)
            {
                var divider = new GameObject("Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rect = divider.GetComponent<RectTransform>();
                rect.SetParent(dividers, false);
                rect.anchorMin = new Vector2((float)i / total, 0f);
                rect.anchorMax = new Vector2((float)i / total, 1f);
                rect.sizeDelta = new Vector2(4f, 0f);
                rect.anchoredPosition = Vector2.zero;
                var image = divider.GetComponent<Image>();
                image.color = new Color(0.12f, 0.08f, 0.08f, 1f);
                image.raycastTarget = false;
            }
        }
    }
}
