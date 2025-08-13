/// <summary>
/// Special "no state" type for simple workflows.
/// </summary>
public class NoState
{
    public static NoState Instance { get; } = new();
}
