using UnityEngine;

namespace Zone5
{
    public class ManeuverManager : MonoBehaviour
    {
        [Header("Gameplay (MVP test)")]
        public ManeuverDatabase maneuverDb;
        public float fuWorld = 1f;

        public ManeuverProfile Resolve(string raw)
        {
            if (maneuverDb != null && maneuverDb.TryResolve(raw, out var profile))
                return profile;

            if (ManeuverProfileCatalog.TryResolve(raw, out var catalogProfile))
                return catalogProfile;

            return null;
        }

        public ManeuverProfile GetDefaultProfile()
        {
            if (maneuverDb != null)
                return maneuverDb.Resolve("");
            return ManeuverProfileCatalog.Resolve("");
        }
    }
}
