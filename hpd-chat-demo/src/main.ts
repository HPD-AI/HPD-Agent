import { AgentClient, HpdApiError } from '@hpd-research/hpd-agent-client';
import {
  createThreadController,
  getActiveToolCalls,
  getPendingRuntimeRequests,
  getTextSubmissionState,
  getThreadTimeline,
  type RuntimeRequest,
  type ThreadController,
  type ThreadProjectionSnapshot,
  type ThreadTimelineItem,
  type ThreadWorkGroup,
  type ToolCall,
} from '@hpd-research/hpd-agent-headless-ui';
import './style.css';
import { createDemoAgentClient, type DemoAgentClient } from './demo-client';

const app = document.querySelector<HTMLDivElement>('#app');
if (!app) throw new Error('Missing #app element');

const icon = (path: string, size = 18): string => `
  <svg aria-hidden="true" width="${size}" height="${size}" viewBox="0 0 24 24"
    fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"
    stroke-linejoin="round">${path}</svg>
`;

const icons = {
  plus: icon('<path d="M12 5v14M5 12h14"/>'),
  chat: icon('<path d="M21 15a4 4 0 0 1-4 4H8l-5 3V7a4 4 0 0 1 4-4h10a4 4 0 0 1 4 4z"/>'),
  spark: icon('<path d="m12 3 1.2 3.4L16.6 8l-3.4 1.2L12 12.6l-1.2-3.4L7.4 8l3.4-1.6z"/><path d="m19 14 .7 2.1 2.1.9-2.1.7-.7 2.3-.7-2.3-2.1-.7 2.1-.9z"/>'),
  layers: icon('<path d="m12 2 9 5-9 5-9-5z"/><path d="m3 12 9 5 9-5M3 17l9 5 9-5"/>'),
  activity: icon('<path d="M3 12h4l2.5-7 5 14L17 12h4"/>'),
  arrow: icon('<path d="m5 12 14 0M13 6l6 6-6 6"/>'),
  check: icon('<path d="m5 12 4 4L19 6"/>'),
  close: icon('<path d="m6 6 12 12M18 6 6 18"/>'),
  chevron: icon('<path d="m9 18 6-6-6-6"/>', 16),
  tool: icon('<path d="M14.7 6.3a4 4 0 0 0-5-5L12 3.6 9.6 6 7.3 3.7a4 4 0 0 0 5 5l-7.8 7.8a2.1 2.1 0 0 0 3 3l7.8-7.8a4 4 0 0 0 5-5L18 9l-2.4-2.4z"/>', 15),
  brain: icon('<path d="M9.5 4A3.5 3.5 0 0 0 6 7.5v.3A3.5 3.5 0 0 0 4 11a3.5 3.5 0 0 0 2 3.2 3.5 3.5 0 0 0 3.5 5.3V4Z"/><path d="M14.5 4A3.5 3.5 0 0 1 18 7.5v.3a3.5 3.5 0 0 1 2 3.2 3.5 3.5 0 0 1-2 3.2 3.5 3.5 0 0 1-3.5 5.3V4Z"/>', 15),
  shield: icon('<path d="M12 3 20 6v5c0 5-3.4 8.3-8 10-4.6-1.7-8-5-8-10V6z"/><path d="m9 12 2 2 4-4"/>', 16),
  stop: icon('<rect x="6" y="6" width="12" height="12" rx="2"/>'),
  paperclip: icon('<path d="m20 11-8.5 8.5a5 5 0 0 1-7-7L14 3a3.5 3.5 0 0 1 5 5l-9.5 9.5a2 2 0 0 1-3-3L15 6"/>', 17),
};

