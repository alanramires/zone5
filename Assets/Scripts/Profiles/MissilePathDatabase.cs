using System.Collections.Generic;
using UnityEngine;

namespace Zone5
{
    [CreateAssetMenu(
        menuName = "Zone5/Missile Path Database",
        fileName = "MissilePathDatabase"
    )]
    public class MissilePathDatabase : ScriptableObject
    {
        public string defaultPathId = "M10F";
        public List<MissilePathProfile> paths = new();

        private Dictionary<string, MissilePathProfile> _byKey;

        private void BuildIndex()
        {
            _byKey = new Dictionary<string, MissilePathProfile>();
            if (paths == null) return;

            foreach (var profile in paths)
            {
                if (profile == null) continue;
                AddKey(profile.pathId, profile);
                foreach (var key in profile.GetAllKeys())
                {
                    AddKey(key, profile);
                }
            }
        }

        private void AddKey(string raw, MissilePathProfile profile)
        {
            if (string.IsNullOrWhiteSpace(raw) || profile == null) return;
            string key = raw.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(key)) return;
            _byKey[key] = profile;
        }

        public MissilePathProfile Resolve(string raw)
        {
            if (_byKey == null) BuildIndex();
            if (_byKey == null || _byKey.Count == 0) return GetDefault();

            string s = (raw ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(s)) return GetDefault();

            int plus = s.IndexOf('+');
            if (plus >= 0) s = s.Substring(0, plus).Trim();

            if (_byKey.TryGetValue(s, out var profile)) return profile;
            return GetDefault();
        }

        private MissilePathProfile GetDefault()
        {
            if (_byKey == null) BuildIndex();
            if (_byKey != null && !string.IsNullOrEmpty(defaultPathId))
            {
                string key = defaultPathId.Trim().ToUpperInvariant();
                if (_byKey.TryGetValue(key, out var profile)) return profile;
            }

            if (paths != null && paths.Count > 0)
                return paths[0];

            return null;
        }
    }
}
