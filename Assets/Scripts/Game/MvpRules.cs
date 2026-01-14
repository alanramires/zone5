using System;

namespace Zone5
{
    public static class MvpRules
    {
        public const bool IsMvp = true;

        public static string SanitizeManeuver(string raw)
        {
            if (!IsMvp) return raw;
            if (string.IsNullOrWhiteSpace(raw)) return raw;

            int plus = raw.IndexOf('+');
            if (plus >= 0)
                raw = raw.Substring(0, plus);

            return raw.Trim();
        }

        public static string SanitizeWeapon(string raw)
        {
            if (!IsMvp) return raw;
            if (string.IsNullOrWhiteSpace(raw)) return "X";

            string s = raw.Trim().ToUpperInvariant();
            if (s == "M" || s == "MISSILE") return "M";
            if (s == "X" || s == "NONE" || s == "N") return "X";
            return "N";
        }
    }
}
