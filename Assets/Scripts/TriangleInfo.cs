using UnityEngine;

public class TriangleInfo
{
    public Vector3 v0, v1, v2;
    public Bounds bounds;
    public Vector3 centroid;

    public TriangleInfo(Vector3 a, Vector3 b, Vector3 c)
    {
        v0 = a;
        v1 = b;
        v2 = c;

        bounds = new Bounds(v0, Vector3.zero);
        bounds.Encapsulate(v1);
        bounds.Encapsulate(v2);

        centroid = (v0 + v1 + v2) / 3f;
    }

    public void UpdateBounds()
    {
        bounds = new Bounds(v0, Vector3.zero);
        bounds.Encapsulate(v1);
        bounds.Encapsulate(v2);

        centroid = (v0 + v1 + v2) / 3f;
    }
}

