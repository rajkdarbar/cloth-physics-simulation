using UnityEngine;
using System.Collections.Generic;

public class ClothGroundPlaneCollision
{
    private ClothCollisionManager manager;

    public ClothGroundPlaneCollision(ClothCollisionManager manager)
    {
        this.manager = manager;
    }

    public void Resolve(List<Vector3> predictedPositions, List<ClothSimulationData.ClothPoint> clothPoints, List<Vector3> previousPositions)
    {
        float margin = 0.32f;

        for (int i = 0; i < clothPoints.Count; i++)
        {
            var p = clothPoints[i];
            if (p.isFixed) continue;

            Vector3 predictedPositionWS = manager.transform.TransformPoint(predictedPositions[i]); // world space

            if (predictedPositionWS.y < manager.GetGroundPlaneHeight() + margin)
            {
                // Snap point slightly above the floor
                Vector3 correctedClothPositionWS = new Vector3(predictedPositionWS.x, manager.GetGroundPlaneHeight() + margin, predictedPositionWS.z);
                Vector3 correctedClothPositionLS = manager.transform.InverseTransformPoint(correctedClothPositionWS); // local space

                predictedPositions[i] = correctedClothPositionLS;
            }
        }
    }
}