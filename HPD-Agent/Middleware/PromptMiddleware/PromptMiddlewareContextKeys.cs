

/// <summary>
/// Well-known context keys available in PromptMiddlewareContext.Properties and PostInvokeContext.Properties.
/// These provide discoverable, strongly-typed access to context information.
/// </summary>
public static class PromptMiddlewareContextKeys
{
    /// <summary>
    /// Key for accessing the conversation thread ID.
    /// Type: string?
    /// Available for all conversations.
    /// </summary>
    public const string ConversationId = "ConversationId";

    /// <summary>
    /// Key for accessing the current run ID.
    /// Type: string?
    /// Available during agent runs.
    /// </summary>
    public const string RunId = "RunId";

    /// <summary>
    /// Key for accessing the conversation thread instance.
    /// Type: ConversationThread?
    /// Available when thread context is present.
    /// </summary>
    public const string Thread = "Thread";

    /// <summary>
    /// Key for expanded skill containers (set by AgentCore before filter execution).
    /// Type: ImmutableHashSet&lt;string&gt;
    /// Available during skill activation turns.
    /// </summary>
    public const string ExpandedSkills = "ExpandedSkills";

    /// <summary>
    /// Key for skill instructions map (accumulated during turn).
    /// Type: ImmutableDictionary&lt;string, string&gt;
    /// Maps skill name → full instruction text from container metadata.
    /// Available during skill activation turns.
    /// </summary>
    public const string SkillInstructions = "SkillInstructions";
}