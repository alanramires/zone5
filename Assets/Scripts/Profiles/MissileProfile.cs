// MissileProfile.cs
using UnityEngine;

namespace Zone5
{
    public enum MissileProfilePathMode
    {
        Straight = 0,
        ZigZag = 1,
        TurnD = 2, // clockwise (D)
        TurnE = 3  // counterclockwise (E)
    }

    [CreateAssetMenu(menuName = "Zone5/Missile Profile", fileName = "MissileProfile_")]
    public class MissileProfile : ScriptableObject
    {
        [Header("Identity (generic)")]
        public string missileName = "Long Range Missile";
        public string missileId = "LRM"; // útil pra salvar/rede depois
        
        [Header("Combat")]
        [Tooltip("Dano base causado por este míssil ao acertar. MVP: 3.")]
        [Min(0)] public int missileDamage = 3;

        [Header("Visual")]
        public Sprite spriteWorld; // tabuleiro
        public Sprite spriteHud;   // HUD/munição
        [Tooltip("Se true, tinge levemente o sprite do míssil com a cor do time (recomendado p/ legibilidade).")]
        public bool tintWithTeam = false;
        [Range(0f, 1f)] public float tintStrength = 0.20f;

        [Header("Movement")]
        [Min(0.1f)] public float rangeFU = 10f; // Phoenix/R-27 = 10 FU
        public MissileProfilePathMode pathMode = MissileProfilePathMode.Straight;

        [Header("Scale (optional)")]
        [Tooltip("Comprimento físico do token do míssil em FU (pra colisão/visual). Default = 0.5.")]
        [Min(0.05f)] public float tokenLengthFU = 0.5f;

        [Header("Team Aliases (optional)")]
        [Tooltip("Nome exibido se o míssil for do time 0 (ex.: AIM-54 Phoenix).")]
        public string aliasTeam0 = "AIM-54 Phoenix";
        [Tooltip("Nome exibido se o míssil for do time 1 (ex.: R-27).")]
        public string aliasTeam1 = "R-27";

        public string GetDisplayName(int teamId)
        {
            if (teamId == 0 && !string.IsNullOrWhiteSpace(aliasTeam0)) return aliasTeam0;
            if (teamId == 1 && !string.IsNullOrWhiteSpace(aliasTeam1)) return aliasTeam1;
            return missileName;
        }
    }
}
