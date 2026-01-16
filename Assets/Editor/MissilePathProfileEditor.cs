using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zone5;

[CustomEditor(typeof(MissilePathProfile))]
public class MissilePathProfileEditor : Editor
{
    private MissilePathProfile _profile;
    private const float PREVIEW_SCALE = 10f; // “alcance fake” só pra visualizar

    private void OnEnable()
    {
        _profile = (MissilePathProfile)target;
        SceneView.duringSceneGui += OnSceneView;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneView;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Scene Preview:\n" +
            "- Origem em (-19,-10)\n" +
            "- Direcao inicial: para direita\n" +
            "- Path desenhado como disparo fantasma\n\n" +
            "Abra o Scene View para visualizar.",
            MessageType.Info
        );

        if (GUI.changed)
            SceneView.RepaintAll();
    }

    private void OnSceneView(SceneView sceneView)
    {
        var profile = _profile;
        if (profile == null) return;
        if (profile.pointsNorm == null || profile.pointsNorm.Count < 2)
            return;

        // Origem do disparo
        Vector3 origin = new Vector3(-12.28131f, 0.9596786f, 0f);

        // Direcao inicial (RIGHT = igual seu jogo)
        Vector3 forward = Vector3.right;
        Handles.color = profile.previewColor;

        var controlPts = BuildWorldPoints(profile.pointsNorm, origin, forward);
        if (controlPts.Length < 2) return;

        int samplesPerSegment = Mathf.Max(1, Mathf.RoundToInt(profile.previewSamples / Mathf.Max(1, controlPts.Length - 1)));
        var smoothPts = SmoothCatmullRom(controlPts, samplesPerSegment);

        for (int i = 1; i < smoothPts.Length; i++)
        {
            Handles.DrawAAPolyLine(16f, smoothPts[i - 1], smoothPts[i]);
        }
    }

    private Vector3[] BuildWorldPoints(List<Vector2> pointsNorm, Vector3 origin, Vector3 forward)
    {
        if (pointsNorm == null || pointsNorm.Count < 2)
            return Array.Empty<Vector3>();

        Vector3 right = new Vector3(-forward.y, forward.x, 0f).normalized;
        var pts = new Vector3[pointsNorm.Count];

        for (int i = 0; i < pointsNorm.Count; i++)
        {
            var p = pointsNorm[i];
            pts[i] = origin
                 + forward * (p.x * PREVIEW_SCALE)
                 + right   * (p.y * PREVIEW_SCALE);
        }

        return pts;
    }

    private static Vector3[] SmoothCatmullRom(Vector3[] controlPoints, int samplesPerSegment)
    {
        if (controlPoints == null || controlPoints.Length < 2)
            return controlPoints ?? Array.Empty<Vector3>();

        if (samplesPerSegment < 1) samplesPerSegment = 1;

        int n = controlPoints.Length;
        var result = new List<Vector3>(n * (samplesPerSegment + 1));
        result.Add(controlPoints[0]);

        for (int i = 0; i < n - 1; i++)
        {
            Vector3 p0 = controlPoints[Mathf.Max(i - 1, 0)];
            Vector3 p1 = controlPoints[i];
            Vector3 p2 = controlPoints[i + 1];
            Vector3 p3 = controlPoints[Mathf.Min(i + 2, n - 1)];

            for (int s = 1; s <= samplesPerSegment; s++)
            {
                float t = s / (float)samplesPerSegment;
                result.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }

        return result.ToArray();
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }
}