app.innerHTML = `
  <div class="noise"></div>
  <div class="mx-auto flex h-full min-h-screen max-w-[1600px] overflow-hidden bg-[#07100e]/60">
    <aside class="hidden w-[248px] shrink-0 flex-col border-r border-white/[0.07] bg-black/15 px-4 py-5 lg:flex">
      <div class="flex items-center gap-3 px-2">
        <div class="grid size-9 place-items-center rounded-xl border border-mint-300/25 bg-mint-300/10 text-mint-300 shadow-[0_0_30px_rgba(94,230,174,0.08)]">
          ${icons.spark}
        </div>
        <div>
          <div class="text-[13px] font-semibold tracking-[0.18em] text-white">HPD</div>
          <div class="text-[10px] tracking-[0.14em] text-white/35">CHAT LAB</div>
        </div>
      </div>

      <button id="new-chat" class="mt-7 flex items-center justify-between rounded-xl border border-white/[0.08] bg-white/[0.035] px-3 py-2.5 text-sm text-white/75 transition hover:border-mint-300/25 hover:bg-mint-300/[0.06] hover:text-white">
        <span class="flex items-center gap-2">${icons.plus} New thread</span>
        <span class="font-mono text-[10px] text-white/25">⌘ K</span>
      </button>

      <div class="mt-8 text-[10px] font-semibold tracking-[0.18em] text-white/30">TODAY</div>
      <nav class="mt-3 space-y-1">
        <button class="group w-full rounded-xl border border-mint-300/[0.12] bg-mint-300/[0.07] px-3 py-3 text-left">
          <span class="flex items-center gap-2 text-[13px] font-medium text-white">
            <span class="text-mint-300">${icons.chat}</span>
            HPD product studio
          </span>
          <span class="mt-1 block pl-6 text-[11px] text-white/35">Headless lifecycle review</span>
        </button>
        <button class="w-full rounded-xl px-3 py-3 text-left text-white/45 transition hover:bg-white/[0.035] hover:text-white/75">
          <span class="flex items-center gap-2 text-[13px]">
            ${icons.layers}
            Project Atlas
          </span>
          <span class="mt-1 block pl-6 text-[11px] text-white/25">Planning session</span>
        </button>
      </nav>

      <div class="mt-auto rounded-2xl border border-white/[0.07] bg-white/[0.025] p-3">
        <div class="flex items-center gap-2 text-[11px] font-medium text-white/65">
          <span class="size-1.5 rounded-full bg-mint-400"></span>
          Local simulation
        </div>
        <p class="mt-2 text-[10px] leading-relaxed text-white/30">
          Real AgentClient + headless controller. A local runtime supplies the demo events.
        </p>
      </div>
    </aside>

    <main class="relative flex min-w-0 flex-1 flex-col">
      <header class="flex h-[70px] shrink-0 items-center justify-between border-b border-white/[0.06] px-5 md:px-7">
        <div class="flex min-w-0 items-center gap-3">
          <button class="grid size-9 place-items-center rounded-xl border border-white/[0.07] text-white/55 lg:hidden" aria-label="Open conversations">
            ${icons.chat}
          </button>
          <div class="min-w-0">
            <div class="flex items-center gap-2">
              <h1 class="truncate text-[15px] font-semibold tracking-[-0.01em] text-white">HPD product studio</h1>
              <span id="connection-badge" class="rounded-full border border-amber-300/15 bg-amber-300/[0.07] px-2 py-0.5 text-[9px] font-semibold uppercase tracking-[0.14em] text-amber-200/70">Demo</span>
            </div>
            <p class="mt-0.5 truncate text-[11px] text-white/35">Session demo-session · main thread</p>
          </div>
        </div>
        <div class="flex items-center gap-2">
          <button id="approval-demo" class="hidden rounded-xl border border-white/[0.08] bg-white/[0.025] px-3 py-2 text-[11px] font-medium text-white/55 transition hover:border-mint-300/20 hover:text-mint-200 sm:block">
            Preview approval
          </button>
          <button class="grid size-9 place-items-center rounded-xl border border-white/[0.08] bg-white/[0.025] text-white/45 transition hover:text-white" aria-label="More options">
            <span class="font-mono text-lg leading-none">···</span>
          </button>
        </div>
      </header>

      <section id="timeline" class="min-h-0 flex-1 overflow-y-auto px-5 pb-8 pt-7 md:px-8" aria-live="polite"></section>
      <section id="requests" class="mx-auto w-full max-w-4xl px-5 md:px-8"></section>

      <footer class="shrink-0 px-4 pb-4 pt-2 md:px-8 md:pb-6">
        <div class="mx-auto max-w-4xl">
          <div id="suggestions" class="mb-2 flex gap-2 overflow-x-auto pb-1"></div>
          <form id="composer" class="rounded-[22px] border border-white/[0.1] bg-[#0d1a17]/95 p-2 shadow-[0_18px_70px_rgba(0,0,0,0.3)] backdrop-blur">
            <textarea id="prompt" rows="1" maxlength="4000" placeholder="Ask HPD about your product, code, or plan..."
              class="max-h-40 min-h-[50px] w-full resize-none bg-transparent px-3 py-3 text-[14px] leading-relaxed text-white/90 outline-none placeholder:text-white/25"></textarea>
            <div class="flex items-center justify-between px-1 pb-1">
              <div class="flex items-center gap-1">
                <button type="button" class="grid size-8 place-items-center rounded-lg text-white/35 transition hover:bg-white/[0.05] hover:text-white/70" aria-label="Attach a file">
                  ${icons.paperclip}
                </button>
                <span class="ml-1 hidden text-[10px] text-white/20 sm:inline">Headless UI · demo projection</span>
              </div>
              <div class="flex items-center gap-2">
                <span id="composer-hint" class="hidden text-[10px] text-white/25 sm:inline">Enter to send</span>
                <button id="submit" type="submit" class="grid size-9 place-items-center rounded-xl bg-mint-400 text-ink-950 shadow-[0_0_24px_rgba(94,230,174,0.2)] transition hover:bg-mint-300 disabled:cursor-not-allowed disabled:bg-white/10 disabled:text-white/25 disabled:shadow-none" aria-label="Send message">
                  ${icons.arrow}
                </button>
              </div>
            </div>
          </form>
          <p class="mt-2 text-center text-[9px] tracking-wide text-white/20">HPD models can make mistakes. Review consequential actions.</p>
        </div>
      </footer>
    </main>

    <aside class="hidden w-[286px] shrink-0 border-l border-white/[0.06] bg-black/10 p-5 xl:block">
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-2 text-[12px] font-semibold text-white/70">
          ${icons.activity}
          Live projection
        </div>
        <span class="flex items-center gap-1.5 rounded-full border border-white/[0.06] px-2 py-1 text-[9px] uppercase tracking-[0.12em] text-white/30">
          <span class="size-1.5 rounded-full bg-mint-400"></span>
          Connected
        </span>
      </div>
      <div id="activity" class="mt-5"></div>
    </aside>
  </div>
`;

