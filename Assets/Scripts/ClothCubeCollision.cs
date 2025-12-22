using UnityEngine;
using System.Collections.Generic;

public class ClothCubeCollision
{
    private ClothCollisionManager manager;

    public ClothCubeCollision(ClothCollisionManager manager)
    {
        this.manager = manager;
    }

    public void Resolve(List<Vector3> predictedPositions, List<ClothSimulationData.ClothPoint> clothPoints, List<Vector3> previousPositions, float dt)
    {
        if (manager.cube == null) return;

        Transform cubeTransform = manager.cube.transform;
        BoxCollider box = manager.cube.GetComponent<BoxCollider>();
        Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, cubeTransform.lossyScale);

        float margin = 0.00001f;

        for (int i = 0; i < clothPoints.Count; i++)
        {
            var p = clothPoints[i];
            if (p.isFixed) continue;

            Vector3 predictedPositionWS = manager.transform.TransformPoint(predictedPositions[i]); // world space
            Vector3 predictedPositionLS = cubeTransform.InverseTransformPoint(predictedPositionWS); // cube local space

            // Inside test
            if (Mathf.Abs(predictedPositionLS.x) > halfExtents.x ||
                Mathf.Abs(predictedPositionLS.y) > halfExtents.y ||
                Mathf.Abs(predictedPositionLS.z) > halfExtents.z)
                continue;

            // Penetration depths
            float penetrationX = halfExtents.x - Mathf.Abs(predictedPositionLS.x);
            float penetrationY = halfExtents.y - Mathf.Abs(predictedPositionLS.y);
            float penetrationZ = halfExtents.z - Mathf.Abs(predictedPositionLS.z);

            // Push out along the axis with the smallest penetration
            if (penetrationX <= penetrationY && penetrationX <= penetrationZ)
            {
                predictedPositionLS.x = Mathf.Sign(predictedPositionLS.x) * (halfExtents.x + margin);
            }
            else if (penetrationY <= penetrationX && penetrationY <= penetrationZ)
            {
                predictedPositionLS.y = Mathf.Sign(predictedPositionLS.y) * (halfExtents.y + margin);
            }
            else
            {
                predictedPositionLS.z = Mathf.Sign(predictedPositionLS.z) * (halfExtents.z + margin);
            }

            // Cube local → world → cloth local
            Vector3 correctedClothPositionWS = cubeTransform.TransformPoint(predictedPositionLS);
            predictedPositions[i] = manager.transform.InverseTransformPoint(correctedClothPositionWS);

            if (manager.stickClothToMesh)
                clothPoints[i].isFixed = true;
        }
    }
}