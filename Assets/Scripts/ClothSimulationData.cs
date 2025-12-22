using UnityEngine;

public class ClothSimulationData
{
    public class ClothPoint
    {
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 force;
        public bool isFixed;
        public float mass;

        public ClothPoint(Vector3 pos, bool fixedPoint = false, float massValue = 0.3f)
        {
            position = pos;
            velocity = Vector3.zero;
            force = Vector3.zero;
            isFixed = fixedPoint;
            mass = massValue;
        }
    }

    public class Spring
    {
        public int indexA, indexB;
        public float restLength;
        public float stiffness;
        public float damping;

        public Spring(int a, int b, float restLen, float k, float d = 2.8f)
        {
            indexA = a;
            indexB = b;
            restLength = restLen;
            stiffness = k;
            damping = d;
        }
    }
}
