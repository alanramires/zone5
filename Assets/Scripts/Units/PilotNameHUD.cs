using TMPro;
using UnityEngine;

namespace Zone5
{
    public class PilotNameHUD : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private AircraftUnit target;
        [SerializeField] private TMP_Text label;

        [Header("Layout")]
        [SerializeField] private Vector3 offset = new Vector3(0f, -0.6f, 0f);
        [SerializeField] private bool keepWorldRotation = true;

        [Header("Color")]
        [SerializeField] private bool tintByTeam = true;
        [SerializeField] private bool useOverrideColor = false;
        [SerializeField] private Color overrideColor = Color.white;

        private string _lastCallsign;
        private int _lastTeamId = int.MinValue;

        private void Reset()
        {
            if (label == null)
                label = GetComponentInChildren<TMP_Text>();
            if (target == null)
                target = GetComponentInParent<AircraftUnit>();
        }

        private void Awake()
        {
            if (label == null)
                label = GetComponentInChildren<TMP_Text>();
            if (target == null)
                target = GetComponentInParent<AircraftUnit>();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            transform.position = target.transform.position + offset;
            if (keepWorldRotation)
                transform.rotation = Quaternion.identity;

            // Refresh if runtime data arrives after prefab init.
            if (NeedsRefresh())
                Refresh();
        }

        public void SetTarget(AircraftUnit unit)
        {
            target = unit;
            Refresh();
        }

        public void Refresh()
        {
            if (label == null) return;

            if (target != null)
            {
                string callsign = !string.IsNullOrWhiteSpace(target.callSign) ? target.callSign : target.unitId;
                if (!string.IsNullOrWhiteSpace(callsign))
                    label.text = callsign;

                if (useOverrideColor)
                    label.color = overrideColor;
                else if (tintByTeam)
                    label.color = GameEnum.GameColors.GetColorForTeam(target.teamId);

                _lastCallsign = callsign;
                _lastTeamId = target.teamId;
            }
        }

        private bool NeedsRefresh()
        {
            if (label == null || target == null) return false;
            string callsign = !string.IsNullOrWhiteSpace(target.callSign) ? target.callSign : target.unitId;
            if (!string.Equals(callsign, _lastCallsign)) return true;
            if (tintByTeam && target.teamId != _lastTeamId) return true;
            return false;
        }
    }
}
