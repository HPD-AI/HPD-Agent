using System;
using System.Threading.Tasks;

    /// <summary>
    /// Manages continuation permissions by checking thresholds and delegating to handlers.
    /// </summary>
    public class ContinuationPermissionManager
    {
        private readonly IPermissionHandler _permissionHandler;
        private readonly IPermissionStorage _permissionStorage;
        private readonly ContinuationOptions _options;

        public ContinuationPermissionManager(IPermissionHandler permissionHandler, IPermissionStorage permissionStorage, ContinuationOptions options)
        {
            _permissionHandler = permissionHandler ?? throw new ArgumentNullException(nameof(permissionHandler));
            _permissionStorage = permissionStorage ?? throw new ArgumentNullException(nameof(permissionStorage));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<ContinuationDecision> ShouldContinueAsync(
            int currentIteration,
            int maxIterations,
            string[] completedFunctions,
            string[] plannedFunctions,
            string conversationId,
            string? projectId)
        {
            // 1. Don't ask if we are below the configured threshold.
            if (currentIteration < _options.DefaultThreshold)
            {
                return new ContinuationDecision { ShouldContinue = true };
            }

            // 2. Check for stored preferences.
            var storedPreference = await _permissionStorage.GetContinuationPreferenceAsync(conversationId, projectId);
                
            if (storedPreference?.AutoApprove == true)
            {
                return new ContinuationDecision { ShouldContinue = true };
            }
            
            if (storedPreference?.HardLimit.HasValue == true && currentIteration >= storedPreference.HardLimit.Value)
            {
                return new ContinuationDecision 
                { 
                    ShouldContinue = false, 
                    Reason = "Continuation stopped by a previously set hard limit." 
                };
            }

            // 3. Delegate to the application handler to ask the user.
            var request = new ContinuationPermissionRequest
            {
                CurrentIteration = currentIteration,
                MaxIterations = maxIterations,
                CompletedFunctions = completedFunctions,
                PlannedFunctions = plannedFunctions,
                ConversationId = conversationId,
                ProjectId = projectId
            };

            var decision = await _permissionHandler.RequestContinuationPermissionAsync(request);

            // 4. Store the preference if the user requested it.
            if (decision.Storage != null)
            {
                await _permissionStorage.SaveContinuationPreferenceAsync(decision.Storage, conversationId, projectId);
            }

            return decision;
        }
    }

    /// <summary>
    /// Configuration options for continuation permissions.
    /// </summary>
    public class ContinuationOptions
    {
        /// <summary>
        /// The number of function-calling iterations to allow before prompting for permission.
        /// </summary>
        public int DefaultThreshold { get; set; } = 3;
    }
