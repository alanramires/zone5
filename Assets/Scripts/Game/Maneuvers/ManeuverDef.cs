using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zone5
{
    public enum ManeuverKind { Move, Acrobatic }

    // ✅ Tipo de caminho da manobra
    public enum PathMode { Straight, BezierQuad, PointList }

    [Serializable]
    public class ManeuverDef
    {
        public string id;                 // canônico: "1G18"
        public string name;               // "Padrão", "High-G Turn"
        public ManeuverKind kind;         // Move / Acrobatic
        public string[] allowedDirs;      // "F","D","E"

        public float gForce;              // 1, 3, 7...
        public float mach;                // 0.6, 1.8...
        public int fuel;                  // futuro
        public int evasionPenalty;        // maior = pior

        // Movimento base
        public float distanceFU;          // usado em reta e como "orçamento" da curva

        // Pós-MVP (path desenhado à mão)
        public List<Vector3> pointsLocalFU;

        // Curvas (usado quando pathMode == BezierQuad)
        public PathMode pathMode;         // default = Straight
        public float turnAngleDeg;        // ângulo final (sempre positivo no catálogo)
        public float curveBias;           // 0..1 (7G baixo / 3G alto)

        // Shape tuning (BezierQuad)
        public float leadInFrac;   // 0..1 quanto anda reto antes da curva (fração da distanceFU)
        public float handleFrac;   // 0..1 controla "onde" fica o controle (seca vs gorda)
        public float lateralFrac;  // 0..1 controla o "peso" lateral da curva


        // Aliases de entrada
        public string[] aliases;
    }
}
