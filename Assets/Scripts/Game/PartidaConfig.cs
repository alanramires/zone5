using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zone5
{
    /// <summary>
    /// Configuração da partida transportada entre cenas (Tela de Entrada → Batalha).
    /// Tela de Entrada: monta os slots e chama Set() antes do LoadScene.
    /// Batalha: UnitSpawner.Spawn() chama TryConsume(); sem config pendente, usa os times da cena.
    /// </summary>
    public static class PartidaConfig
    {
        public const int SlotsPerTeam = 4;
        public const int SlotCount = SlotsPerTeam * 2;

        // Times fechados no MVP
        public const int TeamUsa = 0;  // F-14, lado esquerdo
        public const int TeamUssr = 1; // MiG-29, lado direito

        public enum SlotState
        {
            Empty = 0,
            Occupied = 1,
            Ready = 2, // reservado para multiplayer; em hotseat Occupied já basta
        }

        [Serializable]
        public class Slot
        {
            public int slotId;
            public SlotState state = SlotState.Empty;
            public string callsign = "";

            public int TeamId => TeamOf(slotId);
            public int PlayerId => PlayerIdOf(slotId);
            public bool IsOccupied => state != SlotState.Empty;

            public Slot Clone() => new Slot { slotId = slotId, state = state, callsign = callsign };
        }

        // Slots 0-3 = EUA, 4-7 = URSS. O time sai do slot, nunca é guardado.
        public static int TeamOf(int slotId) => slotId / SlotsPerTeam;

        // playerId estável por slot: o slot 5 é sempre o P6, mesmo com slots vazios no meio.
        public static int PlayerIdOf(int slotId) => slotId + 1;

        private static readonly string[] DefaultCallsignsUsa = { "Maverick", "Iceman", "Goose", "Jester" };
        private static readonly string[] DefaultCallsignsUssr = { "Uri", "Boris", "Dimitri", "Ivan" };

        public static string DefaultCallsign(int slotId)
        {
            if (slotId < 0 || slotId >= SlotCount) return $"Pilot {PlayerIdOf(slotId)}";
            string[] pool = TeamOf(slotId) == TeamUsa ? DefaultCallsignsUsa : DefaultCallsignsUssr;
            return pool[slotId % SlotsPerTeam];
        }

        public static bool HasPending { get; private set; }
        private static Slot[] pendingSlots;

        // Estático sobrevive entre Plays se o domain reload estiver desligado.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Clear();

        public static Slot[] CreateEmptySlots()
        {
            var slots = new Slot[SlotCount];
            for (int i = 0; i < SlotCount; i++)
                slots[i] = new Slot { slotId = i, callsign = DefaultCallsign(i) };
            return slots;
        }

        public static bool Validate(Slot[] slots, out string error)
        {
            error = null;
            if (slots == null || slots.Length != SlotCount)
            {
                error = $"Esperado {SlotCount} slots.";
                return false;
            }

            int usa = 0, ussr = 0;
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                if (s == null || s.slotId != i)
                {
                    error = $"Slot {i} inválido.";
                    return false;
                }
                if (!s.IsOccupied) continue;

                string cs = (s.callsign ?? "").Trim();
                if (cs.Length == 0)
                {
                    error = $"Slot {i} sem callsign.";
                    return false;
                }
                if (!used.Add(cs))
                {
                    error = $"Callsign repetido: {cs}.";
                    return false;
                }

                if (s.TeamId == TeamUsa) usa++;
                else ussr++;
            }

            if (usa == 0 || ussr == 0)
            {
                error = "Precisa de pelo menos 1 jogador de cada lado.";
                return false;
            }
            return true;
        }

        public static bool Set(Slot[] slots, out string error)
        {
            if (!Validate(slots, out error)) return false;

            pendingSlots = new Slot[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                pendingSlots[i] = slots[i].Clone();
                pendingSlots[i].callsign = (pendingSlots[i].callsign ?? "").Trim();
            }
            HasPending = true;
            return true;
        }

        public static bool TryConsume(out Slot[] slots)
        {
            slots = pendingSlots;
            bool had = HasPending;
            Clear();
            return had && slots != null;
        }

        public static void Clear()
        {
            pendingSlots = null;
            HasPending = false;
        }
    }
}
