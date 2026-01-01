using UnityEngine;

namespace Zone5
{
    [CreateAssetMenu(menuName = "Zone5/Unit Profile", fileName = "UnitProfile_")]
    public class UnitProfile : ScriptableObject
    {
        [Header("Identity")]
        public string unitName = "F-14 Tomcat";
        public string unitId = "F14"; // útil pra rede/salvar depois

        [Header("Visual")]
        public Sprite spriteDefault;

        [Header("Stats (Advanced-ready)")]
        [Min(1)] public int maxHp = 3;
        [Min(0)] public int maxFuel = 56;

        [Header("Weapons")]
        [Min(0)] public int missilesMax = 6;
        public bool vulcanUnlimited = true;

        [Header("Scale (optional)")]
        [Tooltip("Comprimento do token do caça em Fighter Units (FU). Default = 1.")]
        public float tokenLengthFU = 1f;
    }
}
