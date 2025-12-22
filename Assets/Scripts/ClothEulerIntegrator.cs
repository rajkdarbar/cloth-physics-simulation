using UnityEngine;

public class ClothEulerIntegrator : ClothPhysicsBase
{
    public override void Integrate(float dt)
    {
        Vector3 gravity = clothTransform.InverseTransformDirection(Vector3.down * 9.81f);

        foreach (var p in clothPoints)
        {
            if (p.isFixed) continue;

            p.force += gravity * p.mass;
            Vector3 acceleration = p.force / p.mass;

            // Semi-implicit Euler            
            p.velocity += acceleration * dt;
            p.position += p.velocity * dt;

            p.velocity *= 0.82f; // damping factor on velocity (optional)

            p.force = Vector3.zero; // reset force
        }
    }
}