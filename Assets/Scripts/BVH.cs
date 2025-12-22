using System.Collections.Generic;
using UnityEngine;

public class BVH
{
    public BVHNode root;
    public int maxTrianglesPerLeaf = 256;

    public BVH(List<TriangleInfo> triangleList)
    {
        root = Build(triangleList, 0);
    }

    // Quickly update the BVH without rebuilding the entire tree 
    public void Refit(BVHNode node)
    {
        if (node == null) return;

        if (node.IsLeaf)
        {
            foreach (var tri in node.triangles)
                tri.UpdateBounds();

            node.bounds = ComputeBounds(node.triangles);
        }
        else
        {
            Refit(node.left);
            Refit(node.right);

            node.bounds = node.left.bounds;
            node.bounds.Encapsulate(node.right.bounds);
        }
    }

    // Using median splitting 
    private BVHNode Build(List<TriangleInfo> tris, int depth)
    {
        BVHNode node = new BVHNode();
        node.bounds = ComputeBounds(tris);

        if (tris.Count <= maxTrianglesPerLeaf)
        {
            node.triangles = tris;
            return node;
        }

        // Split along the longest axis of the centroid bounds created from all triangles' centroids
        Bounds centroidBounds = ComputeCentroidBounds(tris);
        int axis = GetLongestAxis(centroidBounds.size);

        // Sort triangles based on their centroid’s value along a specific axis
        tris.Sort((a, b) => a.centroid[axis].CompareTo(b.centroid[axis])); // lambda expression
        int mid = tris.Count / 2;

        var leftTris = tris.GetRange(0, mid);
        var rightTris = tris.GetRange(mid, tris.Count - mid);

        node.left = Build(leftTris, depth + 1);
        node.right = Build(rightTris, depth + 1);

        return node;
    }

    private Bounds ComputeBounds(List<TriangleInfo> tris)
    {
        Bounds b = tris[0].bounds;

        for (int i = 1; i < tris.Count; i++)
            b.Encapsulate(tris[i].bounds);

        return b;
    }

    private Bounds ComputeCentroidBounds(List<TriangleInfo> tris)
    {
        Bounds b = new Bounds(tris[0].centroid, Vector3.zero);

        for (int i = 1; i < tris.Count; i++)
            b.Encapsulate(tris[i].centroid);

        return b;
    }

    private int GetLongestAxis(Vector3 size)
    {
        if (size.x >= size.y && size.x >= size.z) return 0;
        if (size.y >= size.z) return 1;
        return 2;
    }
}
