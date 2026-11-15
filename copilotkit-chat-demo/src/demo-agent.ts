import {
  AbstractAgent,
  EventType,
  type BaseEvent,
  type RunAgentInput,
} from '@ag-ui/client';
import { Observable, ReplaySubject } from 'rxjs';

const sleep = (milliseconds: number) =>
  new Promise<void>((resolve) => globalThis.setTimeout(resolve, milliseconds));

function readLastUserText(input: RunAgentInput): string {
  for (let index = input.messages.length - 1; index >= 0; index -= 1) {
    const message = input.messages[index];
    if (message?.role !== 'user') continue;
    if (typeof message.content === 'string') return message.content;
    if (Array.isArray(message.content)) {
      return message.content
        .filter((part) => part.type === 'text')
        .map((part) => part.text ?? '')
        .join(' ');
    }
  }
  return 'What is the difference between AG-UI and CopilotKit?';
}

function answerFor(prompt: string): { reasoning: string; answer: string } {
  const normalized = prompt.toLowerCase();
  if (normalized.includes('ag-ui') || normalized.includes('copilotkit')) {
    return {
      reasoning:
        'The clean split is protocol versus product surface. AG-UI should define the event and state conversation between agent and application; CopilotKit should make that protocol practical in React.',
      answer:
        'AG-UI is the wire between the agent and the user-facing application. It defines streaming text, tool calls, state snapshots, lifecycle events, and human-in-the-loop interrupts.\n\nCopilotKit is the product layer above it. It supplies the React provider, agent registry, message state, prebuilt chat components, generative UI hooks, and reusable interaction behavior.\n\nSo they are not direct substitutes for HPD’s client and headless UI. AG-UI is closer to HPD’s event contract. CopilotKit is closer to the combination of HPD’s headless UI plus a mature component library.',
    };
  }

  if (normalized.includes('opinion')) {
    return {
      reasoning:
        'I am comparing visible customization with the amount of behavior hidden behind the prebuilt component.',
      answer:
        'CopilotKit is clearly more opinionated visually than a framework-neutral state core, but it is also faster to stand up.\n\nThe useful trade is that its opinions are replaceable: use the prebuilt chat first, then move to the headless `useAgent`, slots, or custom tool renderers when the product design needs more control. The cost is a larger dependency surface and a more framework-shaped API.',
    };
  }

  return {
    reasoning:
      'I should respond inside the AG-UI event lifecycle, stream the text, expose one tool call, and finish the run cleanly.',
    answer:
      `Here is my response to “${prompt}”.\n\nThis message was produced by a local agent extending AG-UI’s AbstractAgent. CopilotKit consumed the AG-UI event stream and rendered the result with its own prebuilt chat surface.\n\nThe key comparison is where lifecycle meaning lives. HPD’s headless UI projects thread state into a custom timeline. CopilotKit exposes messages and tool calls through AG-UI and lets its React components own more of the rendering behavior.`,
  };
}

export class DemoAgent extends AbstractAgent {
  private runSequence = 0;

  constructor() {
    super({
      agentId: 'default',
      description: 'Local AG-UI event simulator',
      threadId: 'agui-demo',
      initialMessages: [{
        id: 'welcome',
        role: 'assistant',
        content:
          'This surface is CopilotKit rendering a real AG-UI event stream from a local AbstractAgent.\n\nAsk a question, or try the “Compare AG-UI and CopilotKit” suggestion. Tool calls and reasoning are carried as protocol events; CopilotKit decides how they look.',
      }],
      initialState: {
        mode: 'local-agui-demo',
        renderer: 'copilotkit-v2',
      },
    });
  }

  clone(): this {
    const cloned = new DemoAgent() as this;
    cloned.messages = structuredClone(this.messages);
    cloned.state = structuredClone(this.state);
    cloned.agentId = this.agentId;
    cloned.threadId = this.threadId;
    return cloned;
  }

  run(input: RunAgentInput): Observable<BaseEvent> {
    const subject = new ReplaySubject<BaseEvent>();
    const runNumber = ++this.runSequence;
    const runId = input.runId;
    const prompt = readLastUserText(input);
    const response = answerFor(prompt);
    const assistantId = `assistant-${runNumber}`;
    const reasoningId = `reasoning-${runNumber}`;
    const toolId = `tool-${runNumber}`;
    const toolResultId = `tool-result-${runNumber}`;
    let cancelled = false;

    void (async () => {
      try {
        subject.next({
          type: EventType.RUN_STARTED,
          threadId: input.threadId,
          runId,
        } as BaseEvent);

        subject.next({
          type: EventType.REASONING_START,
          messageId: reasoningId,
        } as BaseEvent);
        subject.next({
          type: EventType.REASONING_MESSAGE_START,
          messageId: reasoningId,
          role: 'reasoning',
        } as BaseEvent);
        subject.next({
          type: EventType.REASONING_MESSAGE_CONTENT,
          messageId: reasoningId,
          delta: response.reasoning,
        } as BaseEvent);
        subject.next({
          type: EventType.REASONING_MESSAGE_END,
          messageId: reasoningId,
        } as BaseEvent);
        subject.next({
          type: EventType.REASONING_END,
          messageId: reasoningId,
        } as BaseEvent);

        subject.next({
          type: EventType.TOOL_CALL_START,
          toolCallId: toolId,
          toolCallName: 'compare_architectures',
          parentMessageId: assistantId,
        } as BaseEvent);
        await sleep(180);
        if (cancelled) return;
        subject.next({
          type: EventType.TOOL_CALL_ARGS,
          toolCallId: toolId,
          delta: JSON.stringify({ prompt: prompt.slice(0, 80), protocol: 'AG-UI 1.0' }),
        } as BaseEvent);
        await sleep(320);
        if (cancelled) return;
        subject.next({
          type: EventType.TOOL_CALL_END,
          toolCallId: toolId,
        } as BaseEvent);
        subject.next({
          type: EventType.TOOL_CALL_RESULT,
          messageId: toolResultId,
          toolCallId: toolId,
          content: 'Compared the AG-UI event boundary with the CopilotKit React surface.',
        } as BaseEvent);

        subject.next({
          type: EventType.TEXT_MESSAGE_START,
          messageId: assistantId,
          role: 'assistant',
        } as BaseEvent);

        const chunks = response.answer.match(/.{1,24}(?:\s+|$)/gs) ?? [response.answer];
        for (const chunk of chunks) {
          await sleep(28);
          if (cancelled) return;
          subject.next({
            type: EventType.TEXT_MESSAGE_CONTENT,
            messageId: assistantId,
            delta: chunk,
          } as BaseEvent);
        }
        subject.next({
          type: EventType.TEXT_MESSAGE_END,
          messageId: assistantId,
        } as BaseEvent);

        subject.next({
          type: EventType.RUN_FINISHED,
          threadId: input.threadId,
          runId,
        } as BaseEvent);
        subject.complete();
      } catch (error) {
        subject.next({
          type: EventType.RUN_ERROR,
          message: error instanceof Error ? error.message : String(error),
          code: 'demo-agent-failed',
        } as BaseEvent);
        subject.complete();
      }
    })();

    return new Observable<BaseEvent>((subscriber) => {
      const subscription = subject.subscribe(subscriber);
      return () => {
        cancelled = true;
        subscription.unsubscribe();
      };
    });
  }
}
