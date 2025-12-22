using System.Collections.Generic;
using UnityEngine;

public class BVHNode
{
    public Bounds bounds;
    public List<TriangleInfo> triangles;
    public BVHNode left;
    public BVHNode right;

    public bool IsLeaf
    {
        get
        {
            return left == null && right == null;
        }
    }
}

