#nullable enable
using UnityEngine;

namespace Perun.Client.Units;

internal static class Formation
{
    private const float GoldenAngle = 137.5f;
    private const float WedgeWidth = 0.75f;

    public static Vector3 Offset(int index, float spacing)
    {
        if (index <= 0)
        {
            return Vector3.zero;
        }
        float angle = index * GoldenAngle * Mathf.Deg2Rad;
        float radius = spacing * Mathf.Sqrt(index);
        return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
    }

    public static Vector3 Heading(float yaw) => new(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad));

    // Index 0 is the point of the wedge; the others pair up behind it, left then right, one row per pair.
    public static Vector3 Wedge(int index, float spacing, Vector3 heading)
    {
        if (index <= 0)
        {
            return Vector3.zero;
        }
        int row = (index + 1) / 2;
        float side = index % 2 == 1 ? -1f : 1f;
        Vector3 right = new(heading.z, 0f, -heading.x);
        return heading * (-spacing * row) + right * (side * spacing * row * WedgeWidth);
    }
}
