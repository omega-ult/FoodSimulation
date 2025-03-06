using UnityEngine;

public static class SDFOperator
{
    public static float Union(float d1, float d2)
    {
        return Mathf.Min(d1, d2);
    }

    public static float Intersection(float d1, float d2)
    {
        return Mathf.Max(d1, d2);
    }

    public static float Subtraction(float d1, float d2)
    {
        return Mathf.Max(d1, -d2);
    }

    public static float SmoothUnion(float d1, float d2, float k)
    {
        float h = Mathf.Clamp01(0.5f + 0.5f * (d2 - d1) / k);
        return Mathf.Lerp(d2, d1, h) - k * h * (1f - h);
    }
}