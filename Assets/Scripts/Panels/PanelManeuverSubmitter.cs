using System.Collections.Generic;
using UnityEngine;

namespace Zone5
{
    public class PanelManeuverSubmitter : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private PanelGroupController panelGroup;
        [SerializeField] private PanelMancheController panelManche;
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private MatchControllerMvp matchController;

        private void OnEnable()
        {
            if (panelGroup == null)
                panelGroup = FindFirstObjectByType<PanelGroupController>();
            if (panelManche == null)
                panelManche = FindFirstObjectByType<PanelMancheController>();
            if (turnManager == null)
                turnManager = FindFirstObjectByType<TurnManager>();
            if (matchController == null)
                matchController = FindFirstObjectByType<MatchControllerMvp>();

            if (panelGroup != null)
                panelGroup.OnManeuversConfirmed += HandleManeuversConfirmed;
        }

        private void OnDisable()
        {
            if (panelGroup != null)
                panelGroup.OnManeuversConfirmed -= HandleManeuversConfirmed;
        }

        private void HandleManeuversConfirmed(List<ManeuverProfile> maneuvers)
        {
            if (turnManager == null || turnManager.sheet == null) return;
            if (turnManager.sheet.phase != GameEnum.TurnState.SelectManeuver) return;
            if (matchController == null) return;

            var unit = ResolveCurrentUnit();
            if (unit == null) return;

            var codes = BuildCodes(maneuvers);
            if (codes.Count == 0) return;

            matchController.SubmitManeuverList(unit.playerId, codes);
            if (panelManche != null)
                panelManche.ResetCockpitToDefault();
            if (panelGroup != null)
                panelGroup.ClearButtonSelections();
        }

        private List<string> BuildCodes(List<ManeuverProfile> maneuvers)
        {
            var codes = new List<string>();
            if (maneuvers == null) return codes;

            for (int i = 0; i < maneuvers.Count; i++)
            {
                var profile = maneuvers[i];
                if (profile == null) continue;
                string id = string.IsNullOrWhiteSpace(profile.maneuverId) ? profile.name : profile.maneuverId;
                if (string.IsNullOrWhiteSpace(id)) continue;
                codes.Add(id.Trim().ToUpperInvariant());
            }

            return codes;
        }

        private AircraftUnit ResolveCurrentUnit()
        {
            var alive = GetAliveUnitsSorted();
            if (alive.Count == 0) return null;

            if (turnManager == null || turnManager.sheet == null)
                return alive[0];

            for (int i = 0; i < alive.Count; i++)
            {
                var unit = alive[i];
                if (unit == null || unit.currentHp <= 0) continue;
                var row = turnManager.GetOrCreatePlayerRow(unit.playerId);
                if (row == null) continue;
                if (!row.maneuverReady)
                    return unit;
            }

            return alive[0];
        }

        private static List<AircraftUnit> GetAliveUnitsSorted()
        {
            var list = new List<AircraftUnit>();
            var units = FindObjectsByType<AircraftUnit>(FindObjectsSortMode.None);
            foreach (var u in units)
            {
                if (u != null && u.currentHp > 0)
                    list.Add(u);
            }

            list.Sort((a, b) =>
            {
                int t = a.teamId.CompareTo(b.teamId);
                if (t != 0) return t;
                return a.playerId.CompareTo(b.playerId);
            });

            return list;
        }
    }
}
