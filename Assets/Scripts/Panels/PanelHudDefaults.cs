using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zone5
{
    public class PanelHudDefaults : MonoBehaviour
    {
        [Header("Panel Parts")]
        [SerializeField] private GameObject trackDefault;
        [SerializeField] private GameObject trackAfterBurner;
        [SerializeField] private GameObject buttonConfirm;
        [SerializeField] private GameObject buttonInclude;
        [SerializeField] private GameObject buttonRemove;
        [SerializeField] private Image trackDImage;
        [SerializeField] private TMP_Text trackDText;

        [Header("Maneuver")]
        [SerializeField] private PanelGroupController panelGroup;
        [SerializeField] private ManeuverManager maneuverManager;
        [SerializeField] private ManeuverProfile defaultManeuverProfile;

        private UnitSpawner _spawner;

        private void OnEnable()
        {
            BindSpawner();
        }

        private void Start()
        {
            ApplyDefaults();
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
                _spawner.OnSpawnCompleted += ApplyDefaults;
        }

        private void UnbindSpawner()
        {
            if (_spawner == null) return;
            _spawner.OnSpawnCompleted -= ApplyDefaults;
            _spawner = null;
        }

        public void ApplyDefaults()
        {
            if (trackDefault != null) trackDefault.SetActive(true);
            if (trackAfterBurner != null) trackAfterBurner.SetActive(false);
            if (buttonConfirm != null)
            {
                buttonConfirm.SetActive(false);
                var confirmButton = buttonConfirm.GetComponent<Button>();
                if (confirmButton != null) confirmButton.interactable = false;
            }
            if (buttonInclude != null)
            {
                buttonInclude.SetActive(false);
                var includeButton = buttonInclude.GetComponent<Button>();
                if (includeButton != null) includeButton.interactable = false;
            }
            if (buttonRemove != null)
            {
                buttonRemove.SetActive(false);
                var removeButton = buttonRemove.GetComponent<Button>();
                if (removeButton != null) removeButton.interactable = false;
            }

            if (panelGroup != null)
            {
                panelGroup.InitializeDefaults();
                return;
            }

            ManeuverProfile profile = ResolveProfile(defaultManeuverProfile);
            ApplyTrack(profile);
        }

        private ManeuverProfile ResolveProfile(ManeuverProfile profile)
        {
            if (profile != null) return profile;
            if (maneuverManager == null)
                maneuverManager = FindFirstObjectByType<ManeuverManager>();

            ManeuverProfile resolved = null;
            if (maneuverManager != null)
            {
                resolved = maneuverManager.GetDefaultProfile();
            }

            if (resolved == null)
                resolved = ManeuverProfileCatalog.Resolve("");

            return resolved;
        }

        private void ApplyTrack(ManeuverProfile profile)
        {
            if (trackDImage != null)
            {
                trackDImage.sprite = profile != null ? profile.defaultSprite : null;
                if (trackDImage.sprite != null)
                    trackDImage.SetNativeSize();

                var rt = trackDImage.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
            }

            if (trackDText != null)
            {
                if (profile == null)
                {
                    trackDText.text = "Maneuver";
                    return;
                }

                string name = string.IsNullOrWhiteSpace(profile.displayName) ? profile.maneuverId : profile.displayName;
                string g = FormatG(profile.gForce);
                string mach = FormatMach(profile.mach);
                int evasion = (int)profile.evasionPenalty;
                string stats = $"{g}G {mach} Evasion: -{evasion}";
                trackDText.text = $"{name}\n{stats}";
            }
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
