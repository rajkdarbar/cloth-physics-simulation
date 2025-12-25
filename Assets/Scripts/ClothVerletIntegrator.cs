using System.Collections.Generic;
using UnityEngine;

public class ClothVerletIntegrator : ClothPhysicsBase
{
    private List<Vector3> previousPositions = new();

    // Setup() called once inside Start()
    public override void Setup(Vector3[] vertices, int width, int height, float stiffness, float damping, float mass)
    {
        base.Setup(vertices, width, height, stiffness, damping, mass);

        previousPositions.Clear();
        foreach (var p in clothPoints)
        {
            previousPositions.Add(p.position);
        }
    }

    public override void Integrate(float dt)
    {
        int maxSubsteps = 5;
        float maxDt = 1f / 90f;
        int substeps = 0;

        while (dt > 0f && substeps < maxSubsteps)
        {
            float step = Mathf.Min(dt, maxDt);
            DoIntegration(step);
            dt -= step;
            substeps++;
        }
    }

    public void DoIntegration(float dt)
    {
        Vector3 gravity = clothTransform.InverseTransformDirection(Vector3.down * 9.81f);
        float decayFactor = 0.98f;

        for (int i = 0; i < clothPoints.Count; i++)
        {
            var p = clothPoints[i];
            if (p.isFixed) continue;

            p.force += gravity * p.mass;
            Vector3 acceleration = p.force / p.mass;

            Vector3 temp = p.position; // store current position

            // Position Verlet integration (hacky)
            p.position =
                p.position
                + (p.position - previousPositions[i]) * decayFactor
                + acceleration * dt * dt;

            p.velocity = (p.position - temp) * decayFactor; // derive velocity AFTER position update; this is not pure Verlet, it’s a numerical hack

            // Shift history
            previousPositions[i] = temp;

            // Reset force
            p.force = Vector3.zero;
        }
    }
}