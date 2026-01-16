using System.Collections.Generic;
using UnityEngine;

namespace Zone5
{
    public enum MissilePathProfileMode
    {
        Straight = 0,
        PointList = 1
    }

    [CreateAssetMenu(
        menuName = "Zone5/Missile Path Profile",
        fileName = "MissilePath_"
    )]
    public class MissilePathProfile : ScriptableObject
    {
        [Header("Identity")]
        public string pathId;          // ex: M10L1
        public string displayName;     // ex: Curve Left (Light)

        [Header("Aliases (Input)")]
        [Tooltip(
            "Textos alternativos que resolvem para este path.\n" +
            "Ex: L1, LEFT1, ESQ1, ZIG1, 6\n" +
            "Sempre salvos em uppercase pelo sistema."
        )]
        public List<string> aliases = new();

        [Header("Mode")]
        public MissilePathProfileMode mode = MissilePathProfileMode.PointList;

        [Header("Shape (Normalized)")]
        [Tooltip(
            "Pontos normalizados do path.\n" +
            "x = progresso (0..1)\n" +
            "y = deslocamento lateral relativo ao range\n\n" +
            "Ex: y=0.25 = 25% do alcance lateral"
        )]
        public List<Vector2> pointsNorm = new()
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f)
        };

        [Header("Behavior")]
        [Tooltip("Se true, o míssil termina com o mesmo heading do início.")]
        public bool endHeadingSameAsStart = true;

        [Header("Editor Preview")]
        [Tooltip("Cor do path no preview do Inspector.")]
        public Color previewColor = new Color(1f, 0f, 0.7764706f);

        [Range(16, 256)]
        [Tooltip("Quantas amostras usar no preview (mais = mais suave).")]
        public int previewSamples = 64;

        /// <summary>
        /// Retorna os pontos normalizados, garantindo ordem por X.
        /// </summary>
        public List<Vector2> GetSortedPoints()
        {
            pointsNorm.Sort((a, b) => a.x.CompareTo(b.x));
            return pointsNorm;
        }

        public IEnumerable<string> GetAllKeys()
        {
            yield return pathId;
            if (aliases == null) yield break;

            foreach (var a in aliases)
            {
                if (!string.IsNullOrWhiteSpace(a))
                    yield return a.Trim().ToUpperInvariant();
            }
        }
    }
}



