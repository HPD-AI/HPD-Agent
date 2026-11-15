import type { ThreadEventExtension } from '@hpd-research/hpd-agent-headless-ui-svelte';

export type CodingCommandState =
  | 'running'
  | 'backgrounded'
  | 'completed'
  | 'failed'
  | 'cancelled'
  | 'timed-out'
  | 'exited';

export interface CodingCommand {
  commandId: string;
  toolCallId: string;
  command: string;
  category: string;
  workingDirectory: string;
  state: CodingCommandState;
  startTime: string | null;
  completedAt: string | null;
  durationMs: number;
  stdoutBytes: number;
  stderrBytes: number;
  combinedBytes: number;
  exitCode: number | null;
  completionKind: string | null;
  background: boolean;
  operationId: string | null;
  output: string;
  outputTruncated: boolean;
  outputSuppressed: boolean;
  binaryOutput: boolean;
}

export interface CodingEdit {
  toolCallId: string;
  path: string;
  displayPath: string;
  action: string;
  addedLines: number;
  removedLines: number;
  hunks: Array<{
    oldStart: number;
    oldLines: number;
    newStart: number;
    newLines: number;
    lines: string[];
  }>;
  hunksTruncated: boolean;
}

export interface CodingDiagnostics {
  path: string;
  errorCount: number;
  warningCount: number;
  informationCount: number;
  hintCount: number;
  truncated: boolean;
  diagnostics: Array<{
    severity: string;
    source: string;
    code: string | null;
    line: number;
    character: number;
    message: string;
  }>;
}

export interface CodingHarnessState {
  commands: Record<string, CodingCommand>;
  edits: Record<string, CodingEdit>;
  diagnostics: Record<string, CodingDiagnostics>;
  order: string[];
  mutationCount: number;
  changedPathCount: number;
  addedLines: number;
  removedLines: number;
}

const MAX_OUTPUT_CHARACTERS = 64 * 1024;
const MAX_RECENT_EDITS = 50;
const MAX_DIAGNOSTIC_LINES = 64;

const initialState = (): CodingHarnessState => ({
  commands: {},
  edits: {},
  diagnostics: {},
  order: [],
  mutationCount: 0,
  changedPathCount: 0,
  addedLines: 0,
  removedLines: 0,
});

function stringValue(value: unknown, fallback = ''): string {
  return typeof value === 'string' ? value : fallback;
}

function nullableString(value: unknown): string | null {
  return typeof value === 'string' ? value : null;
}

function numberValue(value: unknown, fallback = 0): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
}

function booleanValue(value: unknown): boolean {
  return value === true;
}

function addOrder(state: CodingHarnessState, key: string): string[] {
  return state.order.includes(key) ? state.order : [...state.order, key];
}

function displayCommand(command: string): string {
  const compact = command.replace(/\s+/g, ' ').trim();
  return compact.length > 96 ? `${compact.slice(0, 93)}...` : compact;
}

function commandFromEvent(
  state: CodingHarnessState,
  event: Record<string, unknown>,
): CodingCommand {
  const commandId = stringValue(event.commandId);
  const existing = state.commands[commandId];
  if (existing) return existing;
  return {
    commandId,
    toolCallId: stringValue(event.toolCallId),
    command: stringValue(event.command, 'ExecuteCommand'),
    category: stringValue(event.category, 'Unknown'),
    workingDirectory: stringValue(event.workingDirectory),
    state: 'running',
    startTime: null,
    completedAt: null,
    durationMs: 0,
    stdoutBytes: 0,
    stderrBytes: 0,
    combinedBytes: 0,
    exitCode: null,
    completionKind: null,
    background: false,
    operationId: null,
    output: '',
    outputTruncated: false,
    outputSuppressed: false,
    binaryOutput: false,
  };
}

function appendOutput(current: string, text: string): {
  output: string;
  truncated: boolean;
} {
  const next = `${current}${text}`;
  return next.length <= MAX_OUTPUT_CHARACTERS
    ? { output: next, truncated: false }
    : {
        output: next.slice(-MAX_OUTPUT_CHARACTERS),
        truncated: true,
      };
}

function exitState(event: Record<string, unknown>): CodingCommandState {
  const completion = stringValue(event.completionKind);
  const exitCode = typeof event.exitCode === 'number' ? event.exitCode : null;
  if (completion === 'Completed') {
    return exitCode === 0 ? 'completed' : 'failed';
  }
  if (completion === 'TimedOut') return 'timed-out';
  if (completion === 'Cancelled' || completion === 'Stopped') return 'cancelled';
  if (completion === 'FailedToStart' || completion === 'Faulted') return 'failed';
  return 'exited';
}

function editAction(event: Record<string, unknown>): string {
  if (event.type === 'FILE_WRITE_APPLIED') {
    const mode = stringValue(event.mode);
    if (mode === 'Create') return 'Created';
    if (mode === 'FillEmpty') return 'Filled';
    if (mode === 'Rewrite') return 'Rewrote';
  }
  const kind = stringValue(event.mutationKind);
  if (booleanValue(event.created) || kind === 'Created') return 'Created';
  if (kind === 'Deleted') return 'Deleted';
  return 'Edited';
}

