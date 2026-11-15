import {
  CopilotChat,
  CopilotKitProvider,
  useDefaultRenderTool,
} from '@copilotkit/react-core/v2';
import {
  CopilotChatConfigurationProvider,
} from '@copilotkit/react-core/v2/headless';
import { DemoAgent } from './demo-agent';

const demoAgent = new DemoAgent();

function DefaultToolRenderer() {
  useDefaultRenderTool();
  return null;
}

function Mark({ children }: { children: string }) {
  return (
    <span className="rounded-md border border-violet-300/15 bg-violet-300/[0.07] px-1.5 py-0.5 font-mono text-[10px] text-violet-200/75">
      {children}
    </span>
  );
}

export default function App() {
  return (
    <div className="agui-shell dark relative flex h-screen min-h-[680px] overflow-hidden bg-[#0f1020]/70">
      <div className="agui-noise" />

      <aside className="hidden w-[318px] shrink-0 flex-col border-r border-white/[0.07] bg-[#0b0c18]/75 px-6 py-6 lg:flex">
        <div className="flex items-center gap-3">
          <div className="relative grid size-10 place-items-center rounded-2xl border border-violet-300/20 bg-violet-400/10">
            <span className="absolute -right-1 -top-1 size-3 rounded-full border-2 border-[#0b0c18] bg-cyan-300" />
            <svg
              aria-hidden="true"
              width="22"
              height="22"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="1.7"
              className="text-violet-200"
            >
              <path d="M12 3v3M12 18v3M3 12h3M18 12h3M5.6 5.6l2.1 2.1M16.3 16.3l2.1 2.1M18.4 5.6l-2.1 2.1M7.7 16.3l-2.1 2.1" />
              <circle cx="12" cy="12" r="4.25" />
            </svg>
          </div>
          <div>
            <div className="text-[14px] font-semibold tracking-[-0.01em] text-white">
              AG-UI × CopilotKit
            </div>
            <div className="mt-0.5 text-[10px] uppercase tracking-[0.17em] text-white/30">
              Protocol to product
            </div>
          </div>
        </div>

        <div className="mt-8 rounded-2xl border border-white/[0.07] bg-white/[0.025] p-4">
          <div className="flex items-center justify-between">
            <span className="text-[10px] font-semibold uppercase tracking-[0.16em] text-white/35">
              Local run
            </span>
            <span className="flex items-center gap-1.5 rounded-full bg-emerald-300/[0.07] px-2 py-1 text-[9px] font-semibold uppercase tracking-[0.12em] text-emerald-200/65">
              <span className="size-1.5 rounded-full bg-emerald-300" />
              Active
            </span>
          </div>
          <p className="mt-3 text-[11px] leading-relaxed text-white/38">
            CopilotKit is consuming the actual event stream emitted by an AG-UI
            <span className="text-white/60"> AbstractAgent</span>.
          </p>
          <div className="mt-3 flex flex-wrap gap-1.5">
            <Mark>AG-UI 1.0</Mark>
            <Mark>CopilotKit 1.88</Mark>
            <Mark>React</Mark>
          </div>
        </div>

        <div className="mt-7">
          <div className="text-[10px] font-semibold uppercase tracking-[0.17em] text-white/25">
            What this shows
          </div>
          <div className="mt-3 space-y-3">
            {[
              ['01', 'Protocol', 'AG-UI emits reasoning, tool, text, and run lifecycle events.'],
              ['02', 'Provider', 'CopilotKit owns the registered agent and React state.'],
              ['03', 'Surface', 'The prebuilt CopilotChat controls the interactions and styling.'],
            ].map(([number, title, body]) => (
              <div
                key={number}
                className="rounded-xl border border-white/[0.055] bg-white/[0.018] p-3"
              >
                <div className="flex items-center gap-2">
                  <span className="font-mono text-[9px] text-violet-300/45">{number}</span>
                  <span className="text-[11px] font-medium text-white/65">{title}</span>
                </div>
                <p className="mt-1.5 text-[10px] leading-relaxed text-white/30">{body}</p>
              </div>
            ))}
          </div>
        </div>

        <div className="mt-auto rounded-xl border border-white/[0.055] px-3 py-3">
          <div className="text-[9px] uppercase tracking-[0.15em] text-white/22">Immediate impression</div>
          <p className="mt-1.5 text-[10px] leading-relaxed text-white/38">
            Much faster to a complete UI. More framework shape and less direct ownership
            than HPD’s headless projection.
          </p>
          <a
            href="http://127.0.0.1:4173/"
            className="mt-3 inline-flex items-center gap-1 text-[10px] font-medium text-violet-200/65 transition hover:text-violet-100"
          >
            Open HPD comparison
            <span aria-hidden="true">↗</span>
          </a>
        </div>
      </aside>

      <main className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-[68px] shrink-0 items-center justify-between border-b border-white/[0.06] px-5 md:px-7">
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-[14px] font-semibold text-white/85">CopilotKit prebuilt chat</h1>
              <span className="rounded-full border border-violet-300/15 bg-violet-300/[0.06] px-2 py-0.5 text-[8px] font-semibold uppercase tracking-[0.14em] text-violet-200/60">
                Their UI
              </span>
            </div>
            <p className="mt-1 text-[10px] text-white/28">
              Local AbstractAgent · thread agui-demo
            </p>
          </div>
          <div className="hidden items-center gap-2 text-[10px] text-white/30 sm:flex">
            <span className="size-1.5 rounded-full bg-cyan-300" />
            Streaming through AG-UI events
          </div>
        </header>

        <div className="agui-chat min-h-0 flex-1 overflow-hidden p-3 md:p-5">
          <CopilotKitProvider
            agents__unsafe_dev_only={{ default: demoAgent }}
            agentId="default"
            showDevConsole={false}
            enableInspector={false}
          >
            <CopilotChatConfigurationProvider agentId="default" threadId="agui-demo">
              <DefaultToolRenderer />
              <CopilotChat
                className="h-full overflow-hidden rounded-[26px] border border-white/[0.08] bg-white/[0.025] shadow-[0_24px_90px_rgba(0,0,0,0.34)]"
                labels={{
                  modalHeaderTitle: 'AG-UI product copilot',
                  welcomeMessageText: 'Ask this local AG-UI agent anything.',
                  chatInputPlaceholder: 'Compare the agent protocol and UI layers...',
                }}
                welcomeScreen={false}
              />
            </CopilotChatConfigurationProvider>
          </CopilotKitProvider>
        </div>
      </main>

      <aside className="hidden w-[270px] shrink-0 border-l border-white/[0.06] bg-[#0b0c18]/55 p-5 xl:block">
        <div className="text-[10px] font-semibold uppercase tracking-[0.17em] text-white/28">
          Protocol stack
        </div>
        <div className="mt-4 overflow-hidden rounded-2xl border border-white/[0.07] bg-white/[0.025]">
          {[
            ['CopilotChat', 'React surface', 'bg-violet-300'],
            ['Agent state', 'Messages + tools', 'bg-cyan-300'],
            ['CopilotKit', 'React bindings', 'bg-violet-300/70'],
            ['AG-UI events', 'Wire contract', 'bg-cyan-300/70'],
            ['DemoAgent', 'Local producer', 'bg-white/45'],
          ].map(([name, role, color], index, all) => (
            <div
              key={name}
              className={`flex items-center gap-3 px-3 py-3 ${index < all.length - 1 ? 'border-b border-white/[0.055]' : ''}`}
            >
              <span className={`size-1.5 rounded-full ${color}`} />
              <div className="min-w-0">
                <div className="font-mono text-[10px] text-white/58">{name}</div>
                <div className="mt-0.5 text-[9px] text-white/24">{role}</div>
              </div>
            </div>
          ))}
        </div>

        <div className="mt-5 rounded-2xl border border-violet-300/10 bg-violet-300/[0.035] p-4">
          <div className="text-[10px] font-semibold text-violet-100/60">Try in chat</div>
          <ul className="mt-3 space-y-2.5 text-[10px] leading-relaxed text-white/36">
            <li>“Compare AG-UI and CopilotKit.”</li>
            <li>“Is this more opinionated?”</li>
            <li>“Show me the tool call flow.”</li>
          </ul>
        </div>

        <div className="mt-5 border-t border-white/[0.055] pt-4">
          <div className="text-[9px] uppercase tracking-[0.15em] text-white/22">Compared with HPD</div>
          <p className="mt-2 text-[10px] leading-relaxed text-white/34">
            CopilotKit gives a polished surface immediately. HPD gives more direct
            control over timeline semantics and avoids taking a React UI dependency.
          </p>
        </div>
      </aside>
    </div>
  );
}
