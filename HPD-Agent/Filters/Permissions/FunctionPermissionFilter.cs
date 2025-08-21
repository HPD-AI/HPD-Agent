using System;
using System.Threading.Tasks;


    /// <summary>
    /// Filter that detects function permission requirements and delegates to handlers.
    /// </summary>
    public class FunctionPermissionFilter : IAiFunctionFilter
    {
        private readonly IPermissionHandler _permissionHandler;
        private readonly IPermissionStorage _permissionStorage;

        public FunctionPermissionFilter(IPermissionHandler permissionHandler, IPermissionStorage permissionStorage)
        {
            _permissionHandler = permissionHandler ?? throw new ArgumentNullException(nameof(permissionHandler));
            _permissionStorage = permissionStorage ?? throw new ArgumentNullException(nameof(permissionStorage));
        }

        public async Task InvokeAsync(AiFunctionContext context, Func<AiFunctionContext, Task> next)
        {
            
            // 1. Check if the function requires permission from its metadata.
            // We'll need to update HPDAIFunction to expose this. For now, we assume it exists.
            if (context.Function is not HPDAIFunctionFactory.HPDAIFunction hpdFunction || !hpdFunction.HPDOptions.RequiresPermission)
            {
                await next(context);
                return;
            }
            

            var functionName = context.ToolCallRequest.FunctionName;
            var conversationId = context.Conversation.Id;
            context.Conversation.Metadata.TryGetValue("Project", out var projectObj);
            var projectId = (projectObj as Project)?.Id;

            // 2. Check for a stored preference.
            var storedChoice = await _permissionStorage.GetStoredPermissionAsync(functionName, conversationId, projectId);

            if (storedChoice == PermissionChoice.AlwaysAllow)
            {
                await next(context);
                return;
            }

            if (storedChoice == PermissionChoice.AlwaysDeny)
            {
                context.Result = $"Execution of '{functionName}' was denied by a stored user preference.";
                context.IsTerminated = true;
                return;
            }

            // 3. No stored preference, so delegate to the application's handler.
            var request = new FunctionPermissionRequest
            {
                FunctionName = functionName,
                FunctionDescription = context.Function.Description,
                Arguments = context.ToolCallRequest.Arguments,
                ConversationId = conversationId,
                ProjectId = projectId
            };

            var decision = await _permissionHandler.RequestFunctionPermissionAsync(request);

            // 4. If the user wants to remember this choice, store it.
            if (decision.Storage != null)
            {
                await _permissionStorage.SavePermissionAsync(
                    functionName, 
                    decision.Storage.Choice, 
                    decision.Storage.Scope, 
                    conversationId, 
                    projectId);
            }

            // 5. Apply the user's decision.
            if (decision.Approved)
            {
                await next(context);
            }
            else
            {
                context.Result = $"Execution of '{functionName}' was denied by the user.";
                context.IsTerminated = true;
            }
        }
    }

