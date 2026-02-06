using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zone5
{
    public class PanelGroupController : MonoBehaviour
    {
        public event Action<List<ManeuverProfile>> OnManeuversConfirmed;
        public event Action<string> OnWeaponChanged;
        public event Action<int> OnAfterburnerCountChanged;
        public event Action OnAfterburnerIncluded;
        public event Action OnAfterburnerStepBack;

        [Header("Maneuver")]
        [SerializeField] private ManeuverManager maneuverManager;

        [Header("HUD Tracks")]
        [SerializeField] private GameObject trackDefault;
        [SerializeField] private GameObject trackAfterBurner;
        [SerializeField] private Image trackDImage;
        [SerializeField] private TMP_Text trackDText;
        [SerializeField] private Image trackAImage;
        [SerializeField] private TMP_Text trackAText;
        [SerializeField] private Image trackBImage;
        [SerializeField] private TMP_Text trackBText;

        [Header("State (Debug)")]
        [SerializeField] private string weaponCode = "X";
        [SerializeField] private List<ManeuverProfile> preparedManeuvers = new();
        [SerializeField] private List<string> preparedCodes = new();
        [SerializeField] private int activeManeuverIndex = 0;
        [SerializeField] private bool afterburnerMode;

        [Header("Buttons")]
        [SerializeField] private Button buttonConfirm;
        [SerializeField] private Button buttonInclude;
        [SerializeField] private Button buttonRemove;

        [Header("Arrows")]
        [SerializeField] private GameObject arrowStep0;
        [SerializeField] private GameObject arrowStep1;
        [SerializeField] private GameObject arrowSingle;
        [Header("Selection Group")]
        [SerializeField] private ButtonSelectionGroup selectionGroup;

        private UnitSpawner _spawner;
        private bool _clearSelectionOnce;
        private bool _confirmVisible;
        private GameObject _lastSelection;

        private void Awake()
        {
            if (selectionGroup == null)
            {
                selectionGroup = GetComponent<ButtonSelectionGroup>();
                if (selectionGroup == null)
                    selectionGroup = GetComponentInChildren<ButtonSelectionGroup>();
            }
        }

        // ...



        private void OnEnable()
        {
            BindSpawner();
        }

        // ...



        private void Start()
        {
            InitializeDefaults();
        }

        private void OnDisable()
        {
            UnbindSpawner();
        }

        private void BindSpawner()
        {
            if (_spawner != null) return;
            _spawner = FindFirstObjectByType<UnitSpawner>();
            if (_spawner != null)
                _spawner.OnSpawnCompleted += InitializeDefaults;
        }

        private void UnbindSpawner()
        {
            if (_spawner == null) return;
            _spawner.OnSpawnCompleted -= InitializeDefaults;
            _spawner = null;
        }

        public void InitializeDefaults()
        {
            weaponCode = "X";
            afterburnerMode = false;
            activeManeuverIndex = 0;

            preparedManeuvers.Clear();
            preparedManeuvers.Add(null);
            preparedCodes.Clear();
            preparedCodes.Add(null);
            UpdateTrackUi();
            UpdateButtonsUi();
            OnAfterburnerCountChanged?.Invoke(0);
        }

        public void SetWeapon(string code)
        {
            weaponCode = string.IsNullOrWhiteSpace(code) ? "X" : code.Trim().ToUpperInvariant();
            OnWeaponChanged?.Invoke(weaponCode);
        }

        public void ConfigureManeuverSlots(int slots)
        {
            int count = Mathf.Max(1, slots);
            afterburnerMode = count > 1;
            activeManeuverIndex = 0;
            preparedManeuvers.Clear();
            preparedCodes.Clear();
            for (int i = 0; i < count; i++)
            {
                preparedManeuvers.Add(null);
                preparedCodes.Add(null);
            }
            UpdateTrackUi();
            UpdateButtonsUi();
            OnAfterburnerCountChanged?.Invoke(0);
        }

        public void SetPreparedManeuverAtActive(ManeuverProfile profile)
        {
            SetPreparedManeuverAtActive(profile, null);
        }

        public void SetPreparedManeuverAtActive(ManeuverProfile profile, string code)
        {
            EnsurePreparedSlots();
            preparedManeuvers[activeManeuverIndex] = profile;
            if (preparedCodes != null && activeManeuverIndex >= 0 && activeManeuverIndex < preparedCodes.Count)
                preparedCodes[activeManeuverIndex] = code;
            UpdateTrackUi();
            UpdateButtonsUi();
        }

        public void ConfirmManeuvers()
        {
            if (GetFilledCount() == 0) return;

            var list = new List<ManeuverProfile>(preparedManeuvers);
            OnManeuversConfirmed?.Invoke(list);
            DeselectIfSelected(buttonConfirm);
        }
        public void IncludeManeuver()
        {
            if (!afterburnerMode) return;
            if (activeManeuverIndex != 0) return;
            if (GetManeuverAt(0) == null) return;

            activeManeuverIndex = 1;
            UpdateTrackUi();
            UpdateButtonsUi();
            OnAfterburnerCountChanged?.Invoke(1);
            OnAfterburnerIncluded?.Invoke();
        }

        public void RemoveManeuver()
        {
            if (!afterburnerMode) return;
            var savedProfile = GetManeuverAt(0);
            var savedCode = GetPreparedCodeAt(0);
            if (preparedManeuvers.Count > 1) preparedManeuvers[1] = null;
            if (preparedCodes.Count > 1) preparedCodes[1] = null;
            activeManeuverIndex = 0;
            OnAfterburnerCountChanged?.Invoke(GetFilledCount());
            OnAfterburnerStepBack?.Invoke();

            if (savedProfile != null || !string.IsNullOrWhiteSpace(savedCode))
            {
                EnsurePreparedSlots();
                if (preparedManeuvers.Count > 0) preparedManeuvers[0] = savedProfile;
                if (preparedCodes.Count > 0) preparedCodes[0] = savedCode;
            }

            UpdateTrackUi();
            UpdateButtonsUi();
        }

        public void ClearButtonSelections()
        {
            _clearSelectionOnce = true;
            StartCoroutine(ClearSelectionEndOfFrame());

            if (buttonConfirm != null)
            {
                DeselectIfSelected(buttonConfirm);
                buttonConfirm.interactable = false;
                buttonConfirm.gameObject.SetActive(false);
            }
            if (buttonInclude != null)
            {
                buttonInclude.interactable = false;
                buttonInclude.gameObject.SetActive(false);
            }
            if (buttonRemove != null)
            {
                buttonRemove.interactable = false;
                buttonRemove.gameObject.SetActive(false);
            }
            _confirmVisible = false;
        }

        public void RequestClearSelectionOnce()
        {
            _clearSelectionOnce = true;
            StartCoroutine(ClearSelectionEndOfFrame());
        }

        private IEnumerator ClearSelectionEndOfFrame()
        {
            yield return null;
            if (!_clearSelectionOnce) yield break;
            _clearSelectionOnce = false;

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            DeselectIfSelected(buttonConfirm);
            DeselectIfSelected(buttonInclude);
            DeselectIfSelected(buttonRemove);
        }

        private static void DeselectIfSelected(Button button)
        {
            if (button == null || EventSystem.current == null) return;
            
            if (EventSystem.current.currentSelectedGameObject == button.gameObject)
            {
                Debug.Log($"[PanelGroup] Force deselecting {button.name}");
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        public void RestoreFocusIfConfirmSelected(GameObject fallback)
        {
            if (fallback == null) return;
            if (buttonConfirm == null || EventSystem.current == null) return;
            if (EventSystem.current.currentSelectedGameObject == buttonConfirm.gameObject)
                EventSystem.current.SetSelectedGameObject(fallback);
        }

        public void SetLastSelection(GameObject selection)
        {
            if (selection == null) return;
            _lastSelection = selection;
        }

        private void EnsurePreparedSlots()
        {
            if (preparedManeuvers == null)
                preparedManeuvers = new List<ManeuverProfile>();
            if (preparedManeuvers.Count == 0)
                preparedManeuvers.Add(null);
            if (preparedCodes == null)
                preparedCodes = new List<string>();
            while (preparedCodes.Count < preparedManeuvers.Count)
                preparedCodes.Add(null);
            if (activeManeuverIndex < 0) activeManeuverIndex = 0;
            if (activeManeuverIndex >= preparedManeuvers.Count)
                activeManeuverIndex = preparedManeuvers.Count - 1;
        }

        private ManeuverProfile ResolveDefaultProfile()
        {
            if (maneuverManager == null)
                maneuverManager = FindFirstObjectByType<ManeuverManager>();

            ManeuverProfile profile = null;
            if (maneuverManager != null)
                profile = maneuverManager.GetDefaultProfile();
            if (profile == null)
                profile = ManeuverProfileCatalog.Resolve("");
            return profile;
        }

        private void UpdateTrackUi()
        {
            if (!afterburnerMode)
            {
                if (trackDefault != null) trackDefault.SetActive(true);
                if (trackAfterBurner != null) trackAfterBurner.SetActive(false);
                ApplyTrackSingle(trackDImage, trackDText, GetManeuverAt(0), useAfterburnerSprite: false);
                ApplyTrackSingle(trackAImage, trackAText, null, useAfterburnerSprite: false);
                ApplyTrackSingle(trackBImage, trackBText, null, useAfterburnerSprite: false);
                return;
            }

            if (trackDefault != null) trackDefault.SetActive(false);
            if (trackAfterBurner != null) trackAfterBurner.SetActive(true);

            ApplyTrackSingle(trackAImage, trackAText, GetManeuverAt(0), useAfterburnerSprite: false);
            ApplyTrackSingle(trackBImage, trackBText, GetManeuverAt(1), useAfterburnerSprite: true);
            ApplyTrackSingle(trackDImage, trackDText, null, useAfterburnerSprite: false);
        }

        private void UpdateButtonsUi()
        {
            int filled = GetFilledCount();

            if (buttonInclude != null)
            {
                bool showInclude = afterburnerMode && activeManeuverIndex == 0 && GetManeuverAt(0) != null;
                buttonInclude.gameObject.SetActive(showInclude);
                buttonInclude.interactable = showInclude;
            }

            if (buttonRemove != null)
            {
                bool showRemove = afterburnerMode && activeManeuverIndex == 1 && filled >= 1;
                buttonRemove.gameObject.SetActive(showRemove);
                buttonRemove.interactable = showRemove;
            }

            if (buttonConfirm != null)
            {
                bool showConfirm = !afterburnerMode
                    ? GetManeuverAt(0) != null
                    : filled >= 2;

                buttonConfirm.gameObject.SetActive(showConfirm);
                buttonConfirm.interactable = showConfirm;
                
                // Check condition BEFORE activating (so we know if we just appeared)
                bool newlyAppearing = showConfirm && (!_confirmVisible || !buttonConfirm.gameObject.activeSelf);

                // 1. Activate Button First (Let Unity do its OnEnable checks)
                buttonConfirm.gameObject.SetActive(showConfirm);
                buttonConfirm.interactable = showConfirm;
                
                // 2. Force Clean State if we just appeared
                if (newlyAppearing)
                {
                    // Debug.Log("[PanelGroup] Resetting Confirm Button Visuals...");

                    // Hard Reset Color Property (Unity UI)
                    if (buttonConfirm.image != null)
                        buttonConfirm.image.color = buttonConfirm.colors.normalColor;

                    // Reset CanvasRenderer
                    if (buttonConfirm.targetGraphic != null)
                        buttonConfirm.targetGraphic.CrossFadeColor(buttonConfirm.colors.normalColor, 0f, true, true);
                    
                    // Force State Machine Reset by toggling interactable
                    buttonConfirm.interactable = false;
                    buttonConfirm.interactable = true;

                    // Apply Correct Visuals (Text/Group) - DO THIS LAST
                    if (selectionGroup != null)
                    {
                        // Debug.Log($"[PanelGroup] Clearing SelectionGroup. Options Count: {selectionGroup.GetOptionsCount()}");
                        selectionGroup.ClearSelection();
                        
                        // Force check if Confirm is actually being reset by group
                        // var confirmOption = selectionGroup.GetOptionForButton(buttonConfirm);
                        // if (confirmOption == null) Debug.LogWarning("[PanelGroup] Confirm button NOT found in SelectionGroup options!");
                    }
                    else
                    {
                        Debug.LogWarning("[PanelGroup] SelectionGroup missing! Text color might be wrong.");
                    }

                    // 5. ULTIMATE FALLBACK: Manually fix text if Group failed
                    var text = buttonConfirm.GetComponentInChildren<TMP_Text>(true);
                    if (text != null)
                    {
                        // Use configured color if possible, otherwise default to black
                        text.color = selectionGroup != null ? selectionGroup.NormalTextColor : Color.black;
                        text.fontStyle = FontStyles.Normal;
                    }
                }
                
                if (showConfirm && !_confirmVisible)
                    RestoreFocusIfConfirmSelected(_lastSelection);
                _confirmVisible = showConfirm;
            }

            if (EventSystem.current != null)
            {
                var current = EventSystem.current.currentSelectedGameObject;
                if (current != null)
                {
                    if ((buttonInclude != null && current == buttonInclude.gameObject && !buttonInclude.gameObject.activeSelf)
                        || (buttonRemove != null && current == buttonRemove.gameObject && !buttonRemove.gameObject.activeSelf)
                        || (buttonConfirm != null && current == buttonConfirm.gameObject && !buttonConfirm.gameObject.activeSelf))
                    {
                        EventSystem.current.SetSelectedGameObject(null);
                    }
                }
            }

            if (afterburnerMode)
                OnAfterburnerCountChanged?.Invoke(filled);

            UpdateArrowsUi();
        }

        private void UpdateArrowsUi()
        {
            if (arrowStep0 != null)
                arrowStep0.SetActive(afterburnerMode);
            if (arrowStep1 != null)
                arrowStep1.SetActive(afterburnerMode && activeManeuverIndex == 1);
            if (arrowSingle != null)
                arrowSingle.SetActive(!afterburnerMode);
        }

        private int GetFilledCount()
        {
            int count = 0;
            if (preparedManeuvers == null) return 0;
            for (int i = 0; i < preparedManeuvers.Count; i++)
            {
                if (preparedManeuvers[i] != null)
                    count++;
            }
            return count;
        }

        private ManeuverProfile GetManeuverAt(int index)
        {
            if (preparedManeuvers == null) return null;
            if (index < 0 || index >= preparedManeuvers.Count) return null;
            return preparedManeuvers[index];
        }

        public string GetPreparedCodeAt(int index)
        {
            if (preparedCodes == null) return null;
            if (index < 0 || index >= preparedCodes.Count) return null;
            return preparedCodes[index];
        }

        private void ApplyTrackSingle(Image image, TMP_Text text, ManeuverProfile profile, bool useAfterburnerSprite)
        {
            if (image != null)
            {
                Sprite sprite = null;
                if (profile != null)
                    sprite = useAfterburnerSprite ? profile.afterburnerSprite : profile.defaultSprite;
                image.sprite = sprite;
                if (image.sprite != null)
                {
                    image.enabled = true;
                    image.SetNativeSize();
                }
                else
                {
                    image.enabled = false;
                }

                var rt = image.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
            }

            if (text != null)
            {
                if (profile == null)
                {
                    text.text = "";
                    text.enabled = false;
                }
                else
                {
                    text.text = BuildManeuverLabel(profile);
                    text.enabled = true;
                }
            }
        }

        private static string BuildManeuverLabel(ManeuverProfile profile)
        {
            string name = string.IsNullOrWhiteSpace(profile.displayName) ? profile.maneuverId : profile.displayName;
            string g = FormatG(profile.gForce);
            string mach = FormatMach(profile.mach);
            int evasion = (int)profile.evasionPenalty;
            return $"{name}\n{g}G | {mach} | Eva: -{evasion}";
        }

        private static string FormatG(float g)
        {
            float rounded = Mathf.Round(g);
            if (Mathf.Abs(g - rounded) < 0.001f)
                return ((int)rounded).ToString(CultureInfo.InvariantCulture);
            return g.ToString("0.0", CultureInfo.InvariantCulture);
        }

        private static string FormatMach(Mach tier)
        {
            float m = ((int)tier) / 10f;
            return $"{m.ToString("0.0", CultureInfo.InvariantCulture)}M";
        }
    }
}
