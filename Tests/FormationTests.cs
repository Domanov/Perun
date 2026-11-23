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

    [Fact]
    public void HeadingFollowsTheYaw()
    {
        Assert.Equal(0f, Vector3.Distance(Vector3.forward, Formation.Heading(0f)), 3);
        Assert.Equal(0f, Vector3.Distance(Vector3.right, Formation.Heading(90f)), 3);
    }

    [Fact]
    public void WedgePairsStandBehindThePointLeftThenRight()
    {
        Vector3 heading = Formation.Heading(90f);
        Assert.Equal(Vector3.zero, Formation.Wedge(0, Spacing, heading));

        for (int i = 1; i < 12; i++)
        {
            Vector3 offset = Formation.Wedge(i, Spacing, heading);
            int row = (i + 1) / 2;
            Assert.Equal(-Spacing * row, Vector3.Dot(offset, heading), 3);
            float side = Vector3.Dot(offset, new Vector3(heading.z, 0f, -heading.x));
            Assert.True(i % 2 == 1 ? side < 0f : side > 0f, $"unit {i} is on the wrong side");
        }
    }

    [Fact]
    public void WedgeUnitsDontStandOnEachOther()
    {
        Vector3 heading = Formation.Heading(30f);
        for (int a = 0; a < 15; a++)
        {
            for (int b = a + 1; b < 15; b++)
            {
                float distance = (Formation.Wedge(a, Spacing, heading) - Formation.Wedge(b, Spacing, heading)).magnitude;
                Assert.True(distance > Spacing * 0.8f, $"units {a} and {b} are {distance:0.00} m apart");
            }
        }
    }
}
