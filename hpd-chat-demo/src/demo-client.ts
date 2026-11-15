import type { AgentClient } from '@hpd-research/hpd-agent-client';

type AgentEvent = {
  type: string;
  version: '1.0';
  sessionId: string;
  threadId: string;
  timestamp: string;
  threadSequenceNumber: number;
  threadExecutionId?: string;
  [key: string]: unknown;
};

type EventDelivery = { event: AgentEvent };
type EventSubscriber = (delivery: EventDelivery) => void | Promise<void>;

const SESSION_ID = 'demo-session';
const THREAD_ID = 'main';
const AGENT_ID = 'hpd-demo';

const sleep = (milliseconds: number) =>
  new Promise<void>((resolve) => globalThis.setTimeout(resolve, milliseconds));

export interface DemoAgentClient {
  client: AgentClient;
  requestApproval(): Promise<void>;
  requestCodingDemo(): Promise<void>;
}

export function createDemoAgentClient(): DemoAgentClient {
  let subscriber: EventSubscriber | undefined;
  let finishObservation: (() => void) | undefined;
  let closed = false;
  let sequence = 0;
  let runId = 0;
  let turnId = 0;
  let activeExecutionId: string | undefined;
  let pendingApprovalTurn: string | undefined;

  const event = (type: string, input: Record<string, unknown> = {}): AgentEvent => ({
    version: '1.0',
    type,
    sessionId: SESSION_ID,
    threadId: THREAD_ID,
    timestamp: new Date().toISOString(),
    threadSequenceNumber: ++sequence,
    ...(activeExecutionId ? { threadExecutionId: activeExecutionId } : {}),
    ...input,
  });

  const emit = (type: string, input: Record<string, unknown> = {}): void => {
    if (!closed && subscriber) {
      subscriber({ event: event(type, input) });
    }
  };

  const streamText = async (
    messageId: string,
    text: string,
    role: 'user' | 'assistant',
  ): Promise<void> => {
    const assistant = role === 'assistant';
    emit('TEXT_MESSAGE_START', {
      messageId,
      role,
      source: assistant ? 'AssistantOutput' : 'UserInput',
      visibility: 'Transcript',
      persistence: 'ThreadHistory',
      authorName: assistant ? 'HPD Agent' : 'You',
    });

    const chunks = text.match(/.{1,22}(?:\s+|$)/gs) ?? [text];
    for (const chunk of chunks) {
      await sleep(assistant ? 28 : 12);
      emit('TEXT_DELTA', { messageId, text: chunk });
    }
    emit('TEXT_MESSAGE_END', { messageId });
  };

  const streamReasoning = async (messageId: string, text: string): Promise<void> => {
    emit('REASONING_MESSAGE_START', { messageId });
    const chunks = text.match(/.{1,30}(?:\s+|$)/gs) ?? [text];
    for (const chunk of chunks) {
      await sleep(24);
      emit('REASONING_DELTA', { messageId, text: chunk });
    }
    emit('REASONING_MESSAGE_END', { messageId });
  };

  const runTool = async (name: string, detail: string, result: string): Promise<void> => {
    const callId = `demo-tool-${++sequence}`;
    emit('TOOL_CALL_START', {
      callId,
      name,
      messageId: `tool-message-${callId}`,
      toolHarnessName: 'context',
      callType: 'inspection',
    });
    await sleep(180);
    emit('TOOL_CALL_ARGS', {
      callId,
      argsJson: JSON.stringify({ query: detail }),
    });
    await sleep(360);
    emit('TOOL_CALL_RESULT', {
      callId,
      result: { text: result },
    });
    emit('TOOL_CALL_END', { callId });
  };

  const answerFor = (prompt: string): { reasoning: string; answer: string } => {
    const normalized = prompt.toLowerCase();

    if (normalized.includes('headless') || normalized.includes('opinion')) {
      return {
        reasoning:
          'The design question is whether the abstraction removes protocol complexity without taking ownership of product decisions. I am checking the projection boundary, presentation escape hatches, and lifecycle costs.',
        answer:
          'My read: the headless core is worth it, but the full UI shell should remain optional.\n\nThe useful part is the semantic lifecycle. It separates final transcript output from live work, keeps tool and reasoning state inspectable, and treats approvals and edits as structured state instead of ad-hoc event handling. That is real complexity most chat UIs would otherwise duplicate.\n\nThe opinionated part is the timeline/work-group model. That is acceptable because it follows the agent domain. The visual shell is the part I would keep replaceable. Use the controller and selectors, then build your own React or vanilla presentation layer.',
      };
    }

    if (normalized.includes('plan') || normalized.includes('launch')) {
      return {
        reasoning:
          'I am separating product risk from implementation risk, then ordering the work so visual polish does not hide protocol or recovery gaps.',
        answer:
          'A sensible launch path is:\n\n1. Prove the connection and replay path against a real HPD host.\n2. Exercise one complete execution with reasoning, a tool call, and a final answer.\n3. Add a permission request and verify the composer cannot bypass it.\n4. Test disconnect and reconnect while work is still active.\n5. Measure the Svelte shell bundle separately from the headless core.\n6. Only then decide whether the full shell or a custom design-system adapter is the better long-term fit.\n\nThe biggest risk is not opinionation. It is coupling product UI directly to an early 0.1 API before the real-server lifecycle has been exercised.',
      };
    }

    return {
      reasoning:
        'I am checking the requested outcome, the useful context available in this demo thread, and the smallest implementation path that leaves room for a real HPD backend.',
      answer:
        `Here is how I would approach “${prompt}”.\n\nStart with the user-visible outcome, then work backward into thread state and events. Keep the headless projection as the source of truth: render the transcript for settled answers, work groups for active execution, and runtime requests as blocking controls.\n\nThis demo is taking that route. The layout is custom Tailwind, but the lifecycle comes from the real HPD Agent client and headless UI controller. When a server is available, the same UI can switch from the simulation to a live AgentClient connection.`,
    };
  };

  const finishRun = async (executionId: string, currentTurnId: string): Promise<void> => {
    emit('MESSAGE_TURN_FINISHED', {
      messageTurnId: currentTurnId,
      conversationId: `conversation-${executionId}`,
      usage: {
        operations: [{
          usage: {
            inputTokenCount: 842,
            outputTokenCount: 326,
            totalTokenCount: 1168,
            cachedInputTokenCount: 128,
            reasoningTokenCount: 96,
            additionalCounts: null,
          },
        }],
      },
    });
    emit('THREAD_EXECUTION_FINISHED', {
      threadExecutionId: executionId,
      agentId: AGENT_ID,
      outcome: 'Succeeded',
      finishedAt: new Date().toISOString(),
    });
    activeExecutionId = undefined;
  };

  const answerPrompt = async (prompt: string): Promise<void> => {
    const executionId = `execution-${++runId}`;
    const currentTurnId = `turn-${++turnId}`;
    activeExecutionId = executionId;

    emit('THREAD_EXECUTION_STARTED', {
      threadExecutionId: executionId,
      agentId: AGENT_ID,
      startedAt: new Date().toISOString(),
    });
    emit('MESSAGE_TURN_STARTED', {
      messageTurnId: currentTurnId,
      conversationId: `conversation-${executionId}`,
      agentName: 'HPD Agent',
      eventFlowId: currentTurnId,
    });

    const response = answerFor(prompt);
    await streamReasoning(`reasoning-${currentTurnId}`, response.reasoning);
    await runTool(
      'thread_context.lookup',
      prompt.slice(0, 72),
      'Found the current thread scope, projection state, and one available runtime path.',
    );
    await streamText(`assistant-${currentTurnId}`, response.answer, 'assistant');
    await finishRun(executionId, currentTurnId);
  };

  const runIntro = async (): Promise<void> => {
    const currentTurnId = `turn-${++turnId}`;
    emit('MESSAGE_TURN_STARTED', {
      messageTurnId: currentTurnId,
      conversationId: 'conversation-intro',
      agentName: 'HPD Agent',
      eventFlowId: currentTurnId,
    });
    await streamText('intro-user', 'What are we testing in this interface?', 'user');
    await streamReasoning(
      'intro-reasoning',
      'I should show the distinction between settled transcript content and live agent work without making the interface feel like a protocol inspector.',
    );
    await runTool(
      'ui_capability.inspect',
      'headless timeline projection',
      'Timeline, work groups, runtime requests, active tools, and context usage are available.',
    );
    await streamText(
      'intro-assistant',
      'We are testing a thin product surface over the real headless lifecycle.\n\nThe chat stays readable, while reasoning, tool activity, approvals, streaming state, and usage remain inspectable. The Tailwind layer owns the visual identity; the HPD headless core owns the agent semantics.\n\nTry one of the prompts below, or press “Preview approval” to exercise a blocking permission request.',
      'assistant',
    );
    emit('MESSAGE_TURN_FINISHED', {
      messageTurnId: currentTurnId,
      conversationId: 'conversation-intro',
    });
  };

  const runCodingDemo = async (): Promise<void> => {
    const executionId = `execution-${++runId}`;
    const currentTurnId = `turn-${++turnId}`;
    const commandId = `cmd_demo_${++sequence}`;
    const commandCallId = `tool-command-${++sequence}`;
    const editCallId = `tool-edit-${++sequence}`;
    const filePath = 'src/session/project-thread.ts';
    activeExecutionId = executionId;

    emit('THREAD_EXECUTION_STARTED', {
      threadExecutionId: executionId,
      agentId: AGENT_ID,
      startedAt: new Date().toISOString(),
    });
    emit('MESSAGE_TURN_STARTED', {
      messageTurnId: currentTurnId,
      conversationId: `conversation-${executionId}`,
      agentName: 'Coding Agent',
      eventFlowId: currentTurnId,
    });

    await streamReasoning(
      `coding-reasoning-${currentTurnId}`,
      'I will exercise the coding-harness semantic events that sit outside the generated SDK union: a command lifecycle with output, a durable file mutation with a diff, and language-server diagnostics attached to the changed path.',
    );

    emit('TOOL_CALL_START', {
      callId: commandCallId,
      name: 'ExecuteCommand',
      messageId: `tool-message-${commandCallId}`,
      toolHarnessName: 'CodingToolHarness',
      callType: 'Command',
      eventFlowId: currentTurnId,
    });
    emit('TOOL_CALL_ARGS', {
      callId: commandCallId,
      argsJson: JSON.stringify({
        request: { action: 'run', command: 'npm test -- --run' },
      }),
      eventFlowId: currentTurnId,
    });
    emit('EXECUTE_COMMAND_PROCESS_STARTED', {
      toolCallId: commandCallId,
      functionName: 'ExecuteCommand',
      commandId,
      command: 'npm test -- --run',
      baseCommand: 'npm',
      category: 'Test',
      workingDirectory: '/workspace/hpd-agent',
      shell: '/bin/zsh',
      startedAt: new Date().toISOString(),
      background: false,
      autoBackgroundEligible: false,
      timeoutMilliseconds: 120000,
      eventFlowId: currentTurnId,
    });

    const output = [
      '> hpd-agent@0.9.5 test\n',
      '> vitest run\n\n',
      ' ✓ thread-projection.test.ts (24 tests)\n',
      ' ✓ thread-controller.test.ts (13 tests)\n',
      ' ✓ thread-extensions.test.ts (3 tests)\n',
      '\n Test Files  13 passed (13)\n',
      '      Tests  99 passed (99)\n',
    ];
    let observed = 0;
    for (const chunk of output) {
      await sleep(70);
      observed += chunk.length;
      emit('EXECUTE_COMMAND_OUTPUT_CHUNK', {
        toolCallId: commandCallId,
        functionName: 'ExecuteCommand',
        commandId,
        command: 'npm test -- --run',
        baseCommand: 'npm',
        category: 'Test',
        workingDirectory: '/workspace/hpd-agent',
        stream: chunk.startsWith(' ') ? 'Stderr' : 'Stdout',
        text: chunk,
        observedAt: new Date().toISOString(),
        streamBytesObserved: observed,
        combinedBytesObserved: observed,
        truncated: false,
        suppressed: false,
        binary: false,
        eventFlowId: currentTurnId,
      });
      emit('EXECUTE_COMMAND_PROGRESS', {
        toolCallId: commandCallId,
        functionName: 'ExecuteCommand',
        commandId,
        command: 'npm test -- --run',
        baseCommand: 'npm',
        category: 'Test',
        workingDirectory: '/workspace/hpd-agent',
        elapsedMilliseconds: observed * 7,
        stdoutBytes: observed,
        stderrBytes: 0,
        combinedOutputBytes: observed,
        combinedBytesDiscarded: 0,
        outputObserved: true,
        outputEventsSuppressed: false,
        eventFlowId: currentTurnId,
      });
    }
    emit('EXECUTE_COMMAND_PROCESS_EXITED', {
      toolCallId: commandCallId,
      functionName: 'ExecuteCommand',
      commandId,
      command: 'npm test -- --run',
      baseCommand: 'npm',
      category: 'Test',
      workingDirectory: '/workspace/hpd-agent',
      exitCode: 0,
      completionKind: 'Completed',
      durationMilliseconds: 684,
      stdoutBytes: observed,
      stderrBytes: 0,
      combinedOutputBytes: observed,
      stdoutBytesDiscarded: 0,
      stderrBytesDiscarded: 0,
      combinedBytesDiscarded: 0,
      outputTruncated: false,
      outputDrainTimedOut: false,
      outputEventsSuppressed: false,
      maxPersistedOutputBytes: 65536,
      combinedOutputFormat: 'hpd.execute-command.interleaved.v1',
      eventFlowId: currentTurnId,
    });
    emit('TOOL_CALL_RESULT', {
      callId: commandCallId,
      name: 'ExecuteCommand',
      result: { text: '<execute_command status="completed" exit_code="0" command="npm test -- --run" />' },
      eventFlowId: currentTurnId,
    });
    emit('TOOL_CALL_END', {
      callId: commandCallId,
      messageId: `tool-message-${commandCallId}`,
      name: 'ExecuteCommand',
      argsJson: '{}',
      eventFlowId: currentTurnId,
    });

    emit('TOOL_CALL_START', {
      callId: editCallId,
      name: 'EditFile',
      messageId: `tool-message-${editCallId}`,
      toolHarnessName: 'CodingToolHarness',
      callType: 'FileMutation',
      eventFlowId: currentTurnId,
    });
    emit('TOOL_CALL_ARGS', {
      callId: editCallId,
      argsJson: JSON.stringify({ path: filePath, oldString: 'thread.observe(', newString: 'thread.observe({ extensions })' }),
      eventFlowId: currentTurnId,
    });
    await sleep(180);
    emit('FILE_EDIT_APPLIED', {
      toolCallId: editCallId,
      functionName: 'EditFile',
      path: `/workspace/hpd-agent/${filePath}`,
      displayPath: filePath,
      mutationKind: 'Changed',
      created: false,
      changed: true,
      editCount: 1,
      replacementCount: 1,
      diffStat: {
        addedLines: 3,
        removedLines: 1,
        addedChars: 112,
        removedChars: 28,
      },
      hunksTruncated: false,
      hunks: [{
        oldStart: 17,
        oldLines: 4,
        newStart: 17,
        newLines: 6,
        lines: [
          ' const observation = await thread.observe(',
          '-  onEvent: projectAgentEvent,',
          '+  extensions: [codingHarnessExtension],',
          '+  onEvent: projectAgentEvent,',
          '+  onUnknownEvent: projectApplicationEvent,',
          ' );',
        ],
      }],
      eventFlowId: currentTurnId,
    });
    emit('TOOL_CALL_RESULT', {
      callId: editCallId,
      name: 'EditFile',
      result: { text: 'Updated src/session/project-thread.ts (+3 -1)' },
      eventFlowId: currentTurnId,
    });
    emit('TOOL_CALL_END', {
      callId: editCallId,
      messageId: `tool-message-${editCallId}`,
      name: 'EditFile',
      argsJson: '{}',
      eventFlowId: currentTurnId,
    });

    await sleep(120);
    emit('LANGUAGE_SERVER_DIAGNOSTICS_RECEIVED', {
      path: filePath,
      uri: `file:///workspace/hpd-agent/${filePath}`,
      diagnosticSetCount: 1,
      errorCount: 0,
      warningCount: 1,
      informationCount: 0,
      hintCount: 0,
      diagnosticsTruncated: false,
      diagnostics: [{
        path: filePath,
        serverId: 'typescript',
        source: 'LanguageServer',
        severity: 'Warning',
        line: 20,
        character: 2,
        code: 'TS7006',
        message: 'Extension state is application-owned and should be validated before rendering.',
      }],
      eventFlowId: currentTurnId,
    });

    await streamText(
      `coding-assistant-${currentTurnId}`,
      'The coding preview projected the same categories as the terminal coding harness without changing the generated client SDK.\n\nCommand execution renders as a keyed lifecycle card with bounded output and exit state. The file edit renders as a compact diff, and the latest language-server diagnostics attach to that path. All three come from application event extensions rather than core transcript semantics.',
      'assistant',
    );
    await finishRun(executionId, currentTurnId);
  };

  const requestApproval = async (): Promise<void> => {
    const currentTurnId = `turn-${++turnId}`;
    pendingApprovalTurn = currentTurnId;
    activeExecutionId = `execution-${++runId}`;
    emit('THREAD_EXECUTION_STARTED', {
      threadExecutionId: activeExecutionId,
      agentId: AGENT_ID,
      startedAt: new Date().toISOString(),
    });
    emit('MESSAGE_TURN_STARTED', {
      messageTurnId: currentTurnId,
      conversationId: `conversation-${activeExecutionId}`,
      agentName: 'HPD Agent',
      eventFlowId: currentTurnId,
    });
    await streamReasoning(
      `approval-reasoning-${currentTurnId}`,
      'The next operation changes a production surface. The runtime should stop here and ask for an explicit allow or deny decision.',
    );
    emit('PERMISSION_REQUEST', {
      permissionDomain: 'ToolExecution',
      permissionId: `permission-${currentTurnId}`,
      sourceName: 'HPD.Agent.Permissions',
      functionName: 'deploy_preview',
      callId: `approval-tool-${currentTurnId}`,
      eventFlowId: currentTurnId,
      choices: [
        { id: 'allow_once', label: 'Allow once', kind: 'Allow' },
        { id: 'allow_session', label: 'Allow for session', kind: 'Allow' },
        { id: 'deny_once', label: 'Deny', kind: 'Deny' },
      ],
    });
  };

  const completeApproval = async (allowed: boolean): Promise<void> => {
    if (!pendingApprovalTurn || !activeExecutionId) return;
    const currentTurnId = pendingApprovalTurn;
    const executionId = activeExecutionId;
    pendingApprovalTurn = undefined;
    await sleep(140);
    if (allowed) {
      await runTool(
        'deploy.preview',
        'production preview environment',
        'Preview deployment completed successfully. No production resources were changed.',
      );
      await streamText(
        `approval-assistant-${currentTurnId}`,
        'Approved. The permission reached the runtime, the tool completed inside the existing execution, and the normal composer remained blocked until the request resolved.\n\nThis is the interaction the headless runtime-request model is meant to make explicit.',
        'assistant',
      );
    } else {
      emit('AGENT_REQUEST_TERMINATED', {
        requestId: `permission-${currentTurnId}`,
        reason: 'DeniedByUser',
      });
      await streamText(
        `approval-assistant-${currentTurnId}`,
        'The operation was denied. The execution can finish without pretending the tool succeeded, and the transcript records the refusal as a normal lifecycle outcome.',
        'assistant',
      );
    }
    await finishRun(executionId, currentTurnId);
  };

  const client = {
    sessions: {
      get: async (sessionId: string) => ({
        id: sessionId,
        threads: {
          get: async (threadId: string) => ({
            sessionId,
            id: threadId,
            observe: async (options: {
              onEvent: EventSubscriber;
              signal?: AbortSignal;
            }) => {
              closed = false;
              subscriber = options.onEvent;
              globalThis.setTimeout(() => void runIntro(), 160);
              const done = new Promise<void>((resolve) => {
                finishObservation = resolve;
              });
              options.signal?.addEventListener('abort', () => {
                closed = true;
                finishObservation?.();
              }, { once: true });
              return {
                done,
                close: async () => {
                  closed = true;
                  subscriber = undefined;
                  finishObservation?.();
                },
              };
            },
          }),
        },
      }),
    },
    api: {
      threads: {
        getThread: async ({ sessionId, threadId }: { sessionId: string; threadId: string }) => ({
          id: threadId,
          sessionId,
          defaultAgentId: AGENT_ID,
          name: 'HPD product studio',
          description: 'Interactive headless UI demo',
          createdAt: new Date().toISOString(),
          lastActivity: new Date().toISOString(),
          messageCount: 2,
          tags: ['demo', 'headless-ui'],
        }),
      },
      agentRuntime: {
        getAgentThreadRuntimeState: async () => ({
          observedCursor: { generation: 1, sequenceNumber: 0 },
          activeExecution: null,
          pendingRequests: [],
        }),
        submitAgentInput: async ({ input }: { input: Record<string, unknown> }) => {
          const messages = Array.isArray(input.messages) ? input.messages : [];
          const last = messages.at(-1) as {
            contents?: Array<{ $type?: string; text?: string }>;
          } | undefined;
          const prompt = (last?.contents ?? [])
            .filter((content) => content.$type === 'text')
            .map((content) => content.text ?? '')
            .join(' ')
            .trim() || 'Tell me what this interface is doing.';
          globalThis.setTimeout(() => void answerPrompt(prompt), 120);
          return {
            disposition: 'queued',
            threadExecutionId: `pending-execution-${runId + 1}`,
            activeExecution: null,
          };
        },
      },
      executions: {
        listThreadExecutions: async () => [],
        cancelThreadExecution: async ({ threadExecutionId }: { threadExecutionId: string }) => {
          activeExecutionId = undefined;
          emit('THREAD_EXECUTION_FINISHED', {
            threadExecutionId,
            agentId: AGENT_ID,
            outcome: 'Cancelled',
            finishedAt: new Date().toISOString(),
          });
          return {
            cancellationApplied: true,
            threadExecutionId,
          };
        },
      },
      agentInteractions: {
        respondToAgentRequest: async ({
          input,
        }: {
          input: Record<string, unknown>;
        }) => {
          if (input.type === 'PERMISSION_RESPONSE') {
            const choice = String(input.choiceId ?? '');
            emit('PERMISSION_RESPONSE', {
              permissionId: input.permissionId,
              agentId: AGENT_ID,
              domain: 'ToolExecution',
              decisionKind: choice.startsWith('allow') ? 'Allow' : 'Deny',
              decisionSource: 'UserDecision',
              eventFlowId: pendingApprovalTurn,
            });
            globalThis.setTimeout(() => void completeApproval(choice.startsWith('allow')), 180);
          }
          return {
            accepted: true,
            status: 'accepted',
            requestId: String(input.permissionId ?? input.requestId ?? ''),
            message: null,
          };
        },
      },
    },
  };

  return {
    client: client as unknown as AgentClient,
    requestApproval,
    requestCodingDemo: runCodingDemo,
  };
}
