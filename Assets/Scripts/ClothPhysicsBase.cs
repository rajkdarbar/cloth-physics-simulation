using System.Collections.Generic;
using UnityEngine;

public abstract class ClothPhysicsBase : IClothSolver
{
    protected List<ClothSimulationData.ClothPoint> clothPoints = new();
    protected List<ClothSimulationData.Spring> structuralSprings = new();
    protected List<ClothSimulationData.Spring> shearSprings = new();
    protected List<ClothSimulationData.Spring> bendSprings = new();

    private float windTime = 0f;
    private float windScrollSpeed = 0.2f;

    protected bool allowClothToFall;

    protected Transform clothTransform;

    public void SetAllowClothToFall(bool allow)
    {
        allowClothToFall = allow;
    }

    public virtual void Setup(Vector3[] vertices, int width, int height, float stiffness, float damping, float mass)
    {
        int countX = width + 1;

        clothPoints.Clear();
        structuralSprings.Clear();
        shearSprings.Clear();
        bendSprings.Clear();

        foreach (Vector3 v in vertices)
            clothPoints.Add(new ClothSimulationData.ClothPoint(v, false, mass));

        if (!allowClothToFall)
        {
            // Fix entire top row
            for (int x = 0; x <= width; x++)
                clothPoints[x].isFixed = true;
        }

        for (int y = 0; y <= height; y++)
        {
            for (int x = 0; x <= width; x++)
            {
                int i = y * countX + x;

                // Structural Springs (Immediate Neighbors)
                if (x < width)
                    AddSpring(i, i + 1, stiffness, damping, structuralSprings); // right               

                if (y < height)
                    AddSpring(i, i + countX, stiffness, damping, structuralSprings); // down        

                // Shear Springs (Diagonal Neighbors)
                if (x < width && y < height)
                    AddSpring(i, i + countX + 1, stiffness, damping, shearSprings); // diagonal right-down  

                if (x > 0 && y < height)
                    AddSpring(i, i + countX - 1, stiffness, damping, shearSprings); // diagonal left-down

                // Bend Springs (Skip 1 Neighbors)
                if (x < width - 1)
                    AddSpring(i, i + 2, stiffness * 0.6f, damping, bendSprings); // bend right

                if (y < height - 1)
                    AddSpring(i, i + 2 * countX, stiffness * 0.6f, damping, bendSprings); // bend down
            }
        }
    }

    protected void AddSpring(int a, int b, float stiffness, float damping, List<ClothSimulationData.Spring> springList)
    {
        float restLength = Vector3.Distance(clothPoints[a].position, clothPoints[b].position); // rest length
        springList.Add(new ClothSimulationData.Spring(a, b, restLength, stiffness, damping));
    }

    public void SetClothTransform(Transform t)
    {
        clothTransform = t;
    }

    public virtual void UpdateParameters(float mass, float stiffness, float damping)
    {
        foreach (var p in clothPoints)
        {
            if (!p.isFixed)
                p.mass = mass;
        }

        foreach (var s in structuralSprings)
        {
            s.stiffness = stiffness;
            s.damping = damping;
        }

        foreach (var s in shearSprings)
        {
            s.stiffness = stiffness;
            s.damping = damping;
        }

        foreach (var s in bendSprings)
        {
            s.stiffness = stiffness * 0.6f;
            s.damping = damping;
        }
    }

    public virtual void ApplySpringForces()
    {
        CalculateForce(structuralSprings);
        CalculateForce(shearSprings);
        CalculateForce(bendSprings);
    }

    void CalculateForce(List<ClothSimulationData.Spring> springs)
    {
        foreach (var s in springs)
        {
            var A = clothPoints[s.indexA];
            var B = clothPoints[s.indexB];

            Vector3 delta = B.position - A.position;

            float dist = delta.magnitude;
            if (dist == 0) continue;

            Vector3 dir = delta.normalized;
            float stretch = dist - s.restLength;

            Vector3 springForce = s.stiffness * stretch * dir; // Hooke’s law

            Vector3 relativeVelocity = B.velocity - A.velocity;
            float relativeSpeedAlongSpring = Vector3.Dot(relativeVelocity, dir); // scalar

            Vector3 dampingForce = s.damping * relativeSpeedAlongSpring * dir; // damping handles energy loss / stabilization

            Vector3 totalForce = springForce + dampingForce;

            if (!A.isFixed) A.force += totalForce;
            if (!B.isFixed) B.force -= totalForce;
        }
    }

    public virtual void ApplyWindForceFromTexture(Texture2D windTexture, float windStrength, Vector2[] uvs)
    {
        if (windTexture == null) return;

        windTime += Time.deltaTime * windScrollSpeed;

        for (int i = 0; i < clothPoints.Count; i++)
        {
            if (clothPoints[i].isFixed) continue;

            Vector2 uv = uvs[i];
            uv.x = (uv.x + windTime) % 1f; // % 1f wraps the texture (tiling)

            Color c = windTexture.GetPixelBilinear(uv.x, uv.y); // [0, 1] range            
            Vector3 windDir = new Vector3(c.r, c.g, c.b) * 2f - Vector3.one; // convert color to [-1, 1] wind vector            
            windDir.y *= 0.2f; // flatten Y (vertical) component

            float gust = 0.6f + 0.4f * Mathf.Sin(windTime * 2f + uv.x * 5f + uv.y * 3f + i * 0.01f); // sinusoidal gust variation            
            float jitter = 0.9f + 0.2f * Mathf.Sin(windTime * 5f + i); // small sinusoidal jitter

            float noise = Mathf.PerlinNoise(uv.x * 10f + windTime, uv.y * 10f + windTime); // add smooth Perlin noise variation
            float perlinFactor = 0.8f + 0.4f * noise;

            Vector3 force = windDir.normalized * Mathf.Pow(windStrength * gust * jitter * perlinFactor, 1.8f); // final wind force
            clothPoints[i].force += force;
        }
    }

    public abstract void Integrate(float dt);

    public void CopyUpdatedPositions(Vector3[] buffer)
    {
        for (int i = 0; i < clothPoints.Count; i++)
            buffer[i] = clothPoints[i].position;
    }

    public IReadOnlyList<ClothSimulationData.ClothPoint> GetClothPoints() => clothPoints;
    public List<ClothSimulationData.Spring> GetStructuralSprings() => structuralSprings;
    public List<ClothSimulationData.Spring> GetShearSprings() => shearSprings;
    public List<ClothSimulationData.Spring> GetBendSprings() => bendSprings;
}
