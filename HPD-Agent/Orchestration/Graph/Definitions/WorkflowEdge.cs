/// <summary>
/// An edge in the workflow graph defining a conditional transition between nodes.
/// </summary>
public record WorkflowEdge(
    string FromNodeId,
    string ToNodeId,
    string Condition = "true"
);