const timelineElement = document.querySelector<HTMLElement>('#timeline')!;
const requestsElement = document.querySelector<HTMLElement>('#requests')!;
const activityElement = document.querySelector<HTMLElement>('#activity')!;
const suggestionsElement = document.querySelector<HTMLElement>('#suggestions')!;
const composer = document.querySelector<HTMLFormElement>('#composer')!;
const promptInput = document.querySelector<HTMLTextAreaElement>('#prompt')!;
const submitButton = document.querySelector<HTMLButtonElement>('#submit')!;
const composerHint = document.querySelector<HTMLElement>('#composer-hint')!;
const connectionBadge = document.querySelector<HTMLElement>('#connection-badge')!;

let controller: ThreadController | undefined;
let demo: DemoAgentClient | undefined;
let renderQueued = false;
let lastTimelineSignature = '';

const suggestions = [
  'Is the headless UI too opinionated?',
  'Make a launch plan',
  'What does this interface do?',
];

for (const suggestion of suggestions) {
  const button = document.createElement('button');
  button.type = 'button';
  button.className =
    'shrink-0 rounded-full border border-white/[0.07] bg-white/[0.025] px-3 py-1.5 text-[10px] text-white/38 transition hover:border-mint-300/20 hover:text-mint-200';
  button.textContent = suggestion;
  button.addEventListener('click', () => {
    promptInput.value = suggestion;
    promptInput.focus();
    void sendPrompt();
  });
  suggestionsElement.append(button);
}

