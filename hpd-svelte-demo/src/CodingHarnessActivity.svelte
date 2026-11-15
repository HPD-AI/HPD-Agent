<script lang="ts">
  import {
    compactCommand,
    commandLabel,
    commandSummary,
    formatBytes,
    outputWindow,
    visibleDiagnostics,
    type CodingDiagnostics,
    type CodingEdit,
    type CodingHarnessState,
  } from './coding-harness.ts';

  let { state }: { state: CodingHarnessState | undefined } = $props();

  const commands = $derived(Object.values(state?.commands ?? {}));
  const edits = $derived(Object.values(state?.edits ?? {}));
  const diagnostics = $derived(Object.values(state?.diagnostics ?? {}));

  function diagnosticsForPath(path: string): CodingDiagnostics | undefined {
    const normalized = path.replace(/\\/g, '/');
    return diagnostics.find((item) => {
      const candidate = item.path.replace(/\\/g, '/');
      return candidate === normalized
        || normalized.endsWith(`/${candidate}`)
        || candidate.endsWith(`/${normalized}`);
    });
  }

  function isAttachedDiagnostics(key: string): boolean {
    if (!key.startsWith('diagnostics:')) return false;
    const path = key.slice('diagnostics:'.length);
    return edits.some(edit =>
      edit.path.replace(/\\/g, '/') === path
      || edit.displayPath.replace(/\\/g, '/') === path);
  }

  function diffKind(line: string): 'added' | 'removed' | 'context' {
    if (line.startsWith('+')) return 'added';
    if (line.startsWith('-')) return 'removed';
    return 'context';
  }

  function diffText(line: string): string {
    return line.startsWith('+') || line.startsWith('-') || line.startsWith(' ')
      ? line.slice(1)
      : line;
  }

  function editDiagnostics(edit: CodingEdit): CodingDiagnostics | undefined {
    return diagnosticsForPath(edit.displayPath) ?? diagnosticsForPath(edit.path);
  }
</script>

