using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zone5
{
    [Serializable]
    public class TurnSheet
    {
        public string matchName;
        public int turnIndex = 1;
        public GameEnum.TurnState phase = GameEnum.TurnState.SelectManeuver;
        public List<PlayerRow> rows = new List<PlayerRow>();

        [Serializable]
        public class PlayerRow
        {
            public int playerId;
            public bool isAlive = true;

            // Persistent Info (for UI when unit is dead)
            public string callSign;
            public string aircraftName;
            public string unitId;
            public Sprite aircraftSprite;
            public int cachedHp;  // Last known HP

            public string maneuverRaw;
            public string maneuverComboRaw;
            public bool maneuverReady;

            public string weaponCode;
            public bool weaponReady;

            public string missilePath;
            public bool missileReady;

            public int score;
            public int assists;
        }

        // Checks if all alive players have indicated readiness for the given phase
        public bool AllReadyForPhase(GameEnum.TurnState phaseState)
        {
            if (rows == null || rows.Count == 0)
                return true;

            switch (phaseState)
            {
                case GameEnum.TurnState.SelectManeuver:
                case GameEnum.TurnState.WaitManeuverConfirm:
                    for (int i = 0; i < rows.Count; i++)
                    {
                        var r = rows[i];
                        if (r == null || !r.isAlive) continue;
                        if (!r.maneuverReady) return false;
                    }
                    return true;
                case GameEnum.TurnState.DeclareWeapon:
                case GameEnum.TurnState.WaitWeaponDeclare:
                    for (int i = 0; i < rows.Count; i++)
                    {
                        var r = rows[i];
                        if (r == null || !r.isAlive) continue;
                        if (!r.weaponReady) return false;
                    }
                    return true;
                case GameEnum.TurnState.SelectMissileProfile:
                case GameEnum.TurnState.WaitMissileSelection:
                    for (int i = 0; i < rows.Count; i++)
                    {
                        var r = rows[i];
                        if (r == null || !r.isAlive) continue;
                        
                        // Fix: Only check readiness if they declared 'M'
                        // Use Trim/Upper just in case, though Sanitize ensures 'M'
                        string w = (r.weaponCode ?? "").Trim().ToUpperInvariant();
                        if (w == "M")
                        {
                            if (!r.missileReady) return false;
                        }
                    }
                    return true;
                default:
                    return true;
            }
        }

        // Applies default selections for all players who haven't made a choice yet, based on the phase
        public void ApplyDefaultsForPhase(GameEnum.TurnState phaseState)
        {
            if (rows == null) return;

            switch (phaseState)
            {
                
                case GameEnum.TurnState.SelectManeuver:
                case GameEnum.TurnState.WaitManeuverConfirm:
                    for (int i = 0; i < rows.Count; i++)
                    {
                        var r = rows[i];
                        if (r == null || !r.isAlive || r.maneuverReady) continue;
                        if (string.IsNullOrWhiteSpace(r.maneuverRaw))
                            r.maneuverRaw = "1G12";
                        r.maneuverReady = true;
                    }
                    break;
                case GameEnum.TurnState.DeclareWeapon:
                case GameEnum.TurnState.WaitWeaponDeclare:
                    for (int i = 0; i < rows.Count; i++)
                    {
                        var r = rows[i];
                        if (r == null || !r.isAlive || r.weaponReady) continue;
                        if (string.IsNullOrWhiteSpace(r.weaponCode))
                            r.weaponCode = "X";
                        r.weaponReady = true;
                    }
                    break;
                case GameEnum.TurnState.SelectMissileProfile:
                case GameEnum.TurnState.WaitMissileSelection:
                    for (int i = 0; i < rows.Count; i++)
                    {
                        var r = rows[i];
                        if (r == null || !r.isAlive || r.missileReady) continue;
                        if (string.IsNullOrWhiteSpace(r.missilePath))
                            r.missileReady = true;
                    }
                    break;
            }
        }

        // Prepares the sheet for a new turn: clears all maneuver/weapon/missile selections and readiness
        public void ResetForNewTurn()
        {
            if (rows == null) return;

            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r == null) continue;
                r.maneuverRaw = null;
                r.maneuverComboRaw = null;
                r.maneuverReady = false;
                r.weaponCode = null;
                r.weaponReady = false;
                r.missilePath = null;
                r.missileReady = false;
            }
        }
    }
}
