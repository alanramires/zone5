using UnityEngine;

namespace Zone5
{
    public class PanelThrottleController : MonoBehaviour
    {
        public event System.Action<string> OnSpeedChanged;

        [Header("State")]
        [SerializeField] private string currentSpeedText = "";

        [Header("Selection")]
        [SerializeField] private ButtonSelectionGroup selectionGroup;

        public string CurrentSpeedText => currentSpeedText;

        private void OnEnable()
        {
            BindSelectionGroup();
        }

        private void OnDisable()
        {
            UnbindSelectionGroup();
        }

        public void SetSpeed06()
        {
            SetSpeed("06");
        }

        public void SetSpeed09()
        {
            SetSpeed("09");
        }

        public void SetSpeed12()
        {
            SetSpeed("12");
        }

        public void SetSpeed18()
        {
            SetSpeed("18");
        }

        public void SetSpeed(string speedText)
        {
            if (string.IsNullOrWhiteSpace(speedText)) return;
            string trimmed = speedText.Trim();
            if (currentSpeedText == trimmed) return;
            currentSpeedText = trimmed;
            OnSpeedChanged?.Invoke(currentSpeedText);
        }

        public void ClearSelection()
        {
            if (selectionGroup != null)
                selectionGroup.ClearSelection();

            if (string.IsNullOrEmpty(currentSpeedText)) return;
            currentSpeedText = "";
            OnSpeedChanged?.Invoke(currentSpeedText);
        }

        public void SelectSpeed(string speedText)
        {
            if (string.IsNullOrWhiteSpace(speedText)) return;
            if (selectionGroup != null)
                selectionGroup.SelectById(speedText.Trim());
            SetSpeed(speedText);
        }

        private void BindSelectionGroup()
        {
            if (selectionGroup != null)
                selectionGroup.OnSelectionChanged += OnSpeedSelectionChanged;
        }

        private void UnbindSelectionGroup()
        {
            if (selectionGroup != null)
                selectionGroup.OnSelectionChanged -= OnSpeedSelectionChanged;
        }

        private void OnSpeedSelectionChanged(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            SetSpeed(id);
        }
    }
}
