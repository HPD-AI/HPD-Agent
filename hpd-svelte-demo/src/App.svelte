<script lang="ts">
  import { onDestroy, onMount } from 'svelte';
  import {
    ContextDisplayBar,
    ContextDisplayBreakdown,
    ContextDisplayRoot,
    ContextDisplayText,
    createThreadState,
    SuggestionList,
    ThreadComposer,
    ThreadConversation,
    ThreadError,
    ThreadScrollToBottom,
    ThreadStatus,
    ThreadStatusIndicator,
    ThreadStatusMetrics,
    ThreadTimeline,
    ThreadWorkGroup,
    ToolCall,
    type ThreadState,
  } from '@hpd-research/hpd-agent-headless-ui-svelte';
  import {
    createDemoAgentClient,
  } from '../../hpd-chat-demo/src/demo-client.ts';
  import CodingToolDetails from './CodingToolDetails.svelte';
  import {
    codingHarnessExtension,
    type CodingHarnessState,
  } from './coding-harness.ts';
  import {
    loadPresentationConfig,
    presentationStyle,
    savePresentationConfig,
    themePresets,
    type Density,
    type MessageStyle,
    type Shape,
    type ThemeId,
  } from './presentation.ts';

  const environment = createDemoAgentClient();
  const thread: ThreadState = createThreadState({
    client: environment.client,
    agentId: 'hpd-demo',
    sessionId: 'demo-session',
    threadId: 'main',
    extensions: [codingHarnessExtension],
  });

  let snapshot = $state(thread.getSnapshot());
  const coding = $derived(
    snapshot.extensions['hpd:coding'] as CodingHarnessState | undefined,
  );
  let ready = $state(false);
  let draft = $state('');
  let unsubscribe: (() => void) | undefined;

  let theme = $state<ThemeId>('studio');
  let density = $state<Density>('comfortable');
  let shape = $state<Shape>('soft');
  let messageStyle = $state<MessageStyle>('bubbles');
  let presentationLoaded = $state(false);

  const suggestions = [
    {
      prompt: 'Is the Svelte adapter easier?',
      title: 'Svelte adapter',
      description: 'Compare application code with the custom renderer',
    },
    {
      prompt: 'Show the component split',
      title: 'Component split',
      description: 'Explain what the adapter owns',
    },
    {
      prompt: 'What does HPD own here?',
      title: 'HPD ownership',
      description: 'Separate lifecycle state from presentation',
    },
  ];

  onMount(() => {
    const saved = loadPresentationConfig();
    theme = saved.theme;
    density = saved.density;
    shape = saved.shape;
    messageStyle = saved.messageStyle;
    presentationLoaded = true;

    unsubscribe = thread.subscribe((next) => {
      snapshot = next;
    });
    void thread.start({ includeExecutions: true })
      .then(() => {
        ready = true;
      })
      .catch((cause: unknown) => {
        console.error(cause);
      });

    return () => {
      unsubscribe?.();
      void thread.dispose();
    };
  });

  $effect(() => {
    if (presentationLoaded) {
      savePresentationConfig({ theme, density, shape, messageStyle });
    }
  });

  onDestroy(() => {
    unsubscribe?.();
  });

  async function previewApproval(): Promise<void> {
    if (!snapshot.textSubmissionState.canSubmit) return;
    await environment.requestApproval();
  }

  async function previewCoding(): Promise<void> {
    if (!snapshot.textSubmissionState.canSubmit) return;
    await environment.requestCodingDemo();
  }

  function ownsCodingTool(tool: {
    callId: string;
    name: string;
    toolharnessName?: string;
  }): boolean {
    const current = coding;
    if (!current || tool.toolharnessName !== 'CodingToolHarness') return false;
    if (tool.name === 'ExecuteCommand') {
      return Object.values(current.commands)
        .some(command => command.toolCallId === tool.callId);
    }
    if (tool.name === 'EditFile' || tool.name === 'WriteFile') {
      return Boolean(current.edits[tool.callId]);
    }
    return false;
  }
</script>

<svelte:head>
  <title>HPD Svelte Headless Lab</title>
</svelte:head>

