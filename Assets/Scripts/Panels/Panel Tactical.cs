using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zone5;

public class Panel_tactical : MonoBehaviour
{
    [Header("Players List")]
    [SerializeField] private Transform playersListRoot;   // Panel_Tactical/PlayersBox/PlayersList
    [SerializeField] private PilotRowView pilotRowPrefab; // Prefab do item repetivel (PilotRow)

    // [Header("Button")] - Removed as requested
    // [SerializeField] private Button btnAvancar;
    // [SerializeField] private TMP_Text txtBtnAvancar;

    private readonly List<PilotRowView> rows = new();
    private TurnManager turnManager;
    private TurnStateManager turnStateManager;

    // Evento Removed
    // public event Action OnAdvance;

    private void Awake()
    {
        // Button listeners removed
    }

    private void OnEnable()
    {
        BindTurnManager();
    }

    private void OnDisable()
    {
        UnbindTurnManager();
    }

    private void Start()
    {
        RefreshFromTurnManager();
    }

    // ===== Public API =====

    // Advance removed

    // ===== Internals =====

    private void BindTurnManager()
    {
        UnbindTurnManager();
        turnManager = FindFirstObjectByType<TurnManager>();
        turnStateManager = FindFirstObjectByType<TurnStateManager>();
        
        if (turnManager != null)
        {
            turnManager.OnStateChanged += HandleTurnStateChanged;
            turnManager.OnSheetChanged += HandleSheetChanged;
            // OnAdvance += turnManager.AdvanceButton; // Removed
        }

        if (turnStateManager != null)
            turnStateManager.OnStateChanged += HandleGlobalTurnStateChanged;

        RefreshFromTurnManager();
    }

    private void UnbindTurnManager()
    {
        if (turnManager != null)
        {
            turnManager.OnStateChanged -= HandleTurnStateChanged;
            turnManager.OnSheetChanged -= HandleSheetChanged;
            // OnAdvance -= turnManager.AdvanceButton;
        }
        if (turnStateManager != null)
            turnStateManager.OnStateChanged -= HandleGlobalTurnStateChanged;
    }

    private void HandleTurnStateChanged()
    {
        RefreshFromTurnManager();
    }

    private void HandleSheetChanged()
    {
        RefreshFromTurnManager();
    }

    private void HandleGlobalTurnStateChanged(GameEnum.TurnState state)
    {
        RefreshFromTurnManager();
    }

    private void RefreshFromTurnManager()
    {
        if (turnManager == null || turnManager.sheet == null) return;
        
        GameEnum.TurnState state = turnManager.sheet.phase;

        // 1. Collect all units to map PlayerID -> Unit Data
        var units = FindObjectsByType<AircraftUnit>(FindObjectsSortMode.None);
        var unitMap = new Dictionary<int, AircraftUnit>();
        foreach (var u in units)
        {
            if (u != null) unitMap[u.playerId] = u;
        }

        // 2. Get rows from TurnManager (which tracks who is in the game)
        var sheetRows = turnManager.sheet.rows ?? new List<TurnSheet.PlayerRow>();
        
        // 3. Clear UI
        ClearRows();

        // 4. Rebuild UI
        // Sort by PlayerID for consistency
        var sortedRows = new List<TurnSheet.PlayerRow>(sheetRows);
        sortedRows.Sort((a,b) => a.playerId.CompareTo(b.playerId));

        foreach (var pRow in sortedRows)
        {
            if (pRow == null) continue;
            if (pRow.playerId <= 0) continue;

            // Get unit info - Try Map first (live), then fallback to Persistent
            string callsign = "Unknown";
            string aircraftName = "Unknown";
            string unitProfileId = null;
            Sprite aircraftSprite = null;
            
            if (unitMap.TryGetValue(pRow.playerId, out var unit))
            {
                callsign = !string.IsNullOrEmpty(unit.callSign) ? unit.callSign : unit.unitId;
                aircraftName = unit.UnitData != null ? unit.UnitData.unitId : "Fighter";
                unitProfileId = aircraftName;
                aircraftSprite = unit.UnitData != null ? unit.UnitData.spriteDefault : null;
                
                // Update persistent data just in case
                pRow.callSign = callsign;
                pRow.aircraftName = aircraftName;
                pRow.unitId = unitProfileId;
                pRow.aircraftSprite = aircraftSprite;
            }
            else
            {
                // Unit destroyed? Use persistent data
                callsign = !string.IsNullOrEmpty(pRow.callSign) ? pRow.callSign : $"P{pRow.playerId}";
                aircraftName = !string.IsNullOrEmpty(pRow.aircraftName) ? pRow.aircraftName : "Fighter";
                unitProfileId = !string.IsNullOrEmpty(pRow.unitId) ? pRow.unitId : aircraftName;
                aircraftSprite = pRow.aircraftSprite;
            }

            string status = GetPilotStatus(pRow, state);
            
            // Create Row
            if (pilotRowPrefab != null)
            {
                var ui = Instantiate(pilotRowPrefab, playersListRoot);
                ui.Setup(pRow.playerId.ToString(), callsign, unitProfileId, status, aircraftSprite);
                
                Color teamColor = GameEnum.GameColors.GetColorForTeam(unit != null ? unit.teamId : 0);
                ui.SetColors(teamColor, !pRow.isAlive);

                rows.Add(ui);
            }
        }
    }

    private void ClearRows()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i] != null) Destroy(rows[i].gameObject);
        }
        rows.Clear();
    }

    private static string GetPilotStatus(TurnSheet.PlayerRow row, GameEnum.TurnState state)
    {
        if (row == null) return "Aguardando";
        if (!row.isAlive) return "Eliminado";

        switch (state)
        {
            case GameEnum.TurnState.SelectManeuver:
            case GameEnum.TurnState.WaitManeuverConfirm:
                return row.maneuverReady ? "Pronto" : "Aguardando";
            
            case GameEnum.TurnState.DeclareWeapon:
            case GameEnum.TurnState.WaitWeaponDeclare:
                return row.weaponReady ? "Pronto" : "Aguardando";

            case GameEnum.TurnState.SelectMissileProfile:
            case GameEnum.TurnState.WaitMissileSelection:
                // Only relevant if they declared a missile
                if (MvpRules.SanitizeWeapon(row.weaponCode) == "M")
                    return row.missileReady ? "Pronto" : "Aguardando";
                else
                    return "Aguardando";

            default:
                return "Travado";
        }
    }

    private static string GetPhaseLabel(GameEnum.TurnState state)
    {
        return state switch
        {
            GameEnum.TurnState.SelectManeuver => "Seleção de Manobra",
            GameEnum.TurnState.WaitManeuverConfirm => "Aguardando Confirmação",
            GameEnum.TurnState.DeclareWeapon => "Declaração de Arma",
            GameEnum.TurnState.WaitWeaponDeclare => "Aguardando Arma",
            GameEnum.TurnState.RevealAndMoveFighters => "Movimento",
            GameEnum.TurnState.ResolveCollisions => "Colisões",
            GameEnum.TurnState.SelectMissileProfile => "Seleção de Míssil",
            GameEnum.TurnState.WaitMissileSelection => "Aguardando Míssil",
            GameEnum.TurnState.SpawnMissilesAndResolveEvasion => "Resolução de Ataques",
            GameEnum.TurnState.ApplyDamageAndCheckVictory => "Danos e Vitória",
            GameEnum.TurnState.EndRoundAndAdvance => "Firing...",
            GameEnum.TurnState.MatchEnded => "Match ended",
            _ => state.ToString(),
        };
    }
}
