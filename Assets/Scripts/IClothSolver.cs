using UnityEngine;

public interface IClothSolver
{
    void Setup(Vector3[] vertices, int width, int height, float stiffness, float damping, float mass);
    void SetClothTransform(Transform transform);
    
    void UpdateParameters(float mass, float stiffness, float damping);
    void ApplySpringForces();
    void ApplyWindForceFromTexture(Texture2D windTex, float windStrength, Vector2[] uvs);

    void Integrate(float dt);

    void CopyUpdatedPositions(Vector3[] buffer);
}
