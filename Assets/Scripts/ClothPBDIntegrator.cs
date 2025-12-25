using System.Collections.Generic;
using UnityEngine;

public class ClothPBDIntegrator : ClothPhysicsBase
{
    private List<Vector3> previousPositions = new();
    private List<Vector3> predictedPositions = new();

    private const int solverIterations = 8;
    private float dampingFactor = 0.98f;
    private float minSelfDistance;

    private bool wasStickyLastFrame = false;
    private ClothCollisionManager collisionManager;

    public void SetCollisionManager(ClothCollisionManager manager)
    {
        collisionManager = manager;
    }

    public void SetMinSelfDistance(float val)
    {
        minSelfDistance = val;
    }

    // Setup() called once inside Start()
    public override void Setup(Vector3[] vertices, int width, int height, float stiffness, float damping, float mass)
    {
        base.Setup(vertices, width, height, stiffness, damping, mass);

        previousPositions.Clear();
        predictedPositions.Clear();

        foreach (var p in clothPoints)
        {
            previousPositions.Add(p.position);
            predictedPositions.Add(p.position);
        }
    }

    // Call UpdateParameters() per frame
    public override void UpdateParameters(float mass, float stiffness, float damping)
    {
        base.UpdateParameters(mass, stiffness, damping);

        dampingFactor = Mathf.Exp(-damping * 0.2f); // exponential decay
    }

    // Call ApplySpringForces() per frame
    public override void ApplySpringForces()
    {
        // PBD ignores force-based springs; constraints handle geometric deformation.
        // Reset forces to avoid force accumulation from base class.
        foreach (var p in clothPoints)
        {
            p.force = Vector3.zero;
        }
    }

    // Call Integrate() per frame 
    public override void Integrate(float dt)
    {
        Vector3 gravity = clothTransform.InverseTransformDirection(Vector3.down * 9.81f);

        bool stickyNow = collisionManager != null && collisionManager.stickClothToMesh;

        if (wasStickyLastFrame && !stickyNow)
        {
            // UNFREEZE: restore motion source (velocity)
            for (int i = 0; i < clothPoints.Count; i++)
            {
                clothPoints[i].isFixed = false;

                clothPoints[i].velocity += Vector3.down * 0.2f; // small kick so motion resumes immediately
            }
        }

        wasStickyLastFrame = stickyNow;

        // Step 1: Predict positions
        for (int i = 0; i < clothPoints.Count; i++)
        {
            var p = clothPoints[i];

            if (p.isFixed)
            {
                // FREEZE: kill motion at the source
                p.velocity = Vector3.zero;
                predictedPositions[i] = p.position;
                previousPositions[i] = p.position;
                continue;
            }

            p.force += gravity * p.mass;
            Vector3 acceleration = p.force / p.mass;

            /*

            // Verlet-style position update
            // First frame: delta = 0
            // Later frames: delta ≠ 0 → tracks displacement across frames           

            Vector3 delta = p.position - previousPositions[i];

            previousPositions[i] = p.position;
            predictedPositions[i] = p.position + delta + acceleration * dt * dt;

            */


            // Predict position using semi-implicit Euler integration
            p.velocity = p.velocity + acceleration * dt; // integrate velocity first

            previousPositions[i] = p.position;
            predictedPositions[i] = p.position + p.velocity * dt; // don't commit yet
        }

        // Step 2: Satisfy constraints iteratively (distance constraints)
        for (int iter = 0; iter < solverIterations; iter++)
        {
            SatisfyConstraints(structuralSprings);
            SatisfyConstraints(shearSprings);
            SatisfyConstraints(bendSprings);
        }

        // Step 3: Collisions check
        collisionManager?.ResolvePredictedClothSphereCollisions(predictedPositions, clothPoints, previousPositions, dt); // pass by reference
        collisionManager?.ResolvePredictedClothCubeCollisions(predictedPositions, clothPoints, previousPositions, dt); // pass by reference
        collisionManager?.ResolvePredictedCloth3DMeshCollisions(predictedPositions, clothPoints, previousPositions, dt); // pass by reference 
        collisionManager?.ResolveClothGroundPlaneCollisions(predictedPositions, clothPoints, previousPositions); // pass by reference
        //collisionManager?.ResolveClothSelfCollisions(predictedPositions, clothPoints, minSelfDistance);

        // Step 4: Update positions and velocities
        // We don't need previousPositions here as it has already served its purpose earlier.        
        for (int i = 0; i < clothPoints.Count; i++)
        {
            if (clothPoints[i].isFixed) continue;

            Vector3 newPos = predictedPositions[i];

            Vector3 rawVelocity = (newPos - clothPoints[i].position) / dt; // velocity reconstruction              
            clothPoints[i].velocity = rawVelocity * dampingFactor;  // overwrites the earlier velocity

            clothPoints[i].position = newPos;  // commit final position
        }
    }

    private void SatisfyConstraints(List<ClothSimulationData.Spring> springs)
    {
        foreach (var s in springs)
        {
            int iA = s.indexA;
            int iB = s.indexB;

            var A = clothPoints[iA];
            var B = clothPoints[iB];

            Vector3 posA = predictedPositions[iA];
            Vector3 posB = predictedPositions[iB];

            Vector3 delta = posB - posA;
            Vector3 dir = delta.normalized;

            float dist = delta.magnitude;
            if (dist == 0f) continue;

            float constraint = dist - s.restLength;

            // Inverse masses
            float wA = A.isFixed ? 0f : 1f / A.mass;
            float wB = B.isFixed ? 0f : 1f / B.mass;

            float wSum = wA + wB;
            if (wSum == 0f) continue;

            float k = Mathf.Clamp01(s.stiffness / 1200f); // stiffness does not mean “material rigidity” but how fast constraints converge per iteration

            if (!A.isFixed)
                predictedPositions[iA] += k * (wA / wSum) * constraint * dir;

            if (!B.isFixed)
                predictedPositions[iB] -= k * (wB / wSum) * constraint * dir;
        }
    }
}
