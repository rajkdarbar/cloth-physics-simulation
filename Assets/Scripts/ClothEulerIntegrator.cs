using UnityEngine;

public class ClothEulerIntegrator : ClothPhysicsBase
{
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

        foreach (var p in clothPoints)
        {
            if (p.isFixed) continue;

            p.force += gravity * p.mass;
            Vector3 acceleration = p.force / p.mass;

            // Semi-implicit Euler            
            p.velocity += acceleration * dt;
            p.position += p.velocity * dt;

            p.velocity *= decayFactor; // damping velocity

            p.force = Vector3.zero; // reset force
        }
    }
}