function element<K extends keyof HTMLElementTagNameMap>(
  tag: K,
  className = '',
  text?: string,
): HTMLElementTagNameMap[K] {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}

function appendRichText(parent: HTMLElement, text: string, className = ''): void {
  const blocks = text.split(/\n\n+/);
  for (const block of blocks) {
    const paragraph = element('p', className);
    paragraph.textContent = block;
    paragraph.classList.add('whitespace-pre-wrap');
    parent.append(paragraph);
  }
}

function formatNumber(value: number | null | undefined): string {
  return new Intl.NumberFormat('en-US').format(value ?? 0);
}

function statusLabel(status: string): string {
  return status === 'working'
    ? 'Working'
    : status === 'requesting'
      ? 'Waiting for you'
      : status === 'failed'
        ? 'Failed'
        : status === 'cancelled'
          ? 'Cancelled'
          : 'Ready';
}

function renderMessage(message: ThreadProjectionSnapshot['transcriptMessages'][number]): HTMLElement {
  if (message.role === 'system' || message.role === 'tool') return element('div');
  const article = element('article', 'message-enter mx-auto mb-7 w-full max-w-4xl');

  if (message.role === 'user') {
    const row = element('div', 'flex justify-end');
    const bubble = element(
      'div',
      'max-w-[82%] rounded-[20px_20px_5px_20px] border border-white/[0.08] bg-white/[0.065] px-4 py-3 text-[13px] leading-relaxed text-white/85 shadow-sm',
    );
    appendRichText(bubble, message.content);
    row.append(bubble);
    article.append(row);
    return article;
  }

  const row = element('div', 'flex items-start gap-3');
  const avatar = element(
    'div',
    'mt-0.5 grid size-8 shrink-0 place-items-center rounded-xl border border-mint-300/20 bg-mint-300/[0.07] text-mint-300',
  );
  avatar.innerHTML = icons.spark;
  const body = element('div', 'min-w-0 flex-1');
  const meta = element('div', 'mb-2 flex items-center gap-2');
  meta.append(
    element('span', 'text-[11px] font-semibold tracking-wide text-white/70', message.authorName ?? 'HPD Agent'),
    element('span', 'size-0.5 rounded-full bg-white/20'),
    element('span', 'text-[10px] text-white/25', message.streaming ? 'responding' : 'HPD demo'),
  );
  body.append(meta);
  appendRichText(body, message.content, 'mb-3 text-[13.5px] leading-[1.75] text-white/80');
  if (message.streaming) {
    const cursor = element('span', 'ml-1 inline-block h-4 w-1 animate-pulse rounded-full bg-mint-300');
    body.lastElementChild?.append(cursor);
  }
  row.append(avatar, body);
  article.append(row);
  return article;
}

function renderTool(tool: ToolCall): HTMLElement {
  const details = element(
    'details',
    'group rounded-xl border border-white/[0.06] bg-black/15 px-3 py-2.5',
  );
  const summary = element('summary', 'flex cursor-pointer list-none items-center gap-2 text-[11px]');
  const statusDot = element(
    'span',
    tool.status === 'complete'
      ? 'size-1.5 rounded-full bg-mint-400'
      : tool.status === 'error'
        ? 'size-1.5 rounded-full bg-red-400'
        : 'pulse-dot size-1.5 rounded-full bg-amber-300',
  );
  const iconSpan = element('span', 'text-white/30');
  iconSpan.innerHTML = icons.tool;
  summary.append(
    statusDot,
    iconSpan,
    element('span', 'font-mono text-white/55', tool.name),
    element('span', 'ml-auto text-[9px] uppercase tracking-wider text-white/25', tool.status),
  );
  const body = element('div', 'mt-3 space-y-2 border-t border-white/[0.05] pt-3 text-[10px]');
  body.append(
    element('div', 'font-semibold uppercase tracking-[0.12em] text-white/25', 'Arguments'),
    element(
      'pre',
      'overflow-x-auto rounded-lg bg-black/25 p-2 font-mono leading-relaxed text-white/45',
      JSON.stringify(tool.args ?? {}, null, 2),
    ),
  );
  if (tool.resultText) {
    body.append(
      element('div', 'pt-1 font-semibold uppercase tracking-[0.12em] text-white/25', 'Result'),
      element('p', 'leading-relaxed text-white/50', tool.resultText),
    );
  }
  details.append(summary, body);
  return details;
}

