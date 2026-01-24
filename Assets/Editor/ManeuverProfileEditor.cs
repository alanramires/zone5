using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zone5;

[CustomEditor(typeof(ManeuverProfile))]
public class ManeuverProfileEditor : Editor
{
    private ManeuverProfile _profile;
    private readonly List<Vector3> _points = new();

    private void OnEnable()
    {
        _profile = (ManeuverProfile)target;
        SceneView.duringSceneGui += OnSceneView;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneView;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUI.BeginChangeCheck();

        DrawSection("Identity");
        DrawField("maneuverId");
        DrawField("displayName");
        DrawField("aliases", true);
        DrawField("allowedDirs");
        DrawField("defaultSprite");
        DrawField("afterburnerSprite");

        DrawSection("Classification");
        DrawField("kind");
        DrawField("mainDir");
        DrawField("usage");
        DrawField("usedInAfterBurner");

        DrawSection("Stats");
        DrawField("gForce");
        DrawField("machTier");
        DrawField("evasionPenalty");

        DrawSection("Movement");
        DrawField("pathMode");
        DrawField("distanceFU");
        DrawField("turnAngleDeg", false, "Turn Angle Deg");
        DrawField("curveBias", false, "Speed (bias)");

        DrawSection("Curve Edit");
        DrawField("straightLeadInFrac", false, "Straight In");
        DrawField("bezierForwardHandleFrac", false, "Beizer (y)");
        DrawField("bezierLateralFrac", false, "Beizer (x)");

        DrawSection("Custom Movement");
        DrawField("pointsNorm", true, "Point To Point");

        DrawSection("Move Options");
        DrawField("enforceStraightStartEnd", false, "Enforce Straight Start to End");
        DrawField("endHeadingSameAsStart", false, "Ending Heading Same as Start");
        DrawField("useEndHeadingOverride", false, "Use Custom Heading");
        DrawField("endHeadingOverrideDeg", false, "Custom Heading Angle");

        DrawSection("Preview");
        DrawField("previewColor");
        DrawField("previewSamples");

        DrawSection("VFX");
        DrawField("useVfx");
        DrawField("vfxMode");
        DrawField("vfxProgress", true);
        DrawField("vfxXY", true);

        DrawSection("VFX Options");
        DrawField("backfaceEnabled");
        DrawField("backfaceThresholdDeg");
        DrawField("backfaceLerp");
        DrawField("backfaceColor");
        DrawField("useSmooth");
        DrawField("trailDeformEnabled");
        DrawField("trailDeformMinScale");
        DrawField("trailDeformMaxRollDeg");

        serializedObject.ApplyModifiedProperties();

        if (EditorGUI.EndChangeCheck())
            SceneView.RepaintAll();
    }

    private void OnSceneView(SceneView sceneView)
    {
        var profile = _profile;
        if (profile == null) return;

        _points.Clear();

        Vector3 start = new Vector3(-5f, -5f, 0f);
        Vector3 forward = Vector3.up;
        float fuWorld = 1f;
        TurnDir dir = ResolveDir(profile.mainDir);

        profile.BuildWorldPoints(start, forward, fuWorld, dir, _points);
        if (_points.Count < 2) return;

        Handles.color = profile.previewColor;
        Handles.DrawAAPolyLine(18f, _points.ToArray());

        DrawDebugNodes(profile, start, forward, fuWorld, dir);
    }

    private static TurnDir ResolveDir(ManeuverMainDir mainDir)
    {
        return mainDir switch
        {
            ManeuverMainDir.D => TurnDir.D,
            ManeuverMainDir.E => TurnDir.E,
            ManeuverMainDir.F => TurnDir.F,
            _ => TurnDir.F
        };
    }

