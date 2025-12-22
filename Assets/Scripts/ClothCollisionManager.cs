using UnityEngine;
using System.Collections.Generic;

public class ClothCollisionManager : MonoBehaviour
{
    public ComputeShader clothCollisionShader;

    [Space(10)]
    public GameObject sphere;
    public GameObject cube;
    public GameObject _3DMesh;
    public GameObject groundPlane;
    private float groundPlaneHeight;

    [Space(10)]
    public bool bringClothAboveSphere = false;
    public bool bringClothAboveCube = false;
    public bool bringClothAbove3DMesh = false;

    [Space(10)]
    public bool stickClothToMesh = false;

    [Space(10)]
    [Range(0f, 1f)]
    private float frictionCoefficient = 0.1f;

    private MeshFilter mf;
    private List<TriangleInfo> triangleList = new();
    private List<TriangleInfo> tris;
    private Color nodeColor;

    internal BVH simpleBVH;
    internal BVHFlattener bvhFlattener;
    internal List<int> activeIndices = new List<int>();
    internal List<Vector3> activeClothPointsWS = new List<Vector3>();
    internal Vector3[] contactNormals;
    internal float meshBoundingBoxMargin = 0.5f;

    [Space(10)]
    public bool showBVHTree = false;
    public bool showMeshBoundingBox = false;

    private ClothSphereCollision clothSphereCollision;
    private ClothCubeCollision clothCubeCollision;
    private ClothGroundPlaneCollision clothGroundPlaneCollision;
    private ClothMeshCollision clothMeshCollision;


    void Start()
    {
        mf = _3DMesh.GetComponentInChildren<MeshFilter>();

        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogWarning("No MeshFilter or mesh found on this GameObject.");
            return;
        }

        tris = ExtractTriangles(mf);
        simpleBVH = new BVH(tris);

        bvhFlattener = new BVHFlattener();
        bvhFlattener.Flatten(simpleBVH.root);

        if (sphere != null)
        {
            clothSphereCollision = new ClothSphereCollision(this);
        }

        if (cube != null)
        {
            clothCubeCollision = new ClothCubeCollision(this);
        }