function renderWork(work: ThreadWorkGroup): HTMLElement {
  const details = element(
    'details',
    'message-enter mx-auto mb-6 w-full max-w-4xl overflow-hidden rounded-2xl border border-white/[0.07] bg-[#0b1714]/70',
  );
  if (work.status === 'working') details.open = true;
  const summary = element('summary', 'flex cursor-pointer list-none items-center gap-3 px-4 py-3');
  const status = element(
    'span',
    work.status === 'working'
      ? 'pulse-dot size-2 rounded-full bg-amber-300'
      : work.status === 'worked'
        ? 'size-2 rounded-full bg-mint-400'
        : work.status === 'failed'
          ? 'size-2 rounded-full bg-red-400'
          : 'size-2 rounded-full bg-white/30',
  );
  const label = element('span', 'text-[11px] font-medium text-white/55', work.label || 'Agent work');
  const partsCount = element(
    'span',
    'rounded-full bg-white/[0.04] px-2 py-0.5 text-[9px] uppercase tracking-[0.12em] text-white/25',
    `${work.parts.length} steps`,
  );
  const chevron = element('span', 'ml-auto text-white/20 transition group-open:rotate-90');
  chevron.innerHTML = icons.chevron;
  summary.append(status, label, partsCount, chevron);

  const content = element('div', 'space-y-3 border-t border-white/[0.05] px-4 py-4');
  for (const part of work.parts) {
    if (part.type === 'reasoning') {
      const reasoning = element('details', 'rounded-xl border border-white/[0.05] bg-black/10 px-3 py-2.5');
      const reasoningSummary = element('summary', 'flex cursor-pointer list-none items-center gap-2 text-[10px] text-white/35');
      const brain = element('span');
      brain.innerHTML = icons.brain;
      reasoningSummary.append(brain, document.createTextNode('Reasoning'), element('span', 'ml-auto uppercase tracking-wider text-white/20', part.status));
      reasoning.append(reasoningSummary, element('p', 'mt-2 text-[11px] leading-relaxed text-white/38', part.text));
      content.append(reasoning);
    } else if (part.type === 'tool') {
      content.append(renderTool(part.tool));
    } else if (part.type === 'assistant-draft') {
      const draft = element('div', 'rounded-xl border-l-2 border-mint-300/40 bg-mint-300/[0.025] px-3 py-2');
      draft.append(
        element('div', 'mb-1 text-[9px] uppercase tracking-[0.14em] text-mint-300/45', 'Draft response'),
        element('p', 'whitespace-pre-wrap text-[11px] leading-relaxed text-white/55', part.message.content),
      );
      content.append(draft);
    } else if (part.type === 'progress' || part.type === 'hook' || part.type === 'warning') {
      content.append(
        element(
          'div',
          'rounded-lg border border-white/[0.05] px-3 py-2 text-[10px] text-white/35',
          part.type === 'warning' ? part.message : part.label,
        ),
      );
    }
  }
  if (content.childElementCount === 0) {
    content.append(element('div', 'text-[10px] text-white/25', 'No inspectable work recorded.'));
  }
  details.append(summary, content);
  return details;
}

function renderTimelineItem(item: ThreadTimelineItem): HTMLElement {
  if (item.type === 'message') return renderMessage(item.message);
  if (item.type === 'work') return renderWork(item.work);
  if (item.type === 'progress') {
    return element('div', 'mx-auto mb-4 max-w-4xl text-[10px] text-white/30', item.label);
  }
  if (item.type === 'warning') {
    return element('div', 'mx-auto mb-4 max-w-4xl rounded-xl border border-amber-300/10 bg-amber-300/[0.04] p-3 text-[11px] text-amber-100/60', item.message);
  }
  return element('div');
}

