using System;
using System.Collections.Generic;
using System.Linq;

namespace Zone5
{
    public static class ManeuverCatalog
    {
        public const string DefaultId = "1G18F";

        // Lista “fonte da verdade”
        public static readonly List<ManeuverDef> All = new()
        {
            new ManeuverDef {
                id="0G06F", name="Air Brake", kind=ManeuverKind.Acrobatic,
                allowedDirs=new[]{"F"},
                gForce=0, mach=0.6f, fuel=1, evasionPenalty=3,
                distanceFU=1f, pointsLocalFU=new(),
                aliases=new[]{"0G","0G06","0G06F","AIR BRAKE","AIRBRAKE"}
            },
            new ManeuverDef {
                id="1G09F", name="Padrão", kind=ManeuverKind.Move,
                allowedDirs=new[]{"F"},
                gForce=1, mach=0.9f, fuel=2, evasionPenalty=3,
                distanceFU=1.5f, pointsLocalFU=new(),
                aliases=new[]{"1G","1G09","1G09F"}
            },
            new ManeuverDef {
                id="1G12F", name="Padrão", kind=ManeuverKind.Move,
                allowedDirs=new[]{"F"},
                gForce=1, mach=1.2f, fuel=3, evasionPenalty=3,
                distanceFU=2f, pointsLocalFU=new(),
                aliases=new[]{"1G12","1G12F"}
            },
            new ManeuverDef {
                id="1G18F", name="Padrão", kind=ManeuverKind.Move,
                allowedDirs=new[]{"F"},
                gForce=1, mach=1.8f, fuel=4, evasionPenalty=2,
                distanceFU=3f, pointsLocalFU=new(),
                aliases=new[]{"1G18","1G18F"}
            },
            // Curvas fechadas em alta-G (7G)
            new ManeuverDef {
                id="7G06", name="High-G Turn", kind=ManeuverKind.Move,
                allowedDirs=new[]{"D","E"}, gForce=7, mach=0.6f, fuel=3, evasionPenalty=2,
                distanceFU=1.5f, pointsLocalFU=new(),
                pathMode=PathMode.BezierQuad, turnAngleDeg=90f, curveBias=0.25f,
                leadInFrac=0.15f, handleFrac=0.35f, lateralFrac=0.65f,
                aliases=new[]{"7G06","7G06D","7G06E"}
            },
            new ManeuverDef {
                id="7G09", name="High-G Turn", kind=ManeuverKind.Move,
                allowedDirs=new[]{"D","E"}, gForce=7, mach=0.9f, fuel=4, evasionPenalty=2,
                distanceFU=1.5f, pointsLocalFU=new(),
                pathMode=PathMode.BezierQuad, turnAngleDeg=60f, curveBias=0.25f,
                leadInFrac=0.65f, handleFrac=0.10f, lateralFrac=0.90f,
                aliases=new[]{"7G09","7G09D","7G09E"}
            },
            new ManeuverDef {
                id="7G12", name="High-G Turn", kind=ManeuverKind.Move,
                allowedDirs=new[]{"D","E"}, gForce=7, mach=1.2f, fuel=5, evasionPenalty=2,
                distanceFU=2f, pointsLocalFU=new(),
                pathMode=PathMode.BezierQuad, turnAngleDeg=45f, curveBias=0.25f,
                leadInFrac=0.25f, handleFrac=0.30f, lateralFrac=0.55f,
                aliases=new[]{"7G12","7G12D","7G12E"}
            },
            new ManeuverDef {
                id="7G18", name="High-G Turn", kind=ManeuverKind.Move,
                allowedDirs=new[]{"D","E"}, gForce=7, mach=1.8f, fuel=6, evasionPenalty=2,
                distanceFU=3f, pointsLocalFU=new(),
                pathMode=PathMode.BezierQuad, turnAngleDeg=30f, curveBias=0.25f,
                leadInFrac=0.25f, handleFrac=0.30f, lateralFrac=0.55f,
                aliases=new[]{"7G18","7G18D","7G18E","7GD","7GE"}
            },

            // Curvas suaves em baixa-G (3G)
            new ManeuverDef {
                id="3G06", name="Adjust Turn", kind=ManeuverKind.Move,
                allowedDirs=new[]{"D","E"}, gForce=3, mach=0.6f, fuel=2, evasionPenalty=3,
                distanceFU=1f, pointsLocalFU=new(),
                pathMode=PathMode.BezierQuad, turnAngleDeg=45f, curveBias=0.55f,
                leadInFrac=0.10f, handleFrac=0.45f, lateralFrac=0.35f,
                aliases=new[]{"3G06","3G06D","3G06E"}
            },
            new ManeuverDef {
                id="3G09", name="Adjust Turn", kind=ManeuverKind.Move,
                allowedDirs=new[]{"D","E"}, gForce=3, mach=0.9f, fuel=3, evasionPenalty=3,
                distanceFU=1.5f, pointsLocalFU=new(),
                pathMode=PathMode.BezierQuad, turnAngleDeg=30f, curveBias=0.55f,
                leadInFrac=0.10f, handleFrac=0.45f, lateralFrac=0.35f,
                aliases=new[]{"3G09","3G09D","3G09E"}
            },
            new ManeuverDef {
                id="3G12", name="Adjust Turn", kind=ManeuverKind.Move,
                allowedDirs=new[]{"D","E"}, gForce=3, mach=1.2f, fuel=4, evasionPenalty=3,
                distanceFU=2f, pointsLocalFU=new(),
                pathMode=PathMode.BezierQuad, turnAngleDeg=22.5f, curveBias=0.55f,
                leadInFrac=0.10f, handleFrac=0.45f, lateralFrac=0.35f,
                aliases=new[]{"3G12","3G12D","3G12E"}
            },
            new ManeuverDef {
                id="3G18", name="Adjust Turn", kind=ManeuverKind.Move,
                allowedDirs=new[]{"D","E"}, gForce=3, mach=1.8f, fuel=5, evasionPenalty=3,
                distanceFU=3f, pointsLocalFU=new(),
                pathMode=PathMode.BezierQuad, turnAngleDeg=15f, curveBias=0.55f,
                leadInFrac=0.10f, handleFrac=0.45f, lateralFrac=0.35f,
                aliases=new[]{"3G18","3G18D","3G18E","3GD","3GE"}
            },

        };

