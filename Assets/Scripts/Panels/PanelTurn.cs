using TMPro;
using UnityEngine;
using Zone5;

public class PanelTurn : MonoBehaviour
{
    [SerializeField] private TMP_Text txtPhase;
    [SerializeField] private TurnManager turnManager;

    private void Awake()
    {
        if (turnManager == null)
            turnManager = FindFirstObjectByType<TurnManager>();
    }

    private void OnEnable()
    {
        if (turnManager != null)
        {
            turnManager.OnStateChanged += Refresh;
            turnManager.OnSheetChanged += Refresh;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (turnManager != null)
        {
            turnManager.OnStateChanged -= Refresh;
            turnManager.OnSheetChanged -= Refresh;
        }
    }

    private void Refresh()
    {
        if (txtPhase == null) return;

        int turn = 0;
        string phaseLabel = "Unknown";

        if (turnManager != null && turnManager.sheet != null)
        {
            turn = turnManager.sheet.turnIndex;
            phaseLabel = GetPhaseLabel(turnManager.sheet.phase, turnManager.matchEnded, turnManager.winnerTeam);
        }

        txtPhase.text = $"Fase: {phaseLabel} (Turno: {turn})";
    }

    private static string GetPhaseLabel(GameEnum.TurnState state, bool matchEnded, int winnerTeam)
    {
        if (matchEnded || state == GameEnum.TurnState.MatchEnded)
        {
            return winnerTeam >= 0 ? $"Team {winnerTeam} wins" : "Draw";
        }

        return state switch
        {
            GameEnum.TurnState.SelectManeuver => "Selecionar Manobra",
            GameEnum.TurnState.WaitManeuverConfirm => "Aguardando Manobra",
            GameEnum.TurnState.DeclareWeapon => "Declarar Arma",
            GameEnum.TurnState.WaitWeaponDeclare => "Aguardando Arma",
            GameEnum.TurnState.RevealAndMoveFighters => "Revelar e Mover",
            GameEnum.TurnState.ResolveCollisions => "Resolver Colisoes",
            GameEnum.TurnState.SelectMissileProfile => "Selecionar Missil",
            GameEnum.TurnState.WaitMissileSelection => "Aguardando Missil",
            GameEnum.TurnState.SpawnMissilesAndResolveEvasion => "Resolver Ataques",
            GameEnum.TurnState.ApplyDamageAndCheckVictory => "Aplicar Dano",
            GameEnum.TurnState.EndRoundAndAdvance => "Fim da Rodada",
            _ => state.ToString()
        };
    }
}