function renderRequest(request: RuntimeRequest): HTMLElement {
  const card = element(
    'section',
    'message-enter mb-3 rounded-2xl border border-amber-300/15 bg-amber-200/[0.045] p-4 shadow-[0_16px_50px_rgba(0,0,0,0.18)]',
  );
  const header = element('div', 'flex items-start gap-3');
  const iconBox = element(
    'div',
    'grid size-9 shrink-0 place-items-center rounded-xl border border-amber-200/15 bg-amber-200/[0.06] text-amber-200/70',
  );
  iconBox.innerHTML = icons.shield;
  const copy = element('div', 'min-w-0 flex-1');
  copy.append(
    element('div', 'text-[12px] font-semibold text-amber-50/85', 'Permission required'),
  );

  if (request.kind === 'permission') {
    copy.append(
      element(
        'p',
        'mt-1 text-[11px] leading-relaxed text-amber-50/45',
        `The agent wants to run ${request.request.functionName ?? 'a protected operation'}. This request blocks normal text submission.`,
      ),
    );
    const actions = element('div', 'mt-3 flex flex-wrap gap-2');
    for (const choice of request.request.choices) {
      const allow = choice.kind === 'Allow';
      const button = element(
        'button',
        allow
          ? 'rounded-lg bg-amber-100 px-3 py-1.5 text-[10px] font-semibold text-amber-950 transition hover:bg-white'
          : 'rounded-lg border border-white/[0.08] px-3 py-1.5 text-[10px] text-white/45 transition hover:text-white',
        choice.label,
      );
      button.type = 'button';
      button.addEventListener('click', () => {
        if (!controller) return;
        void (allow ? controller.approve(request.id, choice.id) : controller.deny(request.id));
      });
      actions.append(button);
    }
    copy.append(actions);
  } else {
    copy.append(
      element(
        'p',
        'mt-1 text-[11px] leading-relaxed text-amber-50/45',
        `A ${request.kind} request is waiting for a response.`,
      ),
    );
  }
  header.append(iconBox, copy);
  card.append(header);
  return card;
}

function renderActivity(snapshot: ThreadProjectionSnapshot): void {
  const usage = snapshot.contextUsage?.usage;
  const totalTokens = usage?.totalTokenCount ?? 0;
  activityElement.replaceChildren();

  const statusCard = element('div', 'rounded-2xl border border-white/[0.07] bg-white/[0.025] p-4');
  const statusTop = element('div', 'flex items-center justify-between');
  statusTop.append(
    element('span', 'text-[11px] font-medium text-white/55', 'Agent status'),
    element('span', 'rounded-full bg-mint-300/[0.07] px-2 py-1 text-[9px] font-semibold uppercase tracking-[0.12em] text-mint-300/65', statusLabel(snapshot.activity.status)),
  );
  statusCard.append(statusTop);
  const meter = element('div', 'mt-4 h-1 overflow-hidden rounded-full bg-white/[0.05]');
  const fill = element('div', 'h-full rounded-full bg-gradient-to-r from-mint-500 to-mint-300 transition-all duration-500');
  fill.style.width = snapshot.activity.status === 'idle' ? '24%' : '72%';
  meter.append(fill);
  statusCard.append(meter);
  activityElement.append(statusCard);

  const stats = element('div', 'mt-3 grid grid-cols-2 gap-2');
  const stat = (label: string, value: string) => {
    const card = element('div', 'rounded-xl border border-white/[0.06] bg-white/[0.02] p-3');
    card.append(
      element('div', 'text-[9px] uppercase tracking-[0.12em] text-white/25', label),
      element('div', 'mt-1 font-mono text-[17px] text-white/75', value),
    );
    return card;
  };
  stats.append(
    stat('Tools', String(getActiveToolCalls(snapshot).length)),
    stat('Requests', String(getPendingRuntimeRequests(snapshot).length)),
    stat('Work groups', String(snapshot.workGroups.length)),
    stat('Tokens', formatNumber(totalTokens)),
  );
  activityElement.append(stats);

  const architecture = element('div', 'mt-4 rounded-2xl border border-white/[0.06] bg-white/[0.018] p-4');
  architecture.append(
    element('div', 'text-[10px] font-semibold uppercase tracking-[0.14em] text-white/30', 'Runtime parts'),
  );
  const rows = [
    ['AgentClient', 'SDK contract'],
    ['ThreadController', 'Lifecycle'],
    ['Projection', 'Timeline fold'],
    ['Tailwind', 'Presentation'],
  ];
  for (const [name, role] of rows) {
    const row = element('div', 'mt-3 flex items-center gap-2.5');
    const dot = element('span', name === 'Tailwind' ? 'size-1.5 rounded-full bg-sky-300' : 'size-1.5 rounded-full bg-mint-400');
    row.append(dot, element('span', 'font-mono text-[10px] text-white/48', name), element('span', 'ml-auto text-[9px] text-white/20', role));
    architecture.append(row);
  }
  activityElement.append(architecture);

  if (snapshot.error) {
    const error = element('div', 'mt-3 rounded-xl border border-red-300/10 bg-red-300/[0.04] p-3 text-[10px] leading-relaxed text-red-100/55', snapshot.error);
    activityElement.append(error);
  }
}

