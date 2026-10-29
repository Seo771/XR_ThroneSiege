using ThroneSiege.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace ThroneSiege.UI
{
    [DisallowMultipleComponent]
    public sealed class HealthBarUI : MonoBehaviour
    {
        [SerializeField] private ActorHealth health;
        [SerializeField] private Image fill;

        private void OnEnable()
        {
            if (health != null) health.HealthChanged += Refresh;
            Refresh();
        }

        private void Start() => Refresh();

        private void OnDisable()
        {
            if (health != null) health.HealthChanged -= Refresh;
        }

        private void Refresh()
        {
            if (health != null && fill != null)
                fill.fillAmount = Mathf.Clamp01(health.HealthRatio);
        }
    }
}
