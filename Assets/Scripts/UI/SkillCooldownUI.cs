using ThroneSiege.Player;
using TMPro;
using UnityEngine;

namespace ThroneSiege.UI
{
    [DisallowMultipleComponent]
    public sealed class SkillCooldownUI : MonoBehaviour
    {
        [SerializeField] private PlayerSkullSkill skill;
        [SerializeField] private GameObject cooldownPanel;
        [SerializeField] private TMP_Text cooldownText;
        private int displayedSeconds = -1;

        private void OnEnable()
        {
            displayedSeconds = -1;
            Refresh();
        }

        // Read after the skill's Update so a successful Q press appears immediately.
        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            float remaining = skill != null ? skill.CooldownRemaining : 0f;
            bool coolingDown = remaining > 0f;
            if (cooldownPanel != null && cooldownPanel.activeSelf != coolingDown)
                cooldownPanel.SetActive(coolingDown);

            int seconds = Mathf.CeilToInt(remaining);
            if (seconds == displayedSeconds) return;
            displayedSeconds = seconds;
            if (cooldownText != null)
                cooldownText.text = coolingDown ? seconds.ToString() + "초" : string.Empty;
        }

        private void OnDisable()
        {
            if (cooldownPanel != null) cooldownPanel.SetActive(false);
            if (cooldownText != null) cooldownText.text = string.Empty;
            displayedSeconds = -1;
        }
    }
}
