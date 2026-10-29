using ThroneSiege.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace ThroneSiege.UI
{
    [DisallowMultipleComponent]
    public sealed class BattleResultUI : MonoBehaviour
    {
        [SerializeField] private BattleManager battle;
        [SerializeField] private GameObject result;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private GameObject defeatPanel;
        private Button[] restartButtons;

        private void OnEnable()
        {
            if (battle == null || result == null) return;
            battle.StateChanged += Refresh;
            restartButtons = result.GetComponentsInChildren<Button>(true);
            foreach (Button button in restartButtons) button.onClick.AddListener(Restart);
            Refresh(battle.State);
        }

        private void OnDisable()
        {
            if (battle != null) battle.StateChanged -= Refresh;
            if (restartButtons != null)
                foreach (Button button in restartButtons)
                    if (button != null) button.onClick.RemoveListener(Restart);
        }

        private void Refresh(BattleManager.BattleState state)
        {
            bool victory = state == BattleManager.BattleState.Victory;
            bool defeat = state == BattleManager.BattleState.Defeat;
            if (victoryPanel != null) victoryPanel.SetActive(victory);
            if (defeatPanel != null) defeatPanel.SetActive(defeat);
            if (result != null) result.SetActive(victory || defeat);
        }

        private void Restart()
        {
            if (battle != null && (battle.State == BattleManager.BattleState.Victory ||
                battle.State == BattleManager.BattleState.Defeat)) battle.RestartBattle();
        }
    }
}
