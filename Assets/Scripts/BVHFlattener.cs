using System.Collections.Generic;
using UnityEngine;

public struct BVHTreeNode
{
    public Vector3 boundsMin;
    public Vector3 boundsMax;
    public int leftChild; // index in array (-1 if leaf)
    public int rightChild; // index in array (-1 if leaf)
    public int startTriangle; // start index in triangle array
    public int triangleCount; // number of triangles if leaf
}

public struct Triangle
{
    public Vector3 v0;
    public Vector3 v1;
    public Vector3 v2;
}

public class BVHFlattener
{
    private List<BVHTreeNode> Nodes = new List<BVHTreeNode>();
    private List<Triangle> Triangles = new List<Triangle>();

    public void Flatten(BVHNode root)
    {
        Nodes.Clear();
        Triangles.Clear();

        FlattenNode(root); // pre-order traversal
    }

    private int FlattenNode(BVHNode node)
    {
        int currentIndex = Nodes.Count;

        BVHTreeNode bvhNode = new BVHTreeNode
        {
            boundsMin = node.bounds.min,
            boundsMax = node.bounds.max,
            leftChild = -1,
            rightChild = -1,
            startTriangle = Triangles.Count,
            triangleCount = 0
        };

        Nodes.Add(bvhNode);

        if (node.IsLeaf)
        {
            foreach (var tri in node.triangles)
            {
                Triangles.Add(new Triangle
                {
                    v0 = tri.v0,
                    v1 = tri.v1,
                    v2 = tri.v2
                });
            }

            bvhNode.triangleCount = node.triangles.Count;
        }
        else
        {
            bvhNode.leftChild = FlattenNode(node.left); // assign left child index
            bvhNode.rightChild = FlattenNode(node.right); // assign right child index
        }

        Nodes[currentIndex] = bvhNode;

        return currentIndex;
    }

    public List<BVHTreeNode> GetNodes() => Nodes;
    public List<Triangle> GetTriangles() => Triangles;
}
