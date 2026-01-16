using UnityEditor;
using UnityEngine;

namespace Zone5.EditorTools
{
    [InitializeOnLoad]
    public static class ClearInvalidSelectionOnLoad
    {
        static ClearInvalidSelectionOnLoad()
        {
            EditorApplication.delayCall += ClearIfInvalid;
        }

        private static void ClearIfInvalid()
        {
            var objects = Selection.objects;
            if (objects == null || objects.Length == 0) return;

            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] == null)
                {
                    Selection.activeObject = null;
                    Selection.objects = new UnityEngine.Object[0];
                    break;
                }
            }
        }
    }
}
