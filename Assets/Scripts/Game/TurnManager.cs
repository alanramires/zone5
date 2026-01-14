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
        public float endRoundDelaySeconds = 5.0f;

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

        private void RegisterFromScene()
        {
            var units = FindObjectsByType<AircraftUnit>(FindObjectsSortMode.None);
            for (int i = 0; i < units.Length; i++)
            {
                var unit = units[i];
                if (unit == null || unit.playerId <= 0) continue;
                RegisterUnit(unit.playerId, unit.currentHp > 0);
            }
        }

        public TurnSheet.PlayerRow GetOrCreatePlayerRow(int playerId)
        {
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

        public void RegisterUnit(int playerId, bool isAlive = true)
        {
            var row = GetOrCreatePlayerRow(playerId);
            if (row == null) return;
            row.isAlive = isAlive;
            NotifySheetChanged();
        }

        public void SetAlive(int playerId, bool isAlive)
        {
            var row = GetOrCreatePlayerRow(playerId);
            if (row == null) return;
            row.isAlive = isAlive;
            NotifySheetChanged();
        }

        public void NotifySheetChanged()
        {
            OnSheetChanged?.Invoke();
        }

        public void AdvanceButton()
        {
            if (sheet == null) return;
            sheet.ApplyDefaultsForPhase(sheet.phase);
            NotifySheetChanged();
            TryAdvance();
        }

        public void TryAdvance()
        {
            if (sheet == null) return;
            if (sheet.AllReadyForPhase(sheet.phase))
                NextState();
        }

        private void NextState()
        {
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

        private void EnterState(GameEnum.TurnState state)
        {
            if (logStateChanges)
                Debug.Log($"[TurnManager] Turn={sheet.turnIndex} State={state}");

            switch (state)
            {
                case GameEnum.TurnState.SelectManeuver:
                    sheet.ResetForNewTurn();
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
                    Debug.Log($"[TurnManager] Ending round. Wait {endRoundDelaySeconds}s...");
                    StartCoroutine(WaitAndAdvanceRoutine());
                    break;
            }

            OnStateChanged?.Invoke();
        }

        private System.Collections.IEnumerator WaitAndAdvanceRoutine()
        {
            yield return new WaitForSeconds(endRoundDelaySeconds);
            NextState();
        }
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
