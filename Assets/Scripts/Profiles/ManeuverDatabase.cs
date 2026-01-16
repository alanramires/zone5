using System.Collections.Generic;
using UnityEngine;

namespace Zone5
{
    [CreateAssetMenu(
        menuName = "Zone5/Maneuver Database",
        fileName = "ManeuverDatabase"
    )]
    public class ManeuverDatabase : ScriptableObject
    {
        public string defaultManeuverId = "1G12F";
        public List<ManeuverProfile> maneuvers = new();

        private Dictionary<string, ManeuverProfile> _byKey;

        private void BuildIndex()
        {
            _byKey = new Dictionary<string, ManeuverProfile>();
            if (maneuvers == null) return;

            foreach (var profile in maneuvers)
            {
                if (profile == null) continue;
                AddKey(profile.maneuverId, profile);
                foreach (var key in profile.GetAllKeys())
                {
                    AddKey(key, profile);
                }
            }
        }

        private void AddKey(string raw, ManeuverProfile profile)
        {
            if (string.IsNullOrWhiteSpace(raw) || profile == null) return;
            string key = NormalizeKey(raw);
            if (string.IsNullOrEmpty(key)) return;
            _byKey[key] = profile;
        }

        public ManeuverProfile Resolve(string raw)
        {
            if (TryResolve(raw, out var profile)) return profile;
            return GetDefault();
        }

        public bool TryResolve(string raw, out ManeuverProfile profile)
        {
            profile = null;
            if (_byKey == null) BuildIndex();
            if (_byKey == null || _byKey.Count == 0) return false;

            string key = NormalizeKey(raw);
            if (string.IsNullOrEmpty(key)) return false;

            return _byKey.TryGetValue(key, out profile);
        }

        private ManeuverProfile GetDefault()
        {
            if (_byKey == null) BuildIndex();
            if (_byKey != null && !string.IsNullOrEmpty(defaultManeuverId))
            {
                string key = NormalizeKey(defaultManeuverId);
                if (_byKey.TryGetValue(key, out var profile)) return profile;
            }

            if (maneuvers != null && maneuvers.Count > 0)
                return maneuvers[0];

            return null;
        }

        private static string NormalizeKey(string raw)
        {
            string s = (raw ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(s)) return s;
            int plus = s.IndexOf('+');
            if (plus >= 0) s = s.Substring(0, plus).Trim();
            return s;
        }
    }
}
