using UnityEngine;

namespace Zone5
{
    /// <summary>
    /// Debug: simula a Tela de Entrada preenchendo o PartidaConfig antes do UnitSpawner.Start.
    /// Ignorado se já houver config pendente (vinda da Tela de Entrada de verdade).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class PartidaConfigDebugSeed : MonoBehaviour
    {
        [Tooltip("Slots 0-3 = EUA, 4-7 = URSS. Marque quem está ocupado.")]
        [SerializeField] private bool[] occupied = { true, true, false, false, true, false, false, false };

        private void Awake()
        {
            if (!enabled || PartidaConfig.HasPending) return;

            var slots = PartidaConfig.CreateEmptySlots();
            for (int i = 0; i < slots.Length && i < occupied.Length; i++)
                if (occupied[i]) slots[i].state = PartidaConfig.SlotState.Occupied;

            if (!PartidaConfig.Set(slots, out string error))
                Debug.LogWarning($"[PartidaConfigDebugSeed] Config inválida: {error}");
        }
    }
}
