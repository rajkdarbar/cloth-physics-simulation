using UnityEngine;
using System.Collections.Generic;

public class ClothSphereCollision
{
    private ClothCollisionManager manager;

    public ClothSphereCollision(ClothCollisionManager manager)
    {
        this.manager = manager;
    }

    public void Resolve(List<Vector3> predictedPositions, List<ClothSimulationData.ClothPoint> clothPoints, List<Vector3> previousPositions, float dt)
    {
        if (manager.sphere == null) return;

        Transform sphereTransform = manager.sphere.transform;
        Vector3 sphereCenter = sphereTransform.position;
        float scaleFactor = Mathf.Max(sphereTransform.lossyScale.x, sphereTransform.lossyScale.y, sphereTransform.lossyScale.z);

        SphereCollider sphereCollider = manager.sphere.GetComponent<SphereCollider>();
        float radius = sphereCollider.radius * scaleFactor;

        float margin = 0.005f;

        for (int i = 0; i < clothPoints.Count; i++)
        {
            if (clothPoints[i].isFixed) continue;

            Vector3 predictedPositionWS = manager.transform.TransformPoint(predictedPositions[i]);
            Vector3 fromCenterToPredictedPositionWS = predictedPositionWS - sphereCenter;
            float dist = fromCenterToPredictedPositionWS.magnitude;

            if (dist >= radius + margin)
                continue;

            // Always project
            Vector3 normal = fromCenterToPredictedPositionWS.normalized;
            Vector3 correctedClothPositionWS = sphereCenter + normal * (radius + margin);
            predictedPositions[i] = manager.transform.InverseTransformPoint(correctedClothPositionWS); // local space

            // Sticky mode: freeze AFTER projection
            if (manager.stickClothToMesh)
            {
                clothPoints[i].isFixed = true;
                continue;
            }
        }
    }
}