        if (groundPlane != null)
        {
            groundPlaneHeight = groundPlane.transform.position.y;
            clothGroundPlaneCollision = new ClothGroundPlaneCollision(this);
        }
        if (_3DMesh != null)
        {
            clothMeshCollision = new ClothMeshCollision(this);
        }
    }

    void Update()
    {
        groundPlaneHeight = groundPlane.transform.position.y;
        UpdateClothPosition();
    }

    public void UpdateClothPosition()
    {
        if (bringClothAboveSphere && sphere != null)
        {
            BringAbove(sphere);
            bringClothAboveSphere = false;
        }
        if (bringClothAboveCube && cube != null)
        {
            BringAbove(cube);
            bringClothAboveCube = false;
        }
        if (bringClothAbove3DMesh && _3DMesh != null)
        {
            BringAbove(_3DMesh);
            bringClothAbove3DMesh = false;
        }
    }

    private void BringAbove(GameObject target)
    {
        float liftHeight = 5.0f;

        if (!TryGetComponent(out ClothMeshGenerator clothMesh))
            return;

        Vector3 targetCenter = target.transform.position;
        transform.position = targetCenter + new Vector3(0, liftHeight, 0);
        transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

        clothMesh.ReinitializeCloth();
    }

    List<TriangleInfo> ExtractTriangles(MeshFilter mf)
    {
        Mesh mesh = mf.sharedMesh;
        Vector3[] vertices = mesh.vertices; // vertices
        int[] indices = mesh.triangles; // triangle indices

        triangleList.Clear();
        for (int i = 0; i < indices.Length; i += 3)
        {
            Vector3 v0 = mf.transform.TransformPoint(vertices[indices[i]]);
            Vector3 v1 = mf.transform.TransformPoint(vertices[indices[i + 1]]);
            Vector3 v2 = mf.transform.TransformPoint(vertices[indices[i + 2]]);

            TriangleInfo tri = new TriangleInfo(v0, v1, v2);
            triangleList.Add(tri);
        }

        return triangleList;
    }


    public void ResolvePredictedClothSphereCollisions(List<Vector3> predictedPositions, List<ClothSimulationData.ClothPoint> clothPoints, List<Vector3> previousPositions, float dt)
    {
        if (clothSphereCollision == null) return;
        clothSphereCollision.Resolve(predictedPositions, clothPoints, previousPositions, dt);
    }

    public void ResolvePredictedClothCubeCollisions(List<Vector3> predictedPositions, List<ClothSimulationData.ClothPoint> clothPoints, List<Vector3> previousPositions, float dt)
    {
        if (clothCubeCollision == null) return;
        clothCubeCollision.Resolve(predictedPositions, clothPoints, previousPositions, dt);
    }

    public void ResolvePredictedCloth3DMeshCollisions(List<Vector3> predictedPositions, List<ClothSimulationData.ClothPoint> clothPoints, List<Vector3> previousPositions, float dt)
    {
        if (clothMeshCollision == null) return;
        clothMeshCollision.Resolve(predictedPositions, clothPoints, previousPositions, dt);
    }

    public void ResolveClothGroundPlaneCollisions(List<Vector3> predictedPositions, List<ClothSimulationData.ClothPoint> clothPoints, List<Vector3> previousPositions)
    {
        if (clothGroundPlaneCollision == null) return;
        clothGroundPlaneCollision.Resolve(predictedPositions, clothPoints, previousPositions);
    }

    // This does O(n²) comparison - pay attention to that; I am not using it at the moment.
    public void ResolveClothSelfCollisions(List<Vector3> predictedPositions, List<ClothSimulationData.ClothPoint> clothPoints, float minDistance)
    {
        int count = clothPoints.Count;

        for (int i = 0; i < count; i++)
        {
            var pA = clothPoints[i];
            if (pA.isFixed) continue;

            for (int j = i + 1; j < count; j++)
            {
                var pB = clothPoints[j];
                if (pB.isFixed) continue;

                Vector3 posA = predictedPositions[i];
                Vector3 posB = predictedPositions[j];

                Vector3 delta = posB - posA;
                float dist = delta.magnitude;

                if (dist < minDistance && dist > 1e-6f)
                {
                    Vector3 correction = delta.normalized * (minDistance - dist) * 0.5f;

                    predictedPositions[i] -= correction;
                    predictedPositions[j] += correction;
                }
            }
        }
    }

    public float GetGroundPlaneHeight()
    {
        return groundPlaneHeight;
    }

    internal Bounds TransformBoundsToWorld(Bounds localBounds, Transform objTransform)
    {
        Vector3 center = objTransform.TransformPoint(localBounds.center);
        Vector3 extents = localBounds.extents;
        Vector3 worldExtents = objTransform.TransformVector(extents); // it applies rotation matrix after scaling. no position involved here.
        return new Bounds(center, 2f * new Vector3(Mathf.Abs(worldExtents.x), Mathf.Abs(worldExtents.y), Mathf.Abs(worldExtents.z)));
    }


    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        if (showBVHTree && simpleBVH?.root != null)
        {
            nodeColor = Color.yellow;
            DrawBVHNode(simpleBVH.root, 0);
        }

        // Visualize 3D Mesh boundary for debug purpose
        if (showMeshBoundingBox && _3DMesh != null)
        {
            MeshFilter mf = _3DMesh.GetComponentInChildren<MeshFilter>();
            if (mf != null)
            {
                Bounds localBounds = mf.sharedMesh.bounds;
                Bounds worldBounds = TransformBoundsToWorld(localBounds, _3DMesh.transform);
                worldBounds.Expand(meshBoundingBoxMargin);  // apply the same margin used in collision

                Gizmos.color = Color.magenta;
                Gizmos.DrawWireCube(worldBounds.center, worldBounds.size);
            }
        }
    }

    void DrawBVHNode(BVHNode node, int depth)
    {
        if (node != null)
        {
            Gizmos.color = nodeColor;
            Gizmos.DrawWireCube(node.bounds.center, node.bounds.size);

            if (!node.IsLeaf)
            {
                if (node.left != null) DrawBVHNode(node.left, depth + 1);
                if (node.right != null) DrawBVHNode(node.right, depth + 1);
            }
        }
    }
}