function readHunks(value: unknown): CodingEdit['hunks'] {
  if (!Array.isArray(value)) return [];
  return value.flatMap((raw) => {
    if (!raw || typeof raw !== 'object') return [];
    const hunk = raw as Record<string, unknown>;
    return [{
      oldStart: numberValue(hunk.oldStart),
      oldLines: numberValue(hunk.oldLines),
      newStart: numberValue(hunk.newStart),
      newLines: numberValue(hunk.newLines),
      lines: Array.isArray(hunk.lines)
        ? hunk.lines.filter((line): line is string => typeof line === 'string')
        : [],
    }];
  });
}

function readDiagnostics(event: Record<string, unknown>): CodingDiagnostics['diagnostics'] {
  if (!Array.isArray(event.diagnostics)) return [];
  return event.diagnostics.slice(0, MAX_DIAGNOSTIC_LINES).flatMap((raw) => {
    if (!raw || typeof raw !== 'object') return [];
    const diagnostic = raw as Record<string, unknown>;
    return [{
      severity: stringValue(diagnostic.severity, 'Information'),
      source: stringValue(diagnostic.source),
      code: typeof diagnostic.code === 'string' ? diagnostic.code : null,
      line: numberValue(diagnostic.line),
      character: numberValue(diagnostic.character),
      message: stringValue(diagnostic.message),
    }];
  });
}

export const codingHarnessExtension: ThreadEventExtension<CodingHarnessState> = {
  key: 'hpd:coding',
  initialState: initialState(),
  matches: event =>
    event.type.startsWith('EXECUTE_COMMAND_') ||
    event.type === 'FILE_EDIT_APPLIED' ||
    event.type === 'FILE_WRITE_APPLIED' ||
    event.type === 'LANGUAGE_SERVER_DIAGNOSTICS_RECEIVED' ||
    event.type === 'LANGUAGE_SERVER_STATUS_SNAPSHOT',
  reduce: (state, event) => {
    const value = event as Record<string, unknown>;

    if (event.type.startsWith('EXECUTE_COMMAND_')) {
      const command = commandFromEvent(state, value);
      const commandKey = `command:${command.commandId}`;
      const order = addOrder(state, commandKey);

      if (event.type === 'EXECUTE_COMMAND_PROCESS_STARTED') {
        return {
          ...state,
          order,
          commands: {
            ...state.commands,
            [command.commandId]: {
              ...command,
              state: booleanValue(value.background) ? 'backgrounded' : 'running',
              startTime: nullableString(value.startedAt),
              background: booleanValue(value.background),
              operationId: booleanValue(value.background)
                ? command.commandId
                : command.operationId,
            },
          },
        };
      }

      if (event.type === 'EXECUTE_COMMAND_PROGRESS') {
        return {
          ...state,
          order,
          commands: {
            ...state.commands,
            [command.commandId]: {
              ...command,
              durationMs: numberValue(value.elapsedMilliseconds),
              stdoutBytes: numberValue(value.stdoutBytes),
              stderrBytes: numberValue(value.stderrBytes),
              combinedBytes: numberValue(value.combinedOutputBytes),
              outputSuppressed: command.outputSuppressed || booleanValue(value.outputEventsSuppressed),
            },
          },
        };
      }

      if (event.type === 'EXECUTE_COMMAND_OUTPUT_CHUNK') {
        const suppressed = booleanValue(value.suppressed);
        const binary = booleanValue(value.binary);
        const appended = suppressed || binary
          ? { output: command.output, truncated: false }
          : appendOutput(command.output, stringValue(value.text));
        return {
          ...state,
          order,
          commands: {
            ...state.commands,
            [command.commandId]: {
              ...command,
              stdoutBytes: stringValue(value.stream) === 'Stdout'
                ? numberValue(value.streamBytesObserved)
                : command.stdoutBytes,
              stderrBytes: stringValue(value.stream) === 'Stderr'
                ? numberValue(value.streamBytesObserved)
                : command.stderrBytes,
              combinedBytes: numberValue(value.combinedBytesObserved),
              output: appended.output,
              outputTruncated: command.outputTruncated
                || appended.truncated
                || booleanValue(value.truncated),
              outputSuppressed: command.outputSuppressed || suppressed,
              binaryOutput: command.binaryOutput || binary,
            },
          },
        };
      }

      if (event.type === 'EXECUTE_COMMAND_AUTO_BACKGROUNDED') {
        return {
          ...state,
          order,
          commands: {
            ...state.commands,
            [command.commandId]: {
              ...command,
              state: 'backgrounded',
              background: true,
              operationId: stringValue(value.operationId, command.commandId),
              durationMs: numberValue(value.elapsedMilliseconds, command.durationMs),
            },
          },
        };
      }

      if (event.type === 'EXECUTE_COMMAND_PROCESS_EXITED') {
        return {
          ...state,
          order,
          commands: {
            ...state.commands,
            [command.commandId]: {
              ...command,
              state: exitState(value),
              completedAt: stringValue(value.timestamp, new Date().toISOString()),
              durationMs: numberValue(value.durationMilliseconds),
              stdoutBytes: numberValue(value.stdoutBytes),
              stderrBytes: numberValue(value.stderrBytes),
              combinedBytes: numberValue(value.combinedOutputBytes),
              exitCode: typeof value.exitCode === 'number' ? value.exitCode : null,
              completionKind: nullableString(value.completionKind),
              outputTruncated: command.outputTruncated || booleanValue(value.outputTruncated),
              outputSuppressed: command.outputSuppressed || booleanValue(value.outputEventsSuppressed),
            },
          },
        };
      }

      return { ...state, order };
    }

    if (event.type === 'FILE_EDIT_APPLIED' || event.type === 'FILE_WRITE_APPLIED') {
      const toolCallId = stringValue(value.toolCallId);
      if (!toolCallId) return state;
      const existing = state.edits[toolCallId];
      if (existing) return state;
      const diffStat = value.diffStat as Record<string, unknown> | undefined;
      const edit: CodingEdit = {
        toolCallId,
        path: stringValue(value.path),
        displayPath: stringValue(value.displayPath, stringValue(value.path)),
        action: editAction(value),
        addedLines: numberValue(diffStat?.addedLines),
        removedLines: numberValue(diffStat?.removedLines),
        hunks: readHunks(value.hunks),
        hunksTruncated: booleanValue(value.hunksTruncated),
      };
      const edits = Object.fromEntries(
        Object.entries({ ...state.edits, [toolCallId]: edit })
          .slice(-MAX_RECENT_EDITS),
      );
      return {
        ...state,
        edits,
        order: addOrder(state, `edit:${toolCallId}`),
        mutationCount: state.mutationCount + 1,
        changedPathCount: new Set(Object.values(edits).map(item => item.path)).size,
        addedLines: state.addedLines + edit.addedLines,
        removedLines: state.removedLines + edit.removedLines,
      };
    }

    if (event.type === 'LANGUAGE_SERVER_DIAGNOSTICS_RECEIVED') {
      const path = stringValue(value.path);
      if (!path) return state;
      return {
        ...state,
        diagnostics: {
          ...state.diagnostics,
          [path]: {
            path,
            errorCount: numberValue(value.errorCount),
            warningCount: numberValue(value.warningCount),
            informationCount: numberValue(value.informationCount),
            hintCount: numberValue(value.hintCount),
            truncated: booleanValue(value.diagnosticsTruncated),
            diagnostics: readDiagnostics(value),
          },
        },
        order: addOrder(state, `diagnostics:${path}`),
      };
    }

    return state;
  },
};

