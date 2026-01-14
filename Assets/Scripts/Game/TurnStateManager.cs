using System;
using UnityEngine;

namespace Zone5
{
    public class TurnStateManager : MonoBehaviour
    {
        public static TurnStateManager Instance { get; private set; }

        public event Action<GameEnum.TurnState> OnStateChanged;

        [SerializeField] private GameEnum.TurnState currentState = GameEnum.TurnState.SelectManeuver;
        public GameEnum.TurnState CurrentState => currentState;

        private TurnManager turnManager;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            BindTurnManager();
        }

        private void OnDisable()
        {
            UnbindTurnManager();
        }

        private void BindTurnManager()
        {
            UnbindTurnManager();
            turnManager = FindFirstObjectByType<TurnManager>();
            if (turnManager == null) return;

            turnManager.OnStateChanged += SyncFromTurnManager;
            turnManager.OnSheetChanged += SyncFromTurnManager;
            SyncFromTurnManager();
        }

        private void UnbindTurnManager()
        {
            if (turnManager == null) return;
            turnManager.OnStateChanged -= SyncFromTurnManager;
            turnManager.OnSheetChanged -= SyncFromTurnManager;
            turnManager = null;
        }

        private void SyncFromTurnManager()
        {
            if (turnManager?.sheet == null) return;
            SetState(turnManager.sheet.phase);
        }

        public void SetState(GameEnum.TurnState state)
        {
            if (currentState == state) return;
            currentState = state;
            OnStateChanged?.Invoke(state);
        }
    }
}
