/// <summary>
/// Fluent builder that makes simple things simple, complex things possible.
/// </summary>
public class WorkflowBuilder
{
    private readonly List<WorkflowNode> _nodes = new();
    private readonly List<WorkflowEdge> _edges = new();
    private string _name;
    private string _startNodeId = string.Empty;

    private WorkflowBuilder(string name)
    {
        _name = name;
    }

    /// <summary>
    /// Create a new builder with the given workflow name.
    /// </summary>
    public static WorkflowBuilder Create(string name = "Workflow")
        => new WorkflowBuilder(name);

    /// <summary>
    /// Define the start node and add it to the graph.
    /// </summary>
    public WorkflowBuilder StartWith(string nodeId, string agentName)
    {
        _startNodeId = nodeId;
        return AddNode(nodeId, agentName);
    }

    /// <summary>
    /// Add a node to the workflow.
    /// </summary>
    public WorkflowBuilder AddNode(string id, string agentName, IReadOnlyDictionary<string, string>? inputMappings = null)
    {
        _nodes.Add(new WorkflowNode(id, agentName, inputMappings));
        return this;
    }

    /// <summary>
    /// Add a transition edge between two nodes with an optional condition.
    /// </summary>
    public WorkflowBuilder Then(string fromNodeId, string toNodeId, string condition = "true")
    {
        _edges.Add(new WorkflowEdge(fromNodeId, toNodeId, condition));
        return this;
    }

    /// <summary>
    /// Add a conditional edge (alias for Then).
    /// </summary>
    public WorkflowBuilder ThenIf(string fromNodeId, string toNodeId, string condition)
        => Then(fromNodeId, toNodeId, condition);

    /// <summary>
    /// Add a looping edge with a while condition.
    /// </summary>
    public WorkflowBuilder Loop(string fromNodeId, string toNodeId, string whileCondition)
        => Then(fromNodeId, toNodeId, whileCondition);

    /// <summary>
    /// Build the immutable workflow definition.
    /// </summary>
    public WorkflowDefinition Build()
    {
        if (string.IsNullOrEmpty(_startNodeId))
            throw new InvalidOperationException("Workflow must have a start node. Use StartWith() method.");
        return new WorkflowDefinition(_name, _startNodeId, _nodes, _edges);
    }
}
