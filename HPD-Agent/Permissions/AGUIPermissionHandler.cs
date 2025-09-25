using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

    /// <summary>
    /// Interface for emitting permission events to an AGUI stream.
    /// This is implemented by the application's streaming infrastructure.
    /// </summary>
    public interface IPermissionEventEmitter
    {
        Task EmitAsync<T>(T eventData) where T : BaseEvent;
    }

    /// <summary>
    /// AGUI-based permission handler for continuation permissions only.
    /// Function-level permissions are now handled by AGUIPermissionFilter.
    /// </summary>
    public class AGUIPermissionHandler : IPermissionHandler
    {
        private readonly ConcurrentDictionary<string, TaskCompletionSource<ContinuationDecision>> _pendingContinuationPermissions = new();
        private readonly IPermissionEventEmitter _eventEmitter;

        public AGUIPermissionHandler(IPermissionEventEmitter eventEmitter)
        {
            _eventEmitter = eventEmitter ?? throw new ArgumentNullException(nameof(eventEmitter));
        }

        public async Task<ContinuationDecision> RequestContinuationPermissionAsync(ContinuationPermissionRequest request)
        {
            var permissionId = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<ContinuationDecision>();
            _pendingContinuationPermissions[permissionId] = tcs;

            var continuationEvent = new ContinuationPermissionRequestEvent
            {
                Type = "custom",
                PermissionId = permissionId,
                CurrentIteration = request.CurrentIteration,
                MaxIterations = request.MaxIterations,
                CompletedFunctions = request.CompletedFunctions.ToArray(),
                PlannedFunctions = request.PlannedFunctions.ToArray()
            };

            await _eventEmitter.EmitAsync(continuationEvent);

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            try
            {
                return await tcs.Task.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                _pendingContinuationPermissions.TryRemove(permissionId, out _);
                return new ContinuationDecision { ShouldContinue = false, Reason = "Permission request timed out." };
            }
        }

        /// <summary>
        /// Called by the application when it receives a continuation permission response from the frontend.
        /// </summary>
        public void HandlePermissionResponse(PermissionResponsePayload response)
        {
            if (response.Type == "continuation" &&
                _pendingContinuationPermissions.TryRemove(response.PermissionId, out var continuationTcs))
            {
                var decision = new ContinuationDecision
                {
                    ShouldContinue = response.Approved,
                    Reason = response.Approved ? null : "User chose to stop the operation."
                };
                continuationTcs.SetResult(decision);
            }
        }

    }