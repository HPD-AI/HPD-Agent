<script lang="ts">
  import {
    commandLabel,
    commandSummary,
    outputWindow,
    visibleDiagnostics,
    type CodingHarnessState,
  } from './coding-harness.ts';

  let {
    state,
    toolCallId,
  }: {
    state: CodingHarnessState | undefined;
    toolCallId: string;
  } = $props();

  const command = $derived(
    Object.values(state?.commands ?? {})
      .find(candidate => candidate.toolCallId === toolCallId),
  );
  const edit = $derived(state?.edits[toolCallId]);
  const diagnostics = $derived.by(() => {
    if (!edit) return undefined;
    return Object.values(state?.diagnostics ?? {}).find((candidate) => {
      const path = candidate.path.replace(/\\/g, '/');
      const display = edit.displayPath.replace(/\\/g, '/');
      return path === display || display.endsWith(`/${path}`) || path.endsWith(`/${display}`);
    });
  });
  const output = $derived(command ? outputWindow(command) : null);
  const visible = $derived(diagnostics ? visibleDiagnostics(diagnostics) : []);

  function diffKind(line: string): string {
    if (line.startsWith('+')) return 'added';
    if (line.startsWith('-')) return 'removed';
    return 'context';
  }

  function diffText(line: string): string {
    return line.startsWith('+') || line.startsWith('-') || line.startsWith(' ')
      ? line.slice(1)
      : line;
  }
</script>

{#if command}
  <section class="tool-extension command" data-status={command.state}>
    <header>
      <span class="status-dot"></span>
      <strong>{commandLabel(command)} command</strong>
      <span>{commandSummary(command)}</span>
    </header>
    <pre>
      {#if output && (output.head.length || output.tail.length)}
        {#each output.head as line}<span>{line}</span>{/each}
        {#if output.omitted > 0}<span class="muted">… +{output.omitted} lines</span>{/if}
        {#each output.tail as line}<span>{line}</span>{/each}
      {:else}
        <span class="muted">no output observed</span>
      {/if}
    </pre>
  </section>
{/if}

{#if edit}
  <section class="tool-extension edit">
    <header>
      <strong>{edit.action} file</strong>
      <span class="add">+{edit.addedLines}</span>
      <span class="remove">−{edit.removedLines}</span>
    </header>
    <div class="diff">
      {#each edit.hunks as hunk, index (index)}
        <div class="hunk">@@ -{hunk.oldStart},{hunk.oldLines} +{hunk.newStart},{hunk.newLines} @@</div>
        {#each hunk.lines as line, lineIndex (lineIndex)}
          <div class="diff-line" data-kind={diffKind(line)}>
            <span>{line[0] ?? ' '}</span>
            <code>{diffText(line)}</code>
          </div>
        {/each}
      {/each}
    </div>
    {#if diagnostics}
      <div class="diagnostics">
        {#each visible as diagnostic}
          <div data-severity={diagnostic.severity.toLowerCase()}>
            <span>{diagnostic.severity === 'Warning' ? '⚠' : '■'}</span>
            <code>{diagnostic.code ?? diagnostic.source}</code>
            <span>{diagnostic.line}:{diagnostic.character}</span>
            <p>{diagnostic.message}</p>
          </div>
        {/each}
      </div>
    {/if}
  </section>
{/if}

<style>
  .tool-extension {
    margin: 6px 12px 10px;
    overflow: hidden;
    border: 1px solid rgb(23 33 29 / 0.085);
    border-radius: 10px;
    background: #fbfcfb;
  }

  header {
    display: flex;
    align-items: center;
    gap: 6px;
    padding: 6px 8px;
    color: rgb(23 33 29 / 0.5);
    font-size: 8px;
  }

  header strong {
    color: rgb(23 33 29 / 0.66);
    font-weight: 650;
  }

  header > span:last-child {
    margin-left: auto;
    color: rgb(23 33 29 / 0.32);
    font-family: var(--font-mono);
    font-size: 7px;
  }

  .status-dot {
    width: 5px;
    height: 5px;
    border-radius: 999px;
    background: #ad761e;
  }

  .command[data-status="completed"] .status-dot { background: #287a4b; }
  .command[data-status="failed"] .status-dot,
  .command[data-status="timed-out"] .status-dot { background: #b44857; }

  pre {
    max-height: 96px;
    overflow: auto;
    margin: 0;
    border-top: 1px solid rgb(23 33 29 / 0.06);
    background: #18211d;
    padding: 7px 9px;
    color: #d8ded9;
    font-family: var(--font-mono);
    font-size: 8px;
    line-height: 1.55;
    white-space: pre-wrap;
  }

  pre span {
    display: block;
  }

  .muted {
    color: rgb(216 222 217 / 0.42);
    font-style: italic;
  }

  .add { color: #267848; }
  .remove { color: #b44857; }

  .diff {
    border-top: 1px solid rgb(23 33 29 / 0.06);
    font-family: var(--font-mono);
    font-size: 8px;
  }

  .hunk {
    background: #eef3f9;
    padding: 3px 8px;
    color: #5572a5;
  }

  .diff-line {
    display: grid;
    grid-template-columns: 12px minmax(0, 1fr);
    padding: 1px 8px;
    white-space: pre-wrap;
  }

  .diff-line[data-kind="added"] {
    background: #e6f6ec;
    color: #276c42;
  }

  .diff-line[data-kind="removed"] {
    background: #fbeaec;
    color: #9c3f4b;
  }

  .diff-line[data-kind="context"] {
    color: rgb(23 33 29 / 0.5);
  }

  .diagnostics {
    border-top: 1px solid rgb(23 33 29 / 0.06);
    padding: 6px 8px;
  }

  .diagnostics > div {
    display: grid;
    grid-template-columns: 10px auto auto minmax(0, 1fr);
    gap: 4px;
    color: #9f5640;
    font-family: var(--font-mono);
    font-size: 7px;
  }

  .diagnostics > div[data-severity="warning"] {
    color: #8e6a24;
  }

  .diagnostics p {
    min-width: 0;
    margin: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
</style>
