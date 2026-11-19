using Perun.Client.Units;
using UnityEngine;

namespace Perun.Tests;

public class FormationTests
{
    private const float Spacing = 1.6f;

    [Fact]
    public void LeaderStandsOnTheTarget() => Assert.Equal(Vector3.zero, Formation.Offset(0, Spacing));

    [Fact]
    public void OffsetsStayOnTheGround()
    {
        for (int i = 1; i < 20; i++)
        {
            Assert.Equal(0f, Formation.Offset(i, Spacing).y);
        }
    }

    [Fact]
    public void OffsetsMoveOutwardsWithTheSquareRootOfTheIndex()
    {
        for (int i = 1; i < 20; i++)
        {
            Assert.Equal(Spacing * Mathf.Sqrt(i), Formation.Offset(i, Spacing).magnitude, 3);
        }
    }

    [Fact]
    public void UnitsOfAFullGroupDontStandOnEachOther()
    {
        const int group = 10;
        for (int a = 0; a < group; a++)
        {
            for (int b = a + 1; b < group; b++)
            {
                float distance = (Formation.Offset(a, Spacing) - Formation.Offset(b, Spacing)).magnitude;
                Assert.True(distance > Spacing * 0.8f, $"units {a} and {b} are {distance:0.00} m apart");
            }
        }
    }
}