        // Índice pra lookup rápido (alias -> def)
        private static Dictionary<string, ManeuverDef> _byAlias;
        private static Dictionary<string, ManeuverDef> ByAlias => _byAlias ??= BuildIndex();

        private static Dictionary<string, ManeuverDef> BuildIndex()
        {
            var dict = new Dictionary<string, ManeuverDef>();
            foreach (var m in All)
            {
                // id também vale como alias
                Add(dict, m.id, m);

                if (m.aliases != null)
                    foreach (var a in m.aliases) Add(dict, a, m);
            }
            return dict;
        }

        private static void Add(Dictionary<string, ManeuverDef> dict, string key, ManeuverDef m)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            dict[key.Trim().ToUpperInvariant()] = m;
        }

        /// Resolve uma string (“1G18”, “air brake”, “0G”, “1G18F”) -> ManeuverDef
        public static ManeuverDef Resolve(string raw)
        {
            string s = (raw ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(s)) return Resolve(DefaultId);

            // Se vier com "+", pega só a primeira (MVP)
            int plus = s.IndexOf('+');
            if (plus >= 0) s = s.Substring(0, plus).Trim();

            // Se não tem direção no final e o usuário digitou algo tipo "1G18", tenta bater assim.
            // (No futuro, se você quiser, dá pra normalizar adicionando F/D/E conforme UI)
            if (ByAlias.TryGetValue(s, out var m)) return m;

            // fallback
            return Resolve(DefaultId);
        }

        /// Pós-MVP: parse “1G18+1G18” -> lista
        public static List<ManeuverDef> ParseCombo(string raw)
        {
            string s = (raw ?? "").Trim();
            if (string.IsNullOrEmpty(s)) return new List<ManeuverDef> { Resolve(DefaultId) };

            return s.Split('+')
                    .Select(t => Resolve(t))
                    .ToList();
        }
    }
}
