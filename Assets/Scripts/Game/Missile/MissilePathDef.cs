using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zone5
{
    public enum MissilePathMode
    {
        Straight = 0,
        PointList = 1, // lista de pontos (xNorm,yNorm) em espaço local
    }

    [Serializable]
    public class MissilePathDef
    {
        [Header("Identity")]
        public string id;              // ex: M10F, M10L1...
        public string name;            // ex: Straight, Curve Left (Light)...
        public MissilePathMode mode = MissilePathMode.PointList;

        [Header("Shape (normalized)")]
        [Tooltip("Pontos em coordenadas normalizadas. xNorm vai de 0..1 (progresso no alcance). " +
                 "yNorm é deslocamento lateral relativo (fração do alcance). " +
                 "Ex.: yNorm=0.25 significa offset lateral de 25% do rangeFU.")]
        public List<Vector2> pointsNorm = new();

        [Header("Behavior")]
        [Tooltip("Se true, o heading final deve ser o mesmo do início (termina reto). " +
                 "Isso bate com as cartas originais de míssil (todas terminam estabilizadas).")]
        public bool endHeadingSameAsStart = true;

        [Header("Aliases")]
        public string[] aliases;

        public override string ToString() => $"{id} ({name})";
    }
}