<section class="coding-activity" data-empty={!commands.length && !edits.length && !diagnostics.length}>
  <header class="coding-heading">
    <div>
      <span class="eyebrow">Coding extension</span>
      <h3>Harness activity</h3>
    </div>
    <span class="live-dot" aria-label="Extension state is live"></span>
  </header>

  <div class="coding-stats">
    <div>
      <span>Commands</span>
      <strong>{commands.length}</strong>
    </div>
    <div>
      <span>Files</span>
      <strong>{state?.changedPathCount ?? 0}</strong>
    </div>
    <div>
      <span>Diff</span>
      <strong class="add">+{state?.addedLines ?? 0}</strong>
      <strong class="remove">−{state?.removedLines ?? 0}</strong>
    </div>
  </div>

  {#if !commands.length && !edits.length && !diagnostics.length}
    <p class="empty">Run the coding preview to project command, edit, and diagnostic events.</p>
  {:else}
    <div class="coding-items">
      {#each state?.order ?? [] as key (key)}
        {#if key.startsWith('command:')}
          {@const command = state?.commands[key.slice('command:'.length)]}
          {#if command}
            {@const output = outputWindow(command)}
            <details class="coding-card command" data-status={command.state}>
              <summary>
                <span class="status-dot"></span>
                <span class="summary-label">{commandLabel(command)}</span>
                <span class="command-text">{compactCommand(command.command)}</span>
                <span class="summary-meta">{commandSummary(command)}</span>
              </summary>
              <div class="command-meta">
                <span>{command.category}</span>
                {#if command.workingDirectory}<code>{command.workingDirectory}</code>{/if}
              </div>
              <pre class="command-output" data-suppressed={command.outputSuppressed || command.binaryOutput}>
                {#if output.head.length || output.tail.length}
                  {#each output.head as line}<span>{line}</span>{/each}
                  {#if output.omitted > 0}<span class="omitted">… +{output.omitted} lines</span>{/if}
                  {#each output.tail as line}<span>{line}</span>{/each}
                {:else}
                  <span class="no-output">no output observed</span>
                {/if}
              </pre>
              <div class="command-footer">
                <span>{formatBytes(command.combinedBytes)}</span>
                {#if command.background}
                  <span>background</span>
                  <code>{command.operationId ?? command.commandId}</code>
                {/if}
              </div>
            </details>
          {/if}
        {:else if key.startsWith('edit:')}
          {@const edit = state?.edits[key.slice('edit:'.length)]}
          {#if edit}
            {@const lint = editDiagnostics(edit)}
            <details class="coding-card edit">
              <summary>
                <span class="file-kind">{edit.action}</span>
                <span class="file-path">{edit.displayPath}</span>
                <span class="diff-stat"><span class="add">+{edit.addedLines}</span><span class="remove">−{edit.removedLines}</span></span>
              </summary>
              <div class="diff">
                {#each edit.hunks as hunk, hunkIndex (hunkIndex)}
                  <div class="hunk-header">@@ -{hunk.oldStart},{hunk.oldLines} +{hunk.newStart},{hunk.newLines} @@</div>
                  {#each hunk.lines as line, lineIndex (lineIndex)}
                    <div class="diff-line" data-kind={diffKind(line)}>
                      <span class="diff-marker">{line[0] ?? ' '}</span>
                      <span>{diffText(line)}</span>
                    </div>
                  {/each}
                {/each}
                {#if edit.hunksTruncated}<p class="omitted">diff truncated</p>{/if}
              </div>
              {#if lint}
                {@const visible = visibleDiagnostics(lint)}
                <div class="diagnostics">
                  <div class="diagnostics-title">Diagnostics</div>
                  {#if visible.length}
                    {#each visible as diagnostic}
                      <div class="diagnostic" data-severity={diagnostic.severity.toLowerCase()}>
                        <span class="diagnostic-marker">{diagnostic.severity === 'Warning' ? '⚠' : '■'}</span>
                        <code>{diagnostic.code ?? diagnostic.source}</code>
                        <span>{diagnostic.line}:{diagnostic.character}</span>
                        <p>{diagnostic.message}</p>
                      </div>
                    {/each}
                  {:else}
                    <p class="no-output">no errors or warnings</p>
                  {/if}
                  {#if lint.truncated}<p class="omitted">⋮ diagnostics omitted</p>{/if}
                </div>
              {/if}
            </details>
          {/if}
        {:else if key.startsWith('diagnostics:') && !isAttachedDiagnostics(key)}
          {@const lint = state?.diagnostics[key.slice('diagnostics:'.length)]}
          {#if lint}
            {@const visible = visibleDiagnostics(lint)}
            <details class="coding-card diagnostics-card" open={visible.length > 0}>
              <summary>
                <span class="file-kind">Diagnostics</span>
                <span class="file-path">{lint.path}</span>
                <span class="diagnostic-counts">{lint.errorCount} errors · {lint.warningCount} warnings</span>
              </summary>
              <div class="diagnostics">
                {#each visible as diagnostic}
                  <div class="diagnostic" data-severity={diagnostic.severity.toLowerCase()}>
                    <span class="diagnostic-marker">{diagnostic.severity === 'Warning' ? '⚠' : '■'}</span>
                    <code>{diagnostic.code ?? diagnostic.source}</code>
                    <span>{diagnostic.line}:{diagnostic.character}</span>
                    <p>{diagnostic.message}</p>
                  </div>
                {/each}
                {#if !visible.length}<p class="no-output">no errors or warnings</p>{/if}
                {#if lint.truncated}<p class="omitted">⋮ diagnostics omitted</p>{/if}
              </div>
            </details>
          {/if}
        {/if}
      {/each}
    </div>
  {/if}
</section>

<style>
  .coding-activity {
    border: 1px solid rgb(23 33 29 / 0.1);
    border-radius: 18px;
    background: var(--hpd-shell);
    padding: 14px;
    box-shadow: 0 8px 30px rgb(23 33 29 / 0.045);
  }

  .coding-heading {
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    gap: 12px;
  }

  .eyebrow {
    color: rgb(23 33 29 / 0.32);
    font-size: 8px;
    font-weight: 650;
    letter-spacing: 0.13em;
    text-transform: uppercase;
  }

  h3 {
    margin: 3px 0 0;
    color: rgb(23 33 29 / 0.72);
    font-size: 12px;
    font-weight: 650;
  }

  .live-dot {
    width: 7px;
    height: 7px;
    margin-top: 5px;
    border-radius: 999px;
    background: var(--hpd-accent);
    box-shadow: 0 0 0 4px color-mix(in srgb, var(--hpd-accent) 22%, transparent);
  }

  .coding-stats {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 6px;
    margin-top: 12px;
  }

  .coding-stats > div {
    min-width: 0;
    border-radius: 10px;
    background: var(--hpd-panel);
    padding: 7px 8px;
  }

  .coding-stats span {
    display: block;
    color: rgb(23 33 29 / 0.3);
    font-size: 7px;
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .coding-stats strong {
    display: inline-block;
    margin-top: 2px;
    color: rgb(23 33 29 / 0.68);
    font-family: var(--font-mono);
    font-size: 12px;
    font-weight: 550;
  }

  .add { color: #267848; }
  .remove { color: #b44857; }

  .empty {
    margin: 12px 0 0;
    color: rgb(23 33 29 / 0.34);
    font-size: 9px;
    line-height: 1.6;
  }

  .coding-items {
    display: grid;
    gap: 7px;
    margin-top: 10px;
  }

  .coding-card {
    overflow: hidden;
    border: 1px solid rgb(23 33 29 / 0.085);
    border-radius: 11px;
    background: #fff;
  }

  summary {
    display: flex;
    cursor: pointer;
    list-style: none;
    align-items: center;
    gap: 6px;
    padding: 8px 9px;
    color: rgb(23 33 29 / 0.58);
    font-size: 8px;
  }

  summary::-webkit-details-marker { display: none; }

  .status-dot {
    width: 5px;
    height: 5px;
    flex: none;
    border-radius: 999px;
    background: currentColor;
  }

  .command[data-status="completed"] summary { color: #287a4b; }
  .command[data-status="failed"] summary,
  .command[data-status="timed-out"] summary { color: #b44857; }
  .command[data-status="cancelled"] summary { color: #9a6a23; }
  .command[data-status="backgrounded"] summary { color: #705bc5; }
  .command[data-status="running"] summary { color: #ad761e; }

  .summary-label {
    flex: none;
    font-weight: 700;
  }

  .command-text,
  .file-path {
    min-width: 0;
    overflow: hidden;
    color: rgb(23 33 29 / 0.72);
    font-family: var(--font-mono);
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .summary-meta {
    margin-left: auto;
    flex: none;
    color: rgb(23 33 29 / 0.3);
    font-size: 7px;
  }

  .command-meta,
  .command-footer {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
    border-top: 1px solid rgb(23 33 29 / 0.06);
    padding: 6px 9px;
    color: rgb(23 33 29 / 0.32);
    font-size: 7px;
  }

  code {
    font-family: var(--font-mono);
    font-size: inherit;
  }

  .command-output {
    max-height: 136px;
    overflow: auto;
    margin: 0;
    background: #18211d;
    padding: 8px 10px;
    color: #d8ded9;
    font-family: var(--font-mono);
    font-size: 8px;
    line-height: 1.55;
    white-space: pre-wrap;
  }

  .command-output > span {
    display: block;
    min-height: 1.55em;
  }

  .omitted,
  .no-output {
    color: rgb(23 33 29 / 0.31);
    font-style: italic;
  }

  .command-output .omitted,
  .command-output .no-output {
    color: rgb(216 222 217 / 0.45);
  }

  .file-kind {
    flex: none;
    color: rgb(23 33 29 / 0.42);
    font-weight: 700;
    text-transform: uppercase;
  }

  .diff-stat {
    display: flex;
    margin-left: auto;
    gap: 4px;
    font-family: var(--font-mono);
  }

  .diff {
    border-top: 1px solid rgb(23 33 29 / 0.07);
    background: #fbfcfb;
    padding: 4px 0;
    font-family: var(--font-mono);
    font-size: 8px;
  }

  .hunk-header {
    padding: 3px 9px;
    color: #5572a5;
    background: #eef3f9;
  }

  .diff-line {
    display: grid;
    grid-template-columns: 13px minmax(0, 1fr);
    padding: 1px 9px;
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
    color: rgb(23 33 29 / 0.52);
  }

  .diff-marker {
    user-select: none;
  }

  .diagnostics {
    border-top: 1px solid rgb(23 33 29 / 0.07);
    padding: 8px 9px;
  }

  .diagnostics-title {
    margin-bottom: 5px;
    color: rgb(23 33 29 / 0.34);
    font-size: 7px;
    font-weight: 700;
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .diagnostic {
    display: grid;
    grid-template-columns: 10px auto auto minmax(0, 1fr);
    gap: 4px;
    align-items: baseline;
    color: #9f5640;
    font-family: var(--font-mono);
    font-size: 7px;
    line-height: 1.55;
  }

  .diagnostic[data-severity="warning"] {
    color: #8e6a24;
  }

  .diagnostic p {
    min-width: 0;
    margin: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .diagnostic-marker {
    font-size: 6px;
  }

  .diagnostic-counts {
    margin-left: auto;
    color: rgb(23 33 29 / 0.32);
    font-size: 7px;
  }
</style>