    private static void DrawSection(string label)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
    }

    private void DrawField(string propertyName, bool includeChildren = false, string labelOverride = null)
    {
        if (propertyName == "useLegacyArc")
            return;

        SerializedProperty prop = serializedObject.FindProperty(propertyName);
        if (prop == null) return;

        if (string.IsNullOrEmpty(labelOverride))
            EditorGUILayout.PropertyField(prop, includeChildren);
        else
            EditorGUILayout.PropertyField(prop, new GUIContent(labelOverride), includeChildren);
    }

    private static void DrawDebugNodes(ManeuverProfile profile, Vector3 start, Vector3 forward, float fuWorld, TurnDir dir)
    {
        if (profile == null || profile.pathMode != PathMode.BezierQuad) return;
        DrawLegacyArcNodes(profile, start, forward, fuWorld, dir);

        Vector3 fwd = forward;
        fwd.z = 0f;
        if (fwd.sqrMagnitude < 0.000001f) fwd = Vector3.up;
        fwd.Normalize();
        Vector3 right = new Vector3(-fwd.y, fwd.x, 0f).normalized;
        float sign = dir == TurnDir.D ? -1f : 1f;

        float totalDist = Mathf.Max(0f, profile.distanceFU) * Mathf.Max(0.01f, fuWorld);
        float leadFrac = Mathf.Clamp(profile.straightLeadInFrac, 0f, 0.9f);
        float leadDist = totalDist * leadFrac;
        Vector3 leadStart = start + fwd * leadDist;
        float remainingDist = totalDist - leadDist;

        float theta = profile.turnAngleDeg * sign;
        float thetaRad = theta * Mathf.Deg2Rad;
        Vector3 end = leadStart + fwd * remainingDist;
        if (Mathf.Abs(thetaRad) >= 0.0001f)
        {
            float radius = remainingDist / Mathf.Abs(thetaRad);
            Vector3 right0 = MovementCore.Rotate2D(fwd, -90f).normalized;
            float thetaSign = Mathf.Sign(thetaRad);
            Vector3 center = leadStart - right0 * (radius * thetaSign);
            end = center + MovementCore.Rotate2D(leadStart - center, theta);
        }
        float bias = Mathf.Clamp01(profile.curveBias);
        Vector3 control = leadStart
            + fwd * (remainingDist * profile.bezierForwardHandleFrac)
            + right * (remainingDist * profile.bezierLateralFrac * sign * bias);

        DrawNode(start, Color.white);
        DrawNode(leadStart, Color.red);
        DrawNode(control, Color.blue);
        DrawNode(end, Color.green);
    }

    private static void DrawNode(Vector3 pos, Color color)
    {
        Handles.color = color;
        Handles.DrawSolidDisc(pos, Vector3.forward, 0.08f);
    }

    private static void DrawLegacyArcNodes(ManeuverProfile profile, Vector3 start, Vector3 forward, float fuWorld, TurnDir dir)
    {
        Vector3 fwd = forward;
        fwd.z = 0f;
        if (fwd.sqrMagnitude < 0.000001f) fwd = Vector3.up;
        fwd.Normalize();

        float sign = dir == TurnDir.D ? -1f : 1f;
        float totalDist = Mathf.Max(0f, profile.distanceFU) * Mathf.Max(0.01f, fuWorld);
        float theta = profile.turnAngleDeg * sign;
        float thetaRad = theta * Mathf.Deg2Rad;

        if (Mathf.Abs(thetaRad) < 0.0001f)
        {
            Vector3 endStraight = start + fwd * totalDist;
            Handles.color = Color.gray;
            Handles.DrawAAPolyLine(2f, start, endStraight);
            DrawNode(start, Color.white);
            DrawNode(endStraight, Color.green);
            return;
        }

        float radius = totalDist / Mathf.Abs(thetaRad);
        Vector3 right0 = MovementCore.Rotate2D(fwd, -90f).normalized;
        float thetaSign = Mathf.Sign(thetaRad);
        Vector3 center = start - right0 * (radius * thetaSign);
        Vector3 end = center + MovementCore.Rotate2D(start - center, theta);

        DrawNode(center, new Color(0.2f, 0.6f, 1f));
    }
}
