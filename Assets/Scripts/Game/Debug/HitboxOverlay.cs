using UnityEngine;

public class HitboxOverlay : MonoBehaviour
{
    public SpriteRenderer sr;

    private void Awake()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
    }

    public void SetVisible(bool show)
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = show;
    }
}
