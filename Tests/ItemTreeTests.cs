using System.Collections.Generic;
using System.Linq;
using Perun.Client.Arsenal;

namespace Perun.Tests;

public class ItemTreeTests
{
    private sealed record Node(string Id, string? Parent);

    private static List<List<Node>> Split(params Node[] nodes) => ItemTree.ChildSubtrees(nodes, node => node.Id, node => node.Parent);

    [Fact]
    public void EachDirectChildBecomesOneSubtreeWithItsDescendants()
    {
        List<List<Node>> subtrees = Split(
            new Node("case", null),
            new Node("mag", "case"),
            new Node("round1", "mag"),
            new Node("round2", "mag"),
            new Node("medkit", "case"));

        Assert.Equal(2, subtrees.Count);
        Assert.Equal(new[] { "mag", "round1", "round2" }, subtrees[0].Select(node => node.Id));
        Assert.Equal(new[] { "medkit" }, subtrees[1].Select(node => node.Id));
    }

    [Fact]
    public void AnEmptyContainerHasNoSubtrees() => Assert.Empty(Split(new Node("case", null)));

    [Fact]
    public void NothingGivesNothing() => Assert.Empty(ItemTree.ChildSubtrees<Node>(new List<Node>(), node => node.Id, node => node.Parent));

    [Fact]
    public void ItemsOfOtherParentsAreIgnored()
    {
        List<List<Node>> subtrees = Split(
            new Node("case", null),
            new Node("mag", "case"),
            new Node("stray", "elsewhere"));

        Assert.Single(subtrees);
        Assert.DoesNotContain(subtrees[0], node => node.Id == "stray");
    }

    [Fact]
    public void ACycleInBrokenDataDoesntLoopForever()
    {
        List<List<Node>> subtrees = Split(
            new Node("case", null),
            new Node("a", "case"),
            new Node("b", "a"),
            new Node("a", "b"));

        Assert.Single(subtrees);
        Assert.Equal(new[] { "a", "b" }, subtrees[0].Select(node => node.Id));
    }
}
