using System;
using System.Collections.Generic;
using System.Threading.Tasks;


    /// <summary>
    /// Interface that consuming applications implement to handle permission requests.
    /// The library delegates all permission decisions to implementations of this interface.
    /// </summary>
    public interface IPermissionHandler
    {
        /// <summary>
        /// Requests permission for a function execution.
        /// </summary>
        Task<PermissionDecision> RequestFunctionPermissionAsync(FunctionPermissionRequest request);

        /// <summary>
        /// Requests permission to continue function calling beyond configured limits.
        /// </summary>
        Task<ContinuationDecision> RequestContinuationPermissionAsync(ContinuationPermissionRequest request);
    }

    // --- Request Models ---

    /// <summary>
    /// Contains all information about a function permission request.
    /// </summary>
    public class FunctionPermissionRequest
    {
        public string FunctionName { get; set; } = string.Empty;
        public string FunctionDescription { get; set; } = string.Empty;
        public IDictionary<string, object?> Arguments { get; set; } = new Dictionary<string, object?>();
        public string ConversationId { get; set; } = string.Empty;
        public string? ProjectId { get; set; }
    }

    /// <summary>
    /// Contains information about a continuation permission request.
    /// </summary>
    public class ContinuationPermissionRequest
    {
        public int CurrentIteration { get; set; }
        public int MaxIterations { get; set; }
        public string[] CompletedFunctions { get; set; } = Array.Empty<string>();
        public string[] PlannedFunctions { get; set; } = Array.Empty<string>();
        public string ConversationId { get; set; } = string.Empty;
        public string? ProjectId { get; set; }
    }

    // --- Decision & Storage Models ---

    /// <summary>
    /// Represents the application's decision about a function permission request.
    /// </summary>
    public class PermissionDecision
    {
        public bool Approved { get; set; }
        public PermissionStorage? Storage { get; set; }
    }

    /// <summary>
    /// Represents the application's decision about a continuation permission request.
    /// </summary>
    public class ContinuationDecision
    {
        public bool ShouldContinue { get; set; }
        public string? Reason { get; set; }
        public ContinuationStorage? Storage { get; set; }
    }

    /// <summary>
    /// Optional preference storage request from the application.
    /// </summary>
    public class PermissionStorage
    {
        public PermissionChoice Choice { get; set; }
        public PermissionScope Scope { get; set; }
    }

    public enum PermissionChoice
    {
        AlwaysAllow,
        AlwaysDeny,
        Ask
    }

    public enum PermissionScope
    {
        Conversation,
        Project,
        Global
    }

    // --- Continuation Storage Models ---

    /// <summary>
    /// Stores the user's preference for handling future continuation requests.
    /// </summary>
    public class ContinuationPreference
    {
        /// <summary>
        /// Automatically approve continuations up to this many iterations.
        /// </summary>
        public int MaxAutoIterations { get; set; } = 5;

        /// <summary>
        /// If true, always continue without asking again within the given scope.
        /// </summary>
        public bool AutoApprove { get; set; } = false;
        
        /// <summary>
        /// An optional hard limit on the number of iterations to allow.
        /// </summary>
        public int? HardLimit { get; set; }
    }

    /// <summary>
    /// The storage request object for a continuation decision.
    /// </summary>
    public class ContinuationStorage
    {
        public ContinuationPreference Preference { get; set; } = new();
        public PermissionScope Scope { get; set; }
    }

