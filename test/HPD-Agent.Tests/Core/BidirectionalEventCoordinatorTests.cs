using HPD.Agent;
using Xunit;

namespace HPD_Agent.Tests.Core;

/// <summary>
/// Tests for BidirectionalEventCoordinator cycle detection and event bubbling.
/// </summary>
public class BidirectionalEventCoordinatorTests
{
    [Fact]
    public void SetParent_WithNullParent_ThrowsArgumentNullException()
    {
        // Arrange
        var coordinator = new BidirectionalEventCoordinator();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => coordinator.SetParent(null!));
    }

    [Fact]
    public void SetParent_WithSelfReference_ThrowsInvalidOperationException()
    {
        // Arrange
        var coordinator = new BidirectionalEventCoordinator();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => coordinator.SetParent(coordinator));
        Assert.Contains("Cannot set coordinator as its own parent", ex.Message);
        Assert.Contains("infinite loop", ex.Message);
    }

    [Fact]
    public void SetParent_WithTwoNodeCycle_ThrowsInvalidOperationException()
    {
        // Arrange
        var coordinatorA = new BidirectionalEventCoordinator();
        var coordinatorB = new BidirectionalEventCoordinator();

        coordinatorA.SetParent(coordinatorB);

        // Act & Assert
        // Trying to set B's parent to A would create cycle: A -> B -> A
        var ex = Assert.Throws<InvalidOperationException>(() => coordinatorB.SetParent(coordinatorA));
        Assert.Contains("Cycle detected", ex.Message);
        Assert.Contains("infinite loop", ex.Message);
    }

    [Fact]
    public void SetParent_WithThreeNodeCycle_ThrowsInvalidOperationException()
    {
        // Arrange
        var coordinatorA = new BidirectionalEventCoordinator();
        var coordinatorB = new BidirectionalEventCoordinator();
        var coordinatorC = new BidirectionalEventCoordinator();

        coordinatorA.SetParent(coordinatorB);
        coordinatorB.SetParent(coordinatorC);

        // Act & Assert
        // Trying to set C's parent to A would create cycle: A -> B -> C -> A
        var ex = Assert.Throws<InvalidOperationException>(() => coordinatorC.SetParent(coordinatorA));
        Assert.Contains("Cycle detected", ex.Message);
    }

    [Fact]
    public void SetParent_WithValidChain_Succeeds()
    {
        // Arrange
        var root = new BidirectionalEventCoordinator();
        var middle = new BidirectionalEventCoordinator();
        var leaf = new BidirectionalEventCoordinator();

        // Act
        middle.SetParent(root);
        leaf.SetParent(middle);

        // Assert
        // If we got here without exception, the chain is valid
        Assert.True(true);
    }

    [Fact]
    public void SetParent_CanChangeParent_WhenNoCycleCreated()
    {
        // Arrange
        var root1 = new BidirectionalEventCoordinator();
        var root2 = new BidirectionalEventCoordinator();
        var child = new BidirectionalEventCoordinator();

        // Act
        child.SetParent(root1);  // First parent
        child.SetParent(root2);  // Change to different parent

        // Assert
        // If we got here without exception, changing parent worked
        Assert.True(true);
    }

    [Fact]
    public void SetParent_WithComplexChain_DetectsCycleCorrectly()
    {
        // Arrange
        // Create chain: A -> B -> C -> D
        var coordinatorA = new BidirectionalEventCoordinator();
        var coordinatorB = new BidirectionalEventCoordinator();
        var coordinatorC = new BidirectionalEventCoordinator();
        var coordinatorD = new BidirectionalEventCoordinator();

        coordinatorA.SetParent(coordinatorB);
        coordinatorB.SetParent(coordinatorC);
        coordinatorC.SetParent(coordinatorD);

        // Act & Assert
        // Trying to set D's parent to B would create cycle: B -> C -> D -> B
        var ex = Assert.Throws<InvalidOperationException>(() => coordinatorD.SetParent(coordinatorB));
        Assert.Contains("Cycle detected", ex.Message);
    }
}
