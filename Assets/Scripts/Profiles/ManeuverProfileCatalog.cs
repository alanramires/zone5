using System.Collections.Generic;
using UnityEngine;

namespace Zone5
{
    public static class ManeuverProfileCatalog
    {
        public const string DefaultId = "1G12F";

        private static List<ManeuverProfile> _all;
        private static Dictionary<string, ManeuverProfile> _byKey;

        private static void BuildIndex()
        {
            _all = new List<ManeuverProfile>(Resources.LoadAll<ManeuverProfile>("DB/Maneuvers"));
            _byKey = new Dictionary<string, ManeuverProfile>();

            foreach (var p in _all)
            {
                if (p == null) continue;
                foreach (var k in p.GetAllKeys())
                {
                    Add(_byKey, k, p);
                }
            }
        }

        private static void Add(Dictionary<string, ManeuverProfile> dict, string key, ManeuverProfile profile)
        {
            if (dict == null || profile == null) return;
            if (string.IsNullOrWhiteSpace(key)) return;
            dict[NormalizeKey(key)] = profile;
        }

        public static ManeuverProfile Resolve(string raw)
        {
            if (TryResolve(raw, out var p)) return p;
            return GetDefault();
        }

        public static bool TryResolve(string raw, out ManeuverProfile profile)
        {
            profile = null;
            if (_byKey == null) BuildIndex();
            if (_byKey == null || _byKey.Count == 0) return false;

            string key = NormalizeKey(raw);
            if (string.IsNullOrEmpty(key)) return false;

            return _byKey.TryGetValue(key, out profile);
        }

        private static ManeuverProfile GetDefault()
        {
            if (_byKey == null) BuildIndex();
            if (_byKey != null && !string.IsNullOrEmpty(DefaultId))
            {
                string key = NormalizeKey(DefaultId);
                if (_byKey.TryGetValue(key, out var p)) return p;
            }
            return (_all != null && _all.Count > 0) ? _all[0] : null;
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
