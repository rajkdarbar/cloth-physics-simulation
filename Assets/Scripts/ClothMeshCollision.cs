using UnityEngine;
using System.Collections.Generic;

public class ClothMeshCollision
{
    private ClothCollisionManager manager;

    public ClothMeshCollision(ClothCollisionManager manager)
    {
        this.manager = manager;
    }

    public void Resolve(List<Vector3> predictedPositions, List<ClothSimulationData.ClothPoint> clothPoints, List<Vector3> previousPositions, float dt)
    {
        if (manager._3DMesh == null || manager.simpleBVH == null) return;

        MeshFilter mf = manager._3DMesh.GetComponentInChildren<MeshFilter>();
        if (mf == null) return;

        Bounds meshBounds = mf.sharedMesh.bounds;
        meshBounds = manager.TransformBoundsToWorld(meshBounds, manager._3DMesh.transform);
        meshBounds.Expand(manager.meshBoundingBoxMargin);

        manager.activeIndices.Clear();
        manager.activeClothPointsWS.Clear();

        for (int i = 0; i < clothPoints.Count; i++)
        {
            if (clothPoints[i].isFixed) continue;

            Vector3 clothPointWS = manager.transform.TransformPoint(predictedPositions[i]);

            if (meshBounds.Contains(clothPointWS))
            {
                manager.activeIndices.Add(i);
                manager.activeClothPointsWS.Add(clothPointWS);
            }
        }

        if (manager.activeClothPointsWS.Count == 0) return; // no work to do

        Vector3[] clothCollisionPoints = manager.activeClothPointsWS.ToArray();
        manager.contactNormals = new Vector3[manager.activeClothPointsWS.Count];

        RunClothCollisionGPU(clothCollisionPoints, manager.contactNormals); // sending to GPU  

        // Compute tangential component for friction
        float staticFrictionThreshold = 2.5f;

        for (int i = 0; i < manager.activeIndices.Count; i++)
        {
            int clothIndex = manager.activeIndices[i];

            if (manager.stickClothToMesh)
            {
                clothPoints[clothIndex].isFixed = true;

                Vector3 pos = manager.transform.InverseTransformPoint(clothCollisionPoints[i]);
                predictedPositions[clothIndex] = pos;
                continue;
            }

            // Positions (world space)
            Vector3 correctedPositionWS = clothCollisionPoints[i]; // from GPU            

            // Velocity (world space)
            Vector3 predictedPositionWS = manager.transform.TransformPoint(predictedPositions[clothIndex]);
            Vector3 previousPositionWS = manager.transform.TransformPoint(previousPositions[clothIndex]);
            Vector3 velocityWS = (predictedPositionWS - previousPositionWS) / dt;

            // Contact normal from the closest triangle
            Vector3 contactNormalWS = manager.contactNormals[i]; // from GPU
            if (contactNormalWS.sqrMagnitude < 1e-8f) continue;
            contactNormalWS.Normalize();

            // Decompose velocity
            Vector3 normalVelocityWS = Vector3.Dot(velocityWS, contactNormalWS) * contactNormalWS;
            Vector3 tangentialVelocityWS = velocityWS - normalVelocityWS;

            Vector3 finalPositionLS = manager.transform.InverseTransformPoint(correctedPositionWS);
            predictedPositions[clothIndex] = finalPositionLS;

            // NOTE:
            // I intentionally do NOT use normal/tangential velocity-based friction here.
            // In a PBD cloth solver, motion is driven by positional distance constraints (springs),
            // so velocity-space friction cannot reliably prevent tangential sliding.
            // Collision is therefore enforced purely as a positional projection
            // (predicted = previous = correctedPosition).
            // The normal/tangential decomposition is kept as a reference for how friction
            // would be handled in impulse-based or rigid-body solvers.

            /*
            if (tangentialVelocityWS.magnitude < staticFrictionThreshold)
            {
                // Stick: no tangential motion
                Vector3 finalPositionLS = transform.InverseTransformPoint(correctedPositionWS);
                predictedPositions[clothIndex] = finalPositionLS;                
            }
            else
            {
                // Slide: tangential motion
                Vector3 vt = tangentialVelocityWS * (1f - frictionCoefficient);
                Vector3 finalPositionWS = correctedPositionWS + vt * dt;

                Vector3 finalPositionLS = transform.InverseTransformPoint(finalPositionWS);
                predictedPositions[clothIndex] = finalPositionLS;                
            }

            */
        }
    }

    void RunClothCollisionGPU(Vector3[] clothCollisionPoints, Vector3[] contactNormals)
    {
        var Nodes = manager.bvhFlattener.GetNodes();
        var Triangles = manager.bvhFlattener.GetTriangles();
        int totalPoints = clothCollisionPoints.Length;

        ComputeBuffer nodeBuffer = new ComputeBuffer(Nodes.Count, sizeof(float) * 6 + sizeof(int) * 4);
        ComputeBuffer triangleBuffer = new ComputeBuffer(Triangles.Count, sizeof(float) * 9);
        ComputeBuffer clothPointBuffer = new ComputeBuffer(totalPoints, sizeof(float) * 3);
        ComputeBuffer normalBuffer = new ComputeBuffer(totalPoints, sizeof(float) * 3);

        nodeBuffer.SetData(Nodes);
        triangleBuffer.SetData(Triangles);
        clothPointBuffer.SetData(clothCollisionPoints);

        int kernel = manager.clothCollisionShader.FindKernel("CSMain");
        manager.clothCollisionShader.SetBuffer(kernel, "bvhNodes", nodeBuffer);
        manager.clothCollisionShader.SetBuffer(kernel, "triangles", triangleBuffer);
        manager.clothCollisionShader.SetBuffer(kernel, "clothPoints", clothPointBuffer);
        manager.clothCollisionShader.SetBuffer(kernel, "outNormals", normalBuffer);

        manager.clothCollisionShader.SetInt("numTriangles", Triangles.Count);
        manager.clothCollisionShader.SetInt("numBVHNodes", Nodes.Count);
        manager.clothCollisionShader.SetInt("numClothPoints", totalPoints);

        int threadGroupSize = 256;
        int threadGroups = Mathf.CeilToInt((float)totalPoints / threadGroupSize);
        manager.clothCollisionShader.Dispatch(kernel, threadGroups, 1, 1);

        clothPointBuffer.GetData(clothCollisionPoints);
        normalBuffer.GetData(contactNormals);

        nodeBuffer.Release();
        triangleBuffer.Release();
        clothPointBuffer.Release();
        normalBuffer.Release();
    }
}