function renderNow(): void {
  if (!controller) return;
  const node = controller.projection.getSnapshot();
  const wasNearBottom =
    timelineElement.scrollHeight - timelineElement.scrollTop - timelineElement.clientHeight < 180;
  const timeline = getThreadTimeline(node, { runtimeRequests: 'exclude' });
  const signature = `${timeline.length}:${node.activity.status}:${node.transcriptMessages.map((message) => message.content.length).join(',')}:${node.workGroups.map((work) => `${work.status}:${work.parts.length}`).join(',')}:${node.pendingRuntimeRequests.length}`;

  if (signature !== lastTimelineSignature) {
    lastTimelineSignature = signature;
    timelineElement.replaceChildren();
    if (timeline.length === 0) {
      const empty = element('div', 'mx-auto mt-[12vh] max-w-md text-center');
      const orb = element('div', 'mx-auto grid size-16 place-items-center rounded-3xl border border-mint-300/15 bg-mint-300/[0.05] text-mint-300');
      orb.innerHTML = icons.spark;
      empty.append(
        orb,
        element('h2', 'mt-5 text-2xl font-semibold tracking-[-0.03em] text-white', 'A clearer view of agent work'),
        element('p', 'mt-2 text-sm leading-relaxed text-white/35', 'Start a thread. The headless UI will separate settled answers from live reasoning, tools, and approvals.'),
      );
      timelineElement.append(empty);
    } else {
      for (const item of timeline) timelineElement.append(renderTimelineItem(item));
    }
    if (wasNearBottom) {
      requestAnimationFrame(() => timelineElement.scrollTo({ top: timelineElement.scrollHeight, behavior: 'smooth' }));
    }
  }

  requestsElement.replaceChildren();
  for (const request of node.pendingRuntimeRequests) requestsElement.append(renderRequest(request));

  const submission = getTextSubmissionState(node);
  promptInput.disabled = !submission.canSubmit;
  submitButton.disabled = !submission.canSubmit || promptInput.value.trim().length === 0;
  composerHint.textContent =
    submission.reason === 'runtime-request'
      ? 'Answer the request first'
      : submission.reason === 'busy'
        ? 'Agent is working'
        : submission.reason === 'error'
          ? 'Clear the error first'
          : 'Enter to send';
  composer.classList.toggle('ring-1', submission.reason === 'runtime-request');
  composer.classList.toggle('ring-amber-300/15', submission.reason === 'runtime-request');
  renderActivity(node);
}

