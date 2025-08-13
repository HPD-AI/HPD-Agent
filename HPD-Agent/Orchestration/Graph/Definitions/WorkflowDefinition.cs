/// <summary>
/// Simple, declarative workflow definition. Serializable to/from JSON/YAML.
/// </summary>
public record WorkflowDefinition(
    string Name,
    string StartNodeId,
    IReadOnlyList<WorkflowNode> Nodes,
    IReadOnlyList<WorkflowEdge> Edges
);
