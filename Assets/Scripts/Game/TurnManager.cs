using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zone5
{
    public class TurnManager : MonoBehaviour
    {
        public event Action OnStateChanged;
        public event Action OnSheetChanged;

        [Header("Turn")]
        [SerializeField] private GameEnum.TurnState initialState = GameEnum.TurnState.SelectManeuver;

        [Header("Sheet")]
        public TurnSheet sheet = new TurnSheet();

        [Header("Players")]
        public bool autoRegisterFromScene = true;

        [Header("Debug")]
        public bool logStateChanges = true;
        [Header("Timing")]
        public float endRoundDelaySeconds = 0.0f;

        [Header("Match")]
        public bool matchEnded = false;
        public int winnerTeam = -1;

        
        private void Start()
        {
            if (sheet == null)
                sheet = new TurnSheet();

            if (sheet.rows == null)
                sheet.rows = new List<TurnSheet.PlayerRow>();

            if (sheet.turnIndex <= 0)
                sheet.turnIndex = 1;

            sheet.phase = initialState;

            if (autoRegisterFromScene) RegisterFromScene();
            EnterState(sheet.phase);
        }

        // Registers all AircraftUnit instances in the scene with valid player IDs
        private void RegisterFromScene()
        {
            var units = FindObjectsByType<AircraftUnit>(FindObjectsSortMode.None);
            for (int i = 0; i < units.Length; i++)
            {
                var unit = units[i];
                if (unit == null || unit.playerId <= 0) continue;
                RegisterUnit(unit);
            }
        }

        // Retrieves existing or creates new PlayerRow for given playerId
        public TurnSheet.PlayerRow GetOrCreatePlayerRow(int playerId)
        {
            if (playerId <= 0) return null;
            if (sheet == null) return null;
            if (sheet.rows == null)
                sheet.rows = new List<TurnSheet.PlayerRow>();

            for (int i = 0; i < sheet.rows.Count; i++)
            {
                var row = sheet.rows[i];
                if (row != null && row.playerId == playerId)
                    return row;
            }

            var data = new TurnSheet.PlayerRow { playerId = playerId };
            sheet.rows.Add(data);
            return data;
        }

        // Registers or updates a player's row based on the given AircraftUnit
        public void RegisterUnit(AircraftUnit unit)
        {
            if (unit == null) return;
            var row = GetOrCreatePlayerRow(unit.playerId);
            if (row == null) return;
            
            row.isAlive = unit.currentHp > 0;
            row.callSign = !string.IsNullOrEmpty(unit.callSign) ? unit.callSign : unit.unitId;
            row.aircraftName = unit.UnitData != null ? unit.UnitData.unitName : "Fighter";
            row.cachedHp = unit.currentHp;

            NotifySheetChanged();
        }

        // Sets a player's alive status
        public void SetAlive(int playerId, bool isAlive)
        {
            var row = GetOrCreatePlayerRow(playerId);
            if (row == null) return;
            row.isAlive = isAlive;
            NotifySheetChanged();
        }

        // Notifies listeners that the turn sheet has changed
        public void NotifySheetChanged()
        {
            OnSheetChanged?.Invoke();
        }

        // Advances the turn state if all players are ready for the current phase
        public void AdvanceButton()
        {
            if (matchEnded) return;
            if (sheet == null) return;
            sheet.ApplyDefaultsForPhase(sheet.phase);
            NotifySheetChanged();
            TryAdvance();
        }

        // Attempts to advance the turn state if all players are ready
        public void TryAdvance()
        {
            if (matchEnded) return;
            if (sheet == null) return;
            if (sheet.AllReadyForPhase(sheet.phase))
                NextState();
        }

        // Advances to the next turn state based on the current state and game logic
        private void NextState()
        {
            if (matchEnded) return;
            if (sheet == null) return;

            GameEnum.TurnState nextState;
            switch (sheet.phase)
            {
                case GameEnum.TurnState.SelectManeuver:
                    // Skip WaitManeuverConfirm for MVP Hotseat
                    nextState = GameEnum.TurnState.DeclareWeapon;
                    break;
                case GameEnum.TurnState.WaitManeuverConfirm:
                    nextState = GameEnum.TurnState.DeclareWeapon;
                    break;
                case GameEnum.TurnState.DeclareWeapon:
                    // Skip WaitWeaponDeclare for MVP Hotseat
                    nextState = GameEnum.TurnState.RevealAndMoveFighters;
                    break;
                case GameEnum.TurnState.WaitWeaponDeclare:
                    nextState = GameEnum.TurnState.RevealAndMoveFighters;
                    break;
                case GameEnum.TurnState.RevealAndMoveFighters:
                    nextState = GameEnum.TurnState.ResolveCollisions;
                    break;
                case GameEnum.TurnState.ResolveCollisions:
                    if (HasAnyActiveMissile())
                        nextState = GameEnum.TurnState.SelectMissileProfile;
                    else
                        nextState = GameEnum.TurnState.ApplyDamageAndCheckVictory;
                    break;
                case GameEnum.TurnState.SelectMissileProfile:
                    // Skip WaitMissileSelection for MVP Hotseat
                    nextState = GameEnum.TurnState.SpawnMissilesAndResolveEvasion;
                    break;
                case GameEnum.TurnState.WaitMissileSelection:
                    nextState = GameEnum.TurnState.SpawnMissilesAndResolveEvasion;
                    break;
                case GameEnum.TurnState.SpawnMissilesAndResolveEvasion:
                    nextState = GameEnum.TurnState.ApplyDamageAndCheckVictory;
                    break;
                case GameEnum.TurnState.ApplyDamageAndCheckVictory:
                    nextState = GameEnum.TurnState.EndRoundAndAdvance;
                    break;
                case GameEnum.TurnState.EndRoundAndAdvance:
                    sheet.turnIndex += 1;
                    nextState = GameEnum.TurnState.SelectManeuver;
                    break;
                default:
                    nextState = GameEnum.TurnState.SelectManeuver;
                    break;
            }

            sheet.phase = nextState;
            EnterState(sheet.phase);
        }

        // Enters a specific turn state and performs associated actions
        private void EnterState(GameEnum.TurnState state)
        {
            if (logStateChanges)
                Debug.Log($"[TurnManager] Turn={sheet.turnIndex} State={state}");

            switch (state)
            {
                case GameEnum.TurnState.SelectManeuver:
                    sheet.ResetForNewTurn();
                    ClearTemporaryDamage();
                    NotifySheetChanged();
                    break;
                case GameEnum.TurnState.RevealAndMoveFighters:
                    Debug.Log("[TurnManager] RevealAndMoveFighters placeholder.");
                    break;
                case GameEnum.TurnState.ResolveCollisions:
                    Debug.Log("[TurnManager] ResolveCollisions placeholder.");
                    break;
                case GameEnum.TurnState.SpawnMissilesAndResolveEvasion:
                    Debug.Log("[TurnManager] SpawnAndResolveAttacks placeholder.");
                    break;
                case GameEnum.TurnState.ApplyDamageAndCheckVictory:
                    Debug.Log("[TurnManager] ApplyDamageAndCheckVictory placeholder.");
                    break;
                case GameEnum.TurnState.EndRoundAndAdvance:
                    if (endRoundDelaySeconds <= 0f)
                    {
                        NextState();
                    }
                    else
                    {
                        Debug.Log($"[TurnManager] Ending round. Wait {endRoundDelaySeconds}s...");
                        StartCoroutine(WaitAndAdvanceRoutine());
                    }
                    break;
                case GameEnum.TurnState.MatchEnded:
                    Debug.Log("[TurnManager] Match ended.");
                    break;
            }

            OnStateChanged?.Invoke();
        }

        // Clears temporary damage records from all aircraft units at the end of a turn
        private void ClearTemporaryDamage()
        {
            var units = FindObjectsByType<AircraftUnit>(FindObjectsSortMode.None);
            int cleared = 0;
            for (int i = 0; i < units.Length; i++)
            {
                var u = units[i];
                if (u == null || u.roundDamageReceived == null) continue;
                if (u.roundDamageReceived.Count > 0)
                {
                    u.roundDamageReceived.Clear();
                    cleared++;
                }
            }
            Debug.Log($"[TEMP CLEAR] Cleared temporary hit points for {cleared} aircraft.");
        }

        // Sets the match as ended with the specified winning team
        public void SetMatchEnded(int winnerTeamId)
        {
            matchEnded = true;
            winnerTeam = winnerTeamId;
            if (sheet != null)
                sheet.phase = GameEnum.TurnState.MatchEnded;
            EnterState(GameEnum.TurnState.MatchEnded);
        }

        // Waits for a delay and then advances to the next state
        private System.Collections.IEnumerator WaitAndAdvanceRoutine()
        {
            yield return new WaitForSeconds(endRoundDelaySeconds);
            NextState();
        }

        // Checks if any alive player has selected a missile weapon
        private bool HasAnyActiveMissile()
        {
            if (sheet == null || sheet.rows == null) return false;
            for (int i = 0; i < sheet.rows.Count; i++)
            {
                var r = sheet.rows[i];
                if (r == null || !r.isAlive) continue;
                
                // Check sanitized weapon code
                string w = (r.weaponCode ?? "").Trim().ToUpperInvariant();
                if (w == "M") return true;
            }
            return false;
        }
    }
}
