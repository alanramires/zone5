using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Zone5
{
    public class PanelMancheController : MonoBehaviour
    {
        [Header("Panel Group")]
        [SerializeField] private PanelGroupController panelGroup;
        [SerializeField] private PanelThrottleController throttleController;

        [Header("Mode Toggles")]
        [SerializeField] private Toggle toggleNormal;
        [SerializeField] private Toggle toggleAcrobacia;
        [SerializeField] private Toggle togglePosComb;

        [Header("Rows")]
        [SerializeField] private GameObject rowNormal;
        [SerializeField] private GameObject rowAcrobacia;
        [SerializeField] private GameObject rowPosComb;

        [Header("Bar Aux")]
        [SerializeField] private Graphic barAuxImage;
        [SerializeField] private TMP_Text barAuxText;

        [Header("Selections")]
        [SerializeField] private ButtonSelectionGroup[] selectionGroups;

        [Header("State")]
        [SerializeField] private string machCodeText = "12";

        private bool _suppressToggle;
        private bool _hasMode;
        private Mode _currentMode;
        private string _currentPrefix = "";
        private int _posCombCount;
        private bool _suppressSelectionUpdate;

        private void Awake()
        {
            if (panelGroup == null)
                panelGroup = FindFirstObjectByType<PanelGroupController>();
            if (throttleController == null)
                throttleController = FindFirstObjectByType<PanelThrottleController>();
        }

        private void OnEnable()
        {
            BindToggles();
            BindSelectionGroups();
            BindThrottle();
            BindPanelGroup();
        }

        private void Start()
        {
            ApplyModeFromToggles();
        }

        private void OnDisable()
        {
            UnbindToggles();
            UnbindSelectionGroups();
            UnbindThrottle();
            UnbindPanelGroup();
        }

        public void OnToggleNormal(bool isOn)
        {
            if (_suppressToggle) return;
            if (isOn)
                SetMode(Mode.Normal);
            else
                EnsureFallbackNormal();
        }

        public void OnToggleAcrobacia(bool isOn)
        {
            if (_suppressToggle) return;
            if (isOn)
                SetMode(Mode.Acrobacia);
            else
                EnsureFallbackNormal();
        }

        public void OnTogglePosComb(bool isOn)
        {
            if (_suppressToggle) return;
            if (isOn)
                SetMode(Mode.PosComb);
            else
                EnsureFallbackNormal();
        }

        public void SetMachCodeText(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return;
            machCodeText = code.Trim();
        }

        public void ChooseManeuverPrefix(string prefix)
        {
            if (panelGroup == null) return;
            if (string.IsNullOrWhiteSpace(prefix)) return;

            _currentPrefix = prefix.Trim().ToUpperInvariant();
            UpdatePreparedManeuver();
        }

        private void SetMode(Mode mode)
        {
            bool modeChanged = !_hasMode || mode != _currentMode;
            _hasMode = true;
            _currentMode = mode;
            if (modeChanged && mode == Mode.PosComb)
                _posCombCount = 0;

            _suppressToggle = true;

            if (toggleNormal != null) toggleNormal.isOn = mode == Mode.Normal;
            if (toggleAcrobacia != null) toggleAcrobacia.isOn = mode == Mode.Acrobacia;
            if (togglePosComb != null) togglePosComb.isOn = mode == Mode.PosComb;

            if (rowNormal != null) rowNormal.SetActive(mode == Mode.Normal);
            if (rowAcrobacia != null) rowAcrobacia.SetActive(mode == Mode.Acrobacia);
            if (rowPosComb != null) rowPosComb.SetActive(mode == Mode.PosComb);

            _suppressToggle = false;

            if (panelGroup != null && modeChanged)
            {
                int slots = mode == Mode.PosComb ? 2 : 1;
                panelGroup.ConfigureManeuverSlots(slots);
            }

            if (modeChanged)
                ClearSelections();

            UpdateBarAux(mode);
        }

        private void BindToggles()
        {
            if (toggleNormal != null) toggleNormal.onValueChanged.AddListener(OnToggleNormal);
            if (toggleAcrobacia != null) toggleAcrobacia.onValueChanged.AddListener(OnToggleAcrobacia);
            if (togglePosComb != null) togglePosComb.onValueChanged.AddListener(OnTogglePosComb);
        }

        private void UnbindToggles()
        {
            if (toggleNormal != null) toggleNormal.onValueChanged.RemoveListener(OnToggleNormal);
            if (toggleAcrobacia != null) toggleAcrobacia.onValueChanged.RemoveListener(OnToggleAcrobacia);
            if (togglePosComb != null) togglePosComb.onValueChanged.RemoveListener(OnTogglePosComb);
        }

        private void UpdateBarAux(Mode mode)
        {
            if (barAuxImage == null)
                barAuxImage = GetComponent<Graphic>();
            if (barAuxImage != null)
                barAuxImage.color = mode switch
                {
                    Mode.Acrobacia => new Color(1f, 0.85f, 0f, 0.8f),
                    Mode.PosComb => new Color(0.1f, 0.4f, 0.1f, 0.8f),
                    _ => new Color(0f, 0f, 0f, 0.8f)
                };

            if (barAuxText != null)
            {
                barAuxText.text = mode switch
                {
                    Mode.Acrobacia => "Acrobacia",
                    Mode.PosComb => $"Pós-Combustor {_posCombCount}/2",
                    _ => "Normal"
                };
            }
        }

        public void ApplyModeFromToggles()
        {
            if (togglePosComb != null && togglePosComb.isOn)
            {
                SetMode(Mode.PosComb);
                return;
            }
            if (toggleAcrobacia != null && toggleAcrobacia.isOn)
            {
                SetMode(Mode.Acrobacia);
                return;
            }
            SetMode(Mode.Normal);
        }

        private void EnsureFallbackNormal()
        {
            if (toggleNormal != null && toggleNormal.isOn) return;
            if (toggleAcrobacia != null && toggleAcrobacia.isOn) return;
            if (togglePosComb != null && togglePosComb.isOn) return;
            SetMode(Mode.Normal);
        }

        private string GetSpeedText()
        {
            if (throttleController != null)
            {
                if (!string.IsNullOrWhiteSpace(throttleController.CurrentSpeedText))
                    return throttleController.CurrentSpeedText;
                return "";
            }
            return machCodeText;
        }

        private static string BuildManeuverId(string prefix, string speedText, out string altId)
        {
            altId = null;
            if (string.IsNullOrEmpty(prefix)) return speedText ?? "";

            char last = prefix[prefix.Length - 1];
            if (last == 'D' || last == 'E' || last == 'F')
            {
                string main = prefix.Substring(0, prefix.Length - 1) + speedText + last;
                altId = prefix + speedText;
                return main;
            }

            return prefix + speedText;
        }

        private ManeuverProfile ResolveProfile(string id)
        {
            ManeuverProfile profile = null;
            var manager = FindFirstObjectByType<ManeuverManager>();
            if (manager != null)
                profile = manager.Resolve(id);
            if (profile == null)
                profile = ManeuverProfileCatalog.Resolve(id);
            return profile;
        }

        private enum Mode
        {
            Normal,
            Acrobacia,
            PosComb
        }

        private void ClearSelections()
        {
            _suppressSelectionUpdate = true;
            _currentPrefix = "";
            if (selectionGroups == null) return;
            for (int i = 0; i < selectionGroups.Length; i++)
            {
                if (selectionGroups[i] != null)
                    selectionGroups[i].ClearSelection();
            }
            if (throttleController != null)
                throttleController.ClearSelection();
            _suppressSelectionUpdate = false;
        }

        private void BindSelectionGroups()
        {
            if (selectionGroups == null) return;
            for (int i = 0; i < selectionGroups.Length; i++)
            {
                if (selectionGroups[i] != null)
                    selectionGroups[i].OnSelectionChanged += OnManeuverSelectionChanged;
            }
        }

        private void UnbindSelectionGroups()
        {
            if (selectionGroups == null) return;
            for (int i = 0; i < selectionGroups.Length; i++)
            {
                if (selectionGroups[i] != null)
                    selectionGroups[i].OnSelectionChanged -= OnManeuverSelectionChanged;
            }
        }

        private void BindThrottle()
        {
            if (throttleController != null)
                throttleController.OnSpeedChanged += OnThrottleChanged;
        }

        private void UnbindThrottle()
        {
            if (throttleController != null)
                throttleController.OnSpeedChanged -= OnThrottleChanged;
        }

        private void BindPanelGroup()
        {
            if (panelGroup != null)
            {
                panelGroup.OnAfterburnerCountChanged += OnAfterburnerCountChanged;
                panelGroup.OnAfterburnerIncluded += OnAfterburnerIncluded;
                panelGroup.OnAfterburnerStepBack += OnAfterburnerStepBack;
            }
        }

        private void UnbindPanelGroup()
        {
            if (panelGroup != null)
            {
                panelGroup.OnAfterburnerCountChanged -= OnAfterburnerCountChanged;
                panelGroup.OnAfterburnerIncluded -= OnAfterburnerIncluded;
                panelGroup.OnAfterburnerStepBack -= OnAfterburnerStepBack;
            }
        }

        private void OnManeuverSelectionChanged(string id)
        {
            if (_suppressSelectionUpdate) return;
            if (string.IsNullOrWhiteSpace(id)) return;
            _currentPrefix = id.Trim().ToUpperInvariant();
            UpdatePreparedManeuver();
        }

        private void OnThrottleChanged(string speed)
        {
            if (_suppressSelectionUpdate) return;
            if (string.IsNullOrWhiteSpace(_currentPrefix)) return;
            UpdatePreparedManeuver();
        }

        private void OnAfterburnerCountChanged(int count)
        {
            _posCombCount = count;
            if (_currentMode == Mode.PosComb)
                UpdateBarAux(_currentMode);
        }

        private void OnAfterburnerIncluded()
        {
            ClearSelections();
        }

        private void OnAfterburnerStepBack()
        {
            RestoreFirstManeuverSelection();
        }

        private void UpdatePreparedManeuver()
        {
            if (panelGroup == null)
                return;
            if (string.IsNullOrWhiteSpace(_currentPrefix))
            {
                panelGroup.SetPreparedManeuverAtActive(null, null);
                return;
            }

            string speed = GetSpeedText();
            if (string.IsNullOrWhiteSpace(speed))
            {
                panelGroup.SetPreparedManeuverAtActive(null, null);
                return;
            }
            string maneuverId = BuildManeuverId(_currentPrefix, speed, out var altId);
            ManeuverProfile profile = ResolveProfile(maneuverId);
            if (profile == null && !string.IsNullOrEmpty(altId))
                profile = ResolveProfile(altId);
            string code = !string.IsNullOrEmpty(altId) ? altId : maneuverId;
            panelGroup.SetPreparedManeuverAtActive(profile, profile != null ? code : null);
        }

        private void RestoreFirstManeuverSelection()
        {
            ClearSelections();
            if (panelGroup == null) return;

            string code = panelGroup.GetPreparedCodeAt(0);
            if (string.IsNullOrWhiteSpace(code)) return;
            if (!TrySplitManeuverCode(code, out var prefix, out var speed))
                return;

            if (throttleController != null)
                throttleController.SelectSpeed(speed);
            SelectPrefix(prefix);
        }

        private void SelectPrefix(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix)) return;
            if (selectionGroups == null) return;
            for (int i = 0; i < selectionGroups.Length; i++)
            {
                if (selectionGroups[i] != null)
                    selectionGroups[i].SelectById(prefix.Trim().ToUpperInvariant());
            }
        }

        private static bool TrySplitManeuverCode(string code, out string prefix, out string speed)
        {
            prefix = "";
            speed = "";
            if (string.IsNullOrWhiteSpace(code)) return false;

            string trimmed = code.Trim().ToUpperInvariant();
            if (trimmed.Length < 3) return false;

            string maybeSpeed = trimmed.Substring(trimmed.Length - 2, 2);
            if (!char.IsDigit(maybeSpeed[0]) || !char.IsDigit(maybeSpeed[1])) return false;

            prefix = trimmed.Substring(0, trimmed.Length - 2);
            speed = maybeSpeed;
            return true;
        }

        public void ResetCockpitToDefault()
        {
            _posCombCount = 0;
            _hasMode = false;
            SetMode(Mode.Normal);
            if (panelGroup != null)
                panelGroup.ClearButtonSelections();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