function scheduleRender(): void {
  if (renderQueued) return;
  renderQueued = true;
  requestAnimationFrame(() => {
    renderQueued = false;
    renderNow();
  });
}

async function sendPrompt(): Promise<void> {
  if (!controller) return;
  const text = promptInput.value.trim();
  if (!text) return;
  promptInput.value = '';
  resizePrompt();
  scheduleRender();
  try {
    await controller.sendMessage({
      contents: [{ $type: 'text', text }],
    });
  } catch (error) {
    console.error(error);
    composerHint.textContent = error instanceof Error ? error.message : 'Message failed';
  }
}

function resizePrompt(): void {
  promptInput.style.height = 'auto';
  promptInput.style.height = `${Math.min(promptInput.scrollHeight, 160)}px`;
  submitButton.disabled =
    !promptInput.value.trim() ||
    (controller ? !getTextSubmissionState(controller.projection.getSnapshot()).canSubmit : true);
}

composer.addEventListener('submit', (event) => {
  event.preventDefault();
  void sendPrompt();
});

promptInput.addEventListener('input', resizePrompt);
promptInput.addEventListener('keydown', (event) => {
  if (event.key === 'Enter' && !event.shiftKey) {
    event.preventDefault();
    void sendPrompt();
  }
});

document.querySelector('#approval-demo')?.addEventListener('click', () => {
  void demo?.requestApproval();
});

document.querySelector('#new-chat')?.addEventListener('click', () => {
  globalThis.location.href = globalThis.location.pathname;
});

async function connectDemo(): Promise<void> {
  const environment = createDemoAgentClient();
  demo = environment;
  controller = createThreadController({
    client: environment.client,
    agentId: 'hpd-demo',
    sessionId: 'demo-session',
    threadId: 'main',
  });
  controller.projection.subscribe(scheduleRender);
  await controller.start({ includeExecutions: true });
  connectionBadge.textContent = 'Demo';
  connectionBadge.className =
    'rounded-full border border-amber-300/15 bg-amber-300/[0.07] px-2 py-0.5 text-[9px] font-semibold uppercase tracking-[0.14em] text-amber-200/70';
}

async function connectLive(baseUrl: string, agentId: string): Promise<void> {
  const client = new AgentClient({ baseUrl });
  const { session } = await client.sessions.create({ agentId });
  controller = createThreadController({
    client,
    agentId,
    sessionId: session.id,
    threadId: 'main',
  });
  controller.projection.subscribe(scheduleRender);
  await controller.start({ includeExecutions: true });
  connectionBadge.textContent = 'Live';
  connectionBadge.className =
    'rounded-full border border-mint-300/15 bg-mint-300/[0.07] px-2 py-0.5 text-[9px] font-semibold uppercase tracking-[0.14em] text-mint-300/70';
}

async function boot(): Promise<void> {
  const parameters = new URLSearchParams(globalThis.location.search);
  const baseUrl = parameters.get('base') ?? import.meta.env.VITE_HPD_BASE_URL;
  const agentId = parameters.get('agent') ?? import.meta.env.VITE_HPD_AGENT_ID ?? 'assistant';
  try {
    if (baseUrl) await connectLive(baseUrl, agentId);
    else await connectDemo();
    (globalThis as typeof globalThis & {
      __hpdDemo?: { getSnapshot: () => ThreadProjectionSnapshot };
    }).__hpdDemo = {
      getSnapshot: () => controller!.projection.getSnapshot(),
    };
    renderNow();
    promptInput.focus();
  } catch (error) {
    console.error(error);
    const detail = error instanceof HpdApiError ? `${error.code}: ${error.message}` : String(error);
    timelineElement.replaceChildren(
      element('div', 'mx-auto mt-24 max-w-md rounded-2xl border border-red-300/10 bg-red-300/[0.04] p-5 text-sm text-red-100/60', `Could not connect to the agent. ${detail}`),
    );
    connectionBadge.textContent = 'Offline';
  }
}

globalThis.addEventListener('beforeunload', () => {
  void controller?.dispose();
});

void boot();
