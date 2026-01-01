using UnityEngine;

namespace Zone5
{
    /// <summary>
    /// Enumerações e constantes globais do jogo.
    /// </summary>
    public static class GameEnum
    {
        // 11 estados do turno (MVP + base pro avançado)
        public enum TurnState
        {
            // 1) Jogadores escolhem 1 carta/manobra (ou 2 no avançado)
            SelectManeuver = 0,

            // 2) Server/host espera todos confirmarem a manobra
            WaitManeuverConfirm = 1,

            // 3) Jogadores declaram arma (None/Missile; avançado: Gun)
            DeclareWeapon = 2,

            // 4) Espera todos declararem a arma
            WaitWeaponDeclare = 3,

            // 5) Revela manobras e move caças (trajetória conecta na anterior)
            RevealAndMoveFighters = 4,

            // 6) Checa colisões simultâneas (se houver no MVP/mais tarde)
            ResolveCollisions = 5,

            // 7) Quem declarou míssil escolhe o perfil/skin do míssil
            SelectMissileProfile = 6,

            // 8) Espera todos que vão disparar escolherem o míssil
            WaitMissileSelection = 7,

            // 9) Spawna todos os mísseis juntos e resolve quase-hit + dodge
            SpawnMissilesAndResolveEvasion = 8,

            // 10) Aplica dano/mortes/assistências e checa vitória
            ApplyDamageAndCheckVictory = 9,

            // 11) Limpa a rodada e avança para a próxima
            EndRoundAndAdvance = 10,
        }

        // Cores de time (por enquanto 4)
        public static class GameColors
        {
            public static readonly Color TeamBlue   = new Color(168f / 255f, 168f / 255f, 255f / 255f);
            public static readonly Color TeamRed    = new Color(255f / 255f, 155f / 255f, 155f / 255f);
            public static readonly Color TeamGreen  = new Color(144f / 255f, 238f / 255f, 144f / 255f);
            public static readonly Color TeamYellow = new Color(255f / 255f, 246f / 255f, 141f / 255f);
        }

        // Armas do MVP/Avançado
        public enum WeaponType
        {
            None = 0,
            Missile = 1,
            Gun = 2, // Vulcan (avançado ou debug)
        }
    }
}
