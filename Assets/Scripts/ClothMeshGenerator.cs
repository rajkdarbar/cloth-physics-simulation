/*

width = 2, height = 2
Total quads = 2 x 2 = 4; it means 1 quad per grid cell
Total triangles = 4 x 2 = 8; it refers 2 triangles per quad
Total vertices = (2+1) × (2+1) = 9

Index layout:

0 --- 1 --- 2
|  /  |  /  |
3 --- 4 --- 5
|  /  |  /  |
6 --- 7 --- 8

*/

using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ClothMeshGenerator : MonoBehaviour
{
    [Header("Cloth Grid Settings")]
    public int width = 20;
    public int height = 20;
    [Range(0.01f, 1f)] public float spacing = 0.1f;
    private Vector3[] initialVertices;

    [Header("WindMap Textures and Cloth Materials")]
    public Texture2D windMapTexture;
    public Material debugMaterial;
    public Material clothMaterial;

    [Header("Cloth Properties")]
    [Range(0.1f, 5f)] public float mass = 0.65f;
    [Range(1f, 800f)] public float stiffness = 300f;
    [Range(0.1f, 20f)] public float damping = 2f;
    [Range(0f, 10f)] public float windStrength = 5f;
    [Range(100f, 1000f)] public float dragStrength = 200f;

    private float lastMass = 0f;
    private float lastStiffness = 0f;
    private float lastDamping = 0f;

    private Camera mainCamera;
    private int selectedPointIndex = -1;

    public IClothSolver clothSolver;
    private Vector2[] uvs;

    public enum IntegrationMode
    {
        SemiImplicitEuler,
        VerletIntegrator,
        PBDIntergrator
    }

    [Header("Different Ingegration Modes")]
    public IntegrationMode integrationMode = IntegrationMode.SemiImplicitEuler;

    [Space(10)]
    public ClothCollisionManager collisionManager;

    [Space(10)]
    public bool showClothGizmo = false;
    private bool lastShowClothGizmo = true;


    public bool allowClothToFall = false;
    private bool lastAllowClothToFallState = false;

    private Vector3[] vertexBuffer;
    private Mesh mesh;
    private MeshRenderer meshRenderer;

    private IReadOnlyList<ClothSimulationData.ClothPoint> gizmoPointsSnapshot;
    private List<ClothSimulationData.Spring> gizmoStructural;
    private List<ClothSimulationData.Spring> gizmoShear;
    private List<ClothSimulationData.Spring> gizmoBend;


    void Start()
    {
        mainCamera = Camera.main;

        Vector3[] vertices = GenerateClothMesh(out uvs);
        initialVertices = vertices;

        vertexBuffer = new Vector3[initialVertices.Length];

        mesh = GetComponent<MeshFilter>().mesh;
        meshRenderer = GetComponent<MeshRenderer>();
        mesh.MarkDynamic();

        switch (integrationMode)
        {
            case IntegrationMode.SemiImplicitEuler:
                clothSolver = new ClothEulerIntegrator();
                break;

            case IntegrationMode.VerletIntegrator:
                clothSolver = new ClothVerletIntegrator();
                break;

            case IntegrationMode.PBDIntergrator:
                clothSolver = new ClothPBDIntegrator();
                break;
        }

        if (clothSolver is ClothPhysicsBase baseSolver)
        {
            baseSolver.SetAllowClothToFall(allowClothToFall);
        }

        clothSolver.Setup(vertices, width, height, stiffness, damping, mass);
        clothSolver.SetClothTransform(transform);

        // PBD solver is used when cloth–object collisions are enabled
        if (clothSolver is ClothPBDIntegrator pbdSolver)
        {
            pbdSolver.SetCollisionManager(collisionManager);
            pbdSolver.SetMinSelfDistance(spacing);
        }
    }

    void Update()
    {
        // ----------------------- Simulation Part -----------------------

        if (allowClothToFall != lastAllowClothToFallState)
        {
            ToggleClothFall(allowClothToFall);
            lastAllowClothToFallState = allowClothToFall;
        }

        // Order matters: physics forces (springs, wind) → user constraint (mouse drag) → integration  

        if (mass != lastMass || stiffness != lastStiffness || damping != lastDamping)
        {
            clothSolver.UpdateParameters(mass, stiffness, damping);

            lastMass = mass;
            lastStiffness = stiffness;
            lastDamping = damping;
        }

        clothSolver.ApplySpringForces();
        clothSolver.ApplyWindForceFromTexture(windMapTexture, windStrength, uvs);

        HandleMouseDrag();

        clothSolver.Integrate(Time.deltaTime);


        // ----------------------- Rendering Part -----------------------

        clothSolver.CopyUpdatedPositions(vertexBuffer);
        mesh.vertices = vertexBuffer;

        if (showClothGizmo != lastShowClothGizmo)
        {
            if (showClothGizmo && debugMaterial != null)
                meshRenderer.sharedMaterial = debugMaterial;

            else if (clothMaterial != null)
                meshRenderer.sharedMaterial = clothMaterial;

            lastShowClothGizmo = showClothGizmo;
        }

        // Gizmo snapshot (READ-ONLY, once per frame)
        if (showClothGizmo && clothSolver is ClothPhysicsBase baseSolver)
        {
            gizmoPointsSnapshot = baseSolver.GetClothPoints();
            gizmoStructural = baseSolver.GetStructuralSprings();
            gizmoShear = baseSolver.GetShearSprings();
            gizmoBend = baseSolver.GetBendSprings();
        }
    }

    Vector3[] GenerateClothMesh(out Vector2[] outUVs)
    {
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[(width + 1) * (height + 1)];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangleIndices = new int[width * height * 6]; // 6 indices per quad (2 triangles × 3 vertices)

        // Generate vertices and UVs
        for (int y = 0; y <= height; y++)
        {
            for (int x = 0; x <= width; x++)
            {
                int i = y * (width + 1) + x;
                vertices[i] = new Vector3((x - width * 0.5f) * spacing, -(y - height * 0.5f) * spacing, 0); // Z = 0; cloth is in XY plane
                uv[i] = new Vector2((float)x / width, (float)y / height);
            }
        }

        // Generate two triangles per quad
        // We are traversing row-wise to access the index of each vertex of a triangle
        int triIndex = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * (width + 1) + x;

                triangleIndices[triIndex++] = i;
                triangleIndices[triIndex++] = i + width + 1;
                triangleIndices[triIndex++] = i + 1;

                triangleIndices[triIndex++] = i + 1;
                triangleIndices[triIndex++] = i + width + 1;
                triangleIndices[triIndex++] = i + width + 2;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangleIndices;
        mesh.uv = uv;
        mesh.RecalculateNormals();
        GetComponent<MeshFilter>().mesh = mesh;

        outUVs = uv;
        return vertices;
    }


    void HandleMouseDrag()
    {
        if (!(clothSolver is ClothPhysicsBase baseSolver)) return;

        var clothPoints = baseSolver.GetClothPoints();

        if (Input.GetMouseButtonDown(0))
        {
            if (!SelectVertexScreenSpace(clothPoints, out selectedPointIndex))
                selectedPointIndex = -1;
        }

        else if (Input.GetMouseButtonUp(0))
        {
            selectedPointIndex = -1;
        }

        if (selectedPointIndex >= 0 && Input.GetMouseButton(0))
        {
            var p = clothPoints[selectedPointIndex];

            if (p.isFixed) return;

            // Vertex local → world
            Vector3 vertexWorld = transform.TransformPoint(p.position);

            // Get its screen-space depth
            float depth = mainCamera.WorldToScreenPoint(vertexWorld).z; // view-space depth, preserved before nonlinear projection

            // Mouse screen position + that depth
            Vector3 mouseScreen = new Vector3(
                Input.mousePosition.x,
                Input.mousePosition.y,
                depth
            );

            // Mouse projected into world space at vertex depth
            Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(mouseScreen);

            // Convert back to local cloth space
            Vector3 mouseLocal = transform.InverseTransformPoint(mouseWorld);

            // Apply drag as force in local cloth space
            p.force += (mouseLocal - p.position) * dragStrength;
        }
    }

    bool SelectVertexScreenSpace(IReadOnlyList<ClothSimulationData.ClothPoint> points, out int index)
    {
        index = -1;
        float bestDist = float.MaxValue;
        float maxScreenDistance = 30f; // pixels

        Vector2 mousePos = Input.mousePosition;

        // Brute-force search is fine here for a few hundred points
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 clothVertexWorldPos = transform.TransformPoint(points[i].position);
            Vector3 clothVertexScreenPos = mainCamera.WorldToScreenPoint(clothVertexWorldPos); // view-space depth, preserved before nonlinear projection

            // Ignore if behind camera
            if (clothVertexScreenPos.z < 0f) continue;

            float dist = Vector2.Distance(mousePos, new Vector2(clothVertexScreenPos.x, clothVertexScreenPos.y));
            if (dist < maxScreenDistance && dist < bestDist)
            {
                bestDist = dist;
                index = i;
            }
        }

        return index >= 0;
    }

    public void ToggleClothFall(bool enable)
    {
        allowClothToFall = enable;

        // Inform solver
        if (clothSolver is ClothPhysicsBase baseSolver)
        {
            baseSolver.SetAllowClothToFall(enable);
            baseSolver.Setup(initialVertices, width, height, stiffness, damping, mass); // reset to initial mesh vertices   
        }
    }

    public void ReinitializeCloth()
    {
        clothSolver.Setup(initialVertices, width, height, stiffness, damping, mass);

        if (clothSolver is ClothPBDIntegrator pbdSolver)
        {
            pbdSolver.SetCollisionManager(collisionManager);
        }
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        if (!showClothGizmo) return;

        if (gizmoPointsSnapshot == null) return;

        Transform t = transform;

        DrawSprings(gizmoStructural, gizmoPointsSnapshot, t, Color.red);
        DrawSprings(gizmoShear, gizmoPointsSnapshot, t, Color.green);
        DrawSprings(gizmoBend, gizmoPointsSnapshot, t, Color.blue);

        Gizmos.color = Color.black;
        foreach (var p in gizmoPointsSnapshot)
        {
            Vector3 wp = t.TransformPoint(p.position);
            Gizmos.DrawSphere(wp, 0.01f);
        }

        if (selectedPointIndex >= 0 && selectedPointIndex < gizmoPointsSnapshot.Count)
        {
            Gizmos.color = Color.red;
            Vector3 wp = t.TransformPoint(gizmoPointsSnapshot[selectedPointIndex].position);
            Gizmos.DrawSphere(wp, 0.05f);
        }
    }

    void DrawSprings(
        List<ClothSimulationData.Spring> springs,
        IReadOnlyList<ClothSimulationData.ClothPoint> points,
        Transform t,
        Color color)
    {
        if (springs == null) return;

        Gizmos.color = color;

        foreach (var s in springs)
        {
            Vector3 a = t.TransformPoint(points[s.indexA].position);
            Vector3 b = t.TransformPoint(points[s.indexB].position);
            Gizmos.DrawLine(a, b);
        }
    }



}