export function commandLabel(command: CodingCommand): string {
  if (command.state === 'completed') return 'Ran';
  if (command.state === 'failed') return 'Failed';
  if (command.state === 'cancelled') return 'Cancelled';
  if (command.state === 'timed-out') return 'Timed out';
  if (command.state === 'backgrounded') return 'Backgrounded';
  if (command.state === 'exited') return 'Exited';
  return 'Running';
}

export function commandSummary(command: CodingCommand): string {
  const parts: string[] = [];
  if (command.durationMs > 0) {
    parts.push(command.durationMs < 1000
      ? `${command.durationMs}ms`
      : `${(command.durationMs / 1000).toFixed(1)}s`);
  }
  if (command.combinedBytes > 0) {
    parts.push(formatBytes(command.combinedBytes));
  }
  if (command.exitCode !== null) parts.push(`exit ${command.exitCode}`);
  if (command.background) parts.push(`background ${command.operationId ?? command.commandId}`);
  if (command.outputTruncated) parts.push('output truncated');
  if (command.outputSuppressed) parts.push('output suppressed');
  if (command.binaryOutput) parts.push('binary output');
  return parts.join(' · ') || 'running';
}

export function outputWindow(command: CodingCommand): {
  head: string[];
  tail: string[];
  omitted: number;
} {
  const lines = command.output.replace(/\r\n/g, '\n').replace(/\r/g, '\n').split('\n');
  while (lines[0]?.trim() === '') lines.shift();
  while (lines.at(-1)?.trim() === '') lines.pop();
  if (lines.length <= 5) return { head: lines, tail: [], omitted: 0 };
  return {
    head: lines.slice(0, 2),
    tail: lines.slice(-2),
    omitted: lines.length - 4,
  };
}

export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function visibleDiagnostics(
  diagnostics: CodingDiagnostics,
): CodingDiagnostics['diagnostics'] {
  return diagnostics.diagnostics
    .filter(item => item.severity === 'Error' || item.severity === 'Warning')
    .sort((left, right) =>
      left.severity.localeCompare(right.severity) ||
      left.line - right.line ||
      left.character - right.character)
    .slice(0, 5);
}

export function compactCommand(command: string): string {
  return displayCommand(command);
}
