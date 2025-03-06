using UnityEngine;

public class SDFSphere : SDFPrimitive
{
    public float radius = 1f;

    public override float GetDistance(Vector3 point)
    {
        return Vector3.Distance(transform.position, point) - radius;
    }
}