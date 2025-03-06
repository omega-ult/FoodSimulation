using UnityEngine;

public abstract class SDFPrimitive : MonoBehaviour, ISDF
{
    public abstract float GetDistance(Vector3 point);
    
    public virtual Vector3 GetNormal(Vector3 point)
    {
        float epsilon = 0.001f;
        Vector3 normal = new Vector3(
            GetDistance(point + new Vector3(epsilon, 0, 0)) - GetDistance(point - new Vector3(epsilon, 0, 0)),
            GetDistance(point + new Vector3(0, epsilon, 0)) - GetDistance(point - new Vector3(0, epsilon, 0)),
            GetDistance(point + new Vector3(0, 0, epsilon)) - GetDistance(point - new Vector3(0, 0, epsilon))
        );
        return normal.normalized;
    }
}