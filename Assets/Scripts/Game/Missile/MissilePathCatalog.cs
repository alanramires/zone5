using System;
using System.Collections.Generic;

namespace Zone5
{
    public static class MissilePathCatalog
    {
        public const string DefaultId = "M10F";

        // Fonte da verdade: FORMAS (normalizadas).
        // O alcance real vem do MissileProfile.rangeFU.
        public static readonly List<MissilePathDef> All = new()
        {
            new MissilePathDef {
                id="M10F", name="Straight", mode=MissilePathMode.PointList,
                // reta no centro
                pointsNorm=new() { new(0f,0f), new(1f,0f) },
                endHeadingSameAsStart=true,
                aliases=new[]{ "M10F","10F","F","STRAIGHT","RETA","MISSILE","MISSIL" }
            },

            // Machine Gun (no seu design é “reta também”, quem limita é o profile (rangeFU=5) e/ou scale do token)
            new MissilePathDef {
                id="M10MG", name="Machine Gun (Straight)", mode=MissilePathMode.PointList,
                pointsNorm=new() { new(0f,0f), new(1f,0f) },
                endHeadingSameAsStart=true,
                aliases=new[]{ "M10MG","MG","GUN","MACHINEGUN","METRALHADORA" }
            },

            // Curvas (terminam retas, só deslocam lateralmente o endpoint)
            // Lead-in: anda reto até x=0.15 antes de começar a corrigir lateralmente.

            new MissilePathDef {
                id="M10L1", name="Curve Left (Light)", mode=MissilePathMode.PointList,
                pointsNorm=new()
                {
                    new(0f,0f),
                    new(0.18f, 0f),      // lead-in
                    new(0.35f, +0.22f),
                    new(0.70f, +0.22f),
                    new(1f, +0.22f),
                },
                endHeadingSameAsStart=true,
                aliases=new[]{ "M10L1","10L1","L1","LEFT1","ESQ1","LEVEESQ","LEVE L" }
            },
            new MissilePathDef {
                id="M10L2", name="Curve Left (Strong)", mode=MissilePathMode.PointList,
                pointsNorm=new()
                {
                    new(0f,0f),
                    new(0.18f, 0f),      // lead-in
                    new(0.35f, +0.38f),
                    new(0.70f, +0.38f),
                    new(1f, +0.38f),
                },
                endHeadingSameAsStart=true,
                aliases=new[]{ "M10L2","10L2","L2","LEFT2","ESQ2","FORTEESQ","FORTE L" }
            },
            new MissilePathDef {
                id="M10R1", name="Curve Right (Light)", mode=MissilePathMode.PointList,
                pointsNorm=new()
                {
                    new(0f,0f),
                    new(0.18f, 0f),      // lead-in
                    new(0.35f, -0.22f),
                    new(0.70f, -0.22f),
                    new(1f, -0.22f),
                },
                endHeadingSameAsStart=true,
                aliases=new[]{ "M10R1","10R1","R1","RIGHT1","DIR1","LEVEDIR","LEVE R" }
            },
            new MissilePathDef {
                id="M10R2", name="Curve Right (Strong)", mode=MissilePathMode.PointList,
                pointsNorm=new()
                {
                    new(0f,0f),
                    new(0.18f, 0f),      // lead-in
                    new(0.35f, -0.38f),
                    new(0.70f, -0.38f),
                    new(1f, -0.38f),
                },
                endHeadingSameAsStart=true,
                aliases=new[]{ "M10R2","10R2","R2","RIGHT2","DIR2","FORTEDIR","FORTE R" }
            },

            // ZigZag / S (termina reto, com deslocamento final diferente)
            // Lead-in igual: anda reto até x=0.15, depois começa o S.

            new MissilePathDef {
                id="M10S1", name="ZigZag (Light)", mode=MissilePathMode.PointList,
                pointsNorm=new()
                {
                    new(0f, 0f),
                    new(0.15f, 0f),      // lead-in
                    new(0.35f, +0.18f),
                    new(0.60f, -0.10f),
                    new(0.80f, -0.10f),
                    new(1f, -0.10f),
                },
                endHeadingSameAsStart=true,
                aliases=new[]{ "M10S1","10S1","S1","ZIG1","ZIGZAG1","S-LEVE","ZIG-LEVE" }
            },
            new MissilePathDef {
                id="M10S2", name="ZigZag (Strong)", mode=MissilePathMode.PointList,
                pointsNorm=new()
                {
                    new(0f, 0f),
                    new(0.15f, 0f),      // lead-in
                    new(0.35f, +0.32f),
                    new(0.60f, -0.20f),
                    new(0.80f, -0.20f),
                    new(1f, -0.20f),
                },
                endHeadingSameAsStart=true,
                aliases=new[]{ "M10S2","10S2","S2","ZIG2","ZIGZAG2","S-FORTE","ZIG-FORTE" }
            },

        };

        private static Dictionary<string, MissilePathDef> _byAlias;
        private static Dictionary<string, MissilePathDef> ByAlias => _byAlias ??= BuildIndex();

        private static Dictionary<string, MissilePathDef> BuildIndex()
        {
            var dict = new Dictionary<string, MissilePathDef>();
            foreach (var p in All)
            {
                Add(dict, p.id, p);
                if (p.aliases != null)
                    foreach (var a in p.aliases) Add(dict, a, p);
            }
            return dict;
        }

        private static void Add(Dictionary<string, MissilePathDef> dict, string key, MissilePathDef def)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            dict[key.Trim().ToUpperInvariant()] = def;
        }

        /// Resolve texto (ex: "M10L1", "L1", "zig2") -> MissilePathDef
        public static MissilePathDef Resolve(string raw)
        {
            string s = (raw ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(s)) return GetDefault();

            // Se vier com "+", pega só a primeira (MVP)
            int plus = s.IndexOf('+');
            if (plus >= 0) s = s.Substring(0, plus).Trim();

            if (ByAlias.TryGetValue(s, out var def)) return def;
            return GetDefault();
        }

        private static MissilePathDef GetDefault()
        {
            if (ByAlias.TryGetValue(DefaultId, out var def)) return def;
            return All.Count > 0 ? All[0] : null;
        }
    }
}