<div
  class="flex h-screen min-h-[700px] overflow-hidden bg-[var(--hpd-page)] text-[#17211d]"
  data-density={density}
  data-shape={shape}
  data-message-style={messageStyle}
  style={presentationStyle(theme)}
>
  <aside class="hidden w-[264px] shrink-0 flex-col bg-[var(--hpd-sidebar)] px-5 py-5 text-white lg:flex">
    <div class="flex items-center gap-3 px-1">
      <div class="grid size-10 place-items-center rounded-2xl bg-[var(--hpd-accent)] text-[var(--hpd-sidebar)] shadow-[0_0_35px_color-mix(in_srgb,var(--hpd-accent)_18%,transparent)]">
        <svg aria-hidden="true" width="21" height="21" viewBox="0 0 24 24" fill="none"
          stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
          <path d="m12 3 1.3 4.1L17.5 9l-4.2 1.9L12 15l-1.3-4.1L6.5 9l4.2-1.9z" />
          <path d="m19 14 .8 2.2 2.2.8-2.2.8L19 20l-.8-2.2-2.2-.8 2.2-.8z" />
        </svg>
      </div>
      <div>
        <div class="text-[13px] font-semibold tracking-[-0.01em]">HPD Svelte</div>
        <div class="text-[9px] uppercase tracking-[0.18em] text-white/35">Headless adapter</div>
      </div>
    </div>

    <button class="mt-8 flex items-center justify-between rounded-xl border border-white/10 bg-white/[0.045] px-3 py-2.5 text-[12px] text-white/65 transition hover:bg-white/[0.075] hover:text-white">
      <span class="flex items-center gap-2">
        <span class="text-lg leading-none text-[var(--hpd-accent)]">+</span>
        New thread
      </span>
      <span class="font-mono text-[9px] text-white/25">⌘ K</span>
    </button>

    <div class="mt-8 text-[9px] font-semibold uppercase tracking-[0.18em] text-white/25">Current</div>
    <button class="mt-2 rounded-xl border border-[var(--hpd-accent)]/10 bg-[var(--hpd-accent)]/[0.055] px-3 py-3 text-left">
      <div class="text-[12px] font-medium text-white/80">HPD product studio</div>
      <div class="mt-1 text-[10px] text-white/32">Svelte primitives + Tailwind</div>
    </button>

    <div class="mt-7">
      <div class="text-[9px] font-semibold uppercase tracking-[0.18em] text-white/25">Component path</div>
      <div class="mt-3 space-y-2">
        {#each [
          ['AgentClient', 'HPD transport'],
          ['createThreadState', 'Reactive controller'],
          ['ThreadConversation', 'Composed shell'],
          ['Tailwind', 'Unstyled hooks'],
        ] as [name, role]}
          <div class="flex items-center gap-2 rounded-lg border border-white/[0.045] px-3 py-2">
            <span class="size-1.5 rounded-full bg-[var(--hpd-accent)]/65"></span>
            <span class="font-mono text-[9px] text-white/52">{name}</span>
            <span class="ml-auto text-[8px] text-white/20">{role}</span>
          </div>
        {/each}
      </div>
    </div>

    <div class="mt-auto rounded-xl border border-white/[0.07] bg-white/[0.025] p-3">
      <div class="flex items-center gap-2 text-[10px] text-white/55">
        <span class="size-1.5 rounded-full bg-[var(--hpd-accent)]"></span>
        Local HPD simulation
      </div>
      <p class="mt-2 text-[9px] leading-relaxed text-white/28">
        Same local events as the custom demo. The Svelte adapter owns the composer,
        timeline, work groups, tools, and runtime requests.
      </p>
    </div>
  </aside>

  <main class="flex min-w-0 flex-1 flex-col">
    <header class="flex h-[70px] shrink-0 items-center justify-between border-b border-[#17211d]/[0.08] bg-[var(--hpd-shell)]/85 px-5 backdrop-blur md:px-7">
      <div>
        <div class="flex items-center gap-2">
          <h1 class="text-[14px] font-semibold tracking-[-0.01em]">Svelte headless conversation</h1>
          <span class="rounded-full bg-[var(--hpd-sidebar)] px-2 py-0.5 text-[8px] font-semibold uppercase tracking-[0.14em] text-[var(--hpd-accent)]">
            Adapter
          </span>
        </div>
        <p class="mt-1 text-[10px] text-[#17211d]/38">Thread primitive from @hpd-research/hpd-agent-headless-ui-svelte</p>
      </div>
      <div class="hidden items-center gap-2 sm:flex">
        <button
          class="rounded-xl border border-[#17211d]/10 bg-white px-3 py-2 text-[10px] font-semibold text-[#17211d]/55 shadow-sm transition hover:border-[#17211d]/20 hover:text-[#17211d]"
          onclick={previewCoding}
        >
          Preview coding
        </button>
        <button
          class="rounded-xl border border-[#17211d]/10 bg-white px-3 py-2 text-[10px] font-semibold text-[#17211d]/55 shadow-sm transition hover:border-[#17211d]/20 hover:text-[#17211d]"
          onclick={previewApproval}
        >
          Preview approval
        </button>
      </div>
    </header>

    <div class="min-h-0 flex-1 p-3 md:p-5">
      {#if ready}
        <ThreadConversation
          {thread}
          class="hpd-conversation"
          runtimeRequestPlacement="composer-panel"
          viewportProps={{ turnAnchor: 'top', scrollBehavior: 'smooth' }}
        >
          {#snippet header({ snapshot: current })}
            <ThreadStatus {thread} class="conversation-status">
              {#snippet children(status)}
                <ThreadStatusIndicator {status} />
                <span class="status-separator">/</span>
                <span>{current.workGroups.length} work groups</span>
                <ThreadStatusMetrics {status} class="status-metrics" />
              {/snippet}
            </ThreadStatus>
          {/snippet}

          {#snippet timeline({ snapshot: current, thread: activeThread })}
            <ThreadTimeline thread={activeThread} timeline={current.timeline}>
              {#snippet work({ work })}
                <ThreadWorkGroup {work}>
                  {#snippet toolPart({ part, props })}
                    {#if ownsCodingTool(part.tool)}
                      <CodingToolDetails state={coding} toolCallId={part.tool.callId} />
                    {:else}
                      <ToolCall tool={part.tool} {...props} />
                    {/if}
                  {/snippet}
                </ThreadWorkGroup>
              {/snippet}
            </ThreadTimeline>
          {/snippet}

          {#snippet footer({ thread: activeThread })}
            <ThreadScrollToBottom>Jump to latest</ThreadScrollToBottom>

            <SuggestionList
              {thread}
              {suggestions}
              mode="send"
              class="suggestions"
            />

            <ThreadComposer
              thread={activeThread}
              bind:value={draft}
              placeholder="Ask the Svelte headless thread..."
              minRows={1}
              maxRows={7}
              submitMode="enter"
            >
              {#snippet child({ state, props })}
                <form {...props.root} class="hpd-composer">
                  <textarea {...props.input} {@attach props.inputAttachment}></textarea>
                  <div class="composer-actions">
                    <span>
                      {#if state.blockedReason === 'runtime-request'}
                        Answer the request first
                      {:else if state.busy}
                        Agent is working
                      {:else}
                        Enter to send
                      {/if}
                    </span>
                    {#if state.canInterrupt}
                      <button {...props.interrupt} class="stop-button" aria-label="Stop response">
                        <span></span>
                      </button>
                    {:else}
                      <button {...props.submit} class="send-button" aria-label="Send message">
                        <svg aria-hidden="true" width="17" height="17" viewBox="0 0 24 24" fill="none"
                          stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                          <path d="M12 19V5M6 11l6-6 6 6" />
                        </svg>
                      </button>
                    {/if}
                  </div>
                </form>
              {/snippet}
            </ThreadComposer>
          {/snippet}
        </ThreadConversation>
        <ThreadError {thread} showAll />
      {:else}
        <div class="grid h-full place-items-center rounded-[26px] border border-[#17211d]/10 bg-[var(--hpd-shell)]">
          <div class="text-center">
            <div class="mx-auto size-8 animate-spin rounded-full border-2 border-[#17211d]/10 border-t-[#17211d]"></div>
            <p class="mt-4 text-[11px] text-[#17211d]/38">Loading Svelte thread state...</p>
          </div>
        </div>
      {/if}
    </div>
  </main>

  <aside class="hidden w-[268px] shrink-0 overflow-y-auto border-l border-[#17211d]/[0.08] bg-[var(--hpd-panel)] p-5 xl:block">
    <div class="text-[9px] font-semibold uppercase tracking-[0.18em] text-[#17211d]/35">Projection</div>

    <div class="config-card">
      <div class="flex items-center justify-between">
        <span class="text-[10px] font-medium text-[#17211d]/55">Appearance</span>
        <span class="config-live">Runtime</span>
      </div>

      <div class="mt-3 text-[8px] uppercase tracking-[0.12em] text-[#17211d]/28">Palette</div>
      <div class="mt-2 grid grid-cols-4 gap-1.5">
        {#each themePresets as preset}
          <button
            class="theme-swatch"
            class:active={theme === preset.id}
            aria-label={`Use ${preset.label} palette`}
            aria-pressed={theme === preset.id}
            onclick={() => { theme = preset.id; }}
          >
            <span style={`background:${preset.accent}`}></span>
            <small>{preset.label}</small>
          </button>
        {/each}
      </div>

      <div class="mt-4 grid grid-cols-2 gap-3">
        <label class="config-field">
          <span>Density</span>
          <select bind:value={density}>
            <option value="comfortable">Comfortable</option>
            <option value="compact">Compact</option>
          </select>
        </label>
        <label class="config-field">
          <span>Corners</span>
          <select bind:value={shape}>
            <option value="soft">Soft</option>
            <option value="sharp">Sharp</option>
          </select>
        </label>
      </div>

      <label class="config-field mt-3">
        <span>Transcript</span>
        <select bind:value={messageStyle}>
          <option value="bubbles">Conversation bubbles</option>
          <option value="document">Aligned document</option>
        </select>
      </label>
    </div>

    <div class="mt-4 rounded-2xl border border-[#17211d]/10 bg-[var(--hpd-shell)] p-4 shadow-sm">
      <div class="flex items-center justify-between">
        <span class="text-[10px] font-medium text-[#17211d]/55">Thread activity</span>
        <span class="rounded-full bg-[#d8ff70]/65 px-2 py-1 text-[8px] font-semibold uppercase tracking-[0.12em] text-[#17211d]/65">
          {snapshot.activity.status}
        </span>
      </div>
      <div class="mt-4 grid grid-cols-2 gap-2">
        {#each [
          ['Tools', snapshot.activeTools.length],
          ['Requests', snapshot.pendingRuntimeRequests.length],
          ['Work', snapshot.workGroups.length],
          ['Messages', snapshot.transcriptMessages.length],
        ] as [label, value]}
      <div class="rounded-xl border border-[#17211d]/[0.07] bg-white p-2.5">
            <div class="text-[8px] uppercase tracking-[0.12em] text-[#17211d]/30">{label}</div>
            <div class="mt-1 font-mono text-[16px] text-[#17211d]/70">{value}</div>
          </div>
        {/each}
      </div>

      <ContextDisplayRoot
        {thread}
        modelContextWindow={128000}
        class="context-display"
      >
        <div class="context-display-label">
          <span>Context usage</span>
          <ContextDisplayText />
        </div>
        <ContextDisplayBar />
        <ContextDisplayBreakdown />
      </ContextDisplayRoot>
    </div>

    <div class="mt-4 rounded-2xl border border-[#17211d]/10 bg-[var(--hpd-shell)] p-4">
      <div class="text-[10px] font-medium text-[#17211d]/55">What changed</div>
      <ul class="mt-3 space-y-2.5 text-[9px] leading-relaxed text-[#17211d]/38">
        <li>No custom message loop.</li>
        <li>No custom work-group renderer.</li>
        <li>No custom request control.</li>
        <li>Only stable styling hooks and snippets.</li>
      </ul>
    </div>

    <a href="http://127.0.0.1:4174/" class="mt-4 inline-flex text-[10px] font-semibold text-[#17211d]/50 underline decoration-[#17211d]/20 underline-offset-4 hover:text-[#17211d]">
      Open CopilotKit comparison ↗
    </a>
  </aside>
</div>
