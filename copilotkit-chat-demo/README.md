# AG-UI × CopilotKit Chat Demo

A React comparison demo that uses:

- an actual AG-UI `AbstractAgent` as the event producer;
- CopilotKit’s v2 React provider and prebuilt `CopilotChat`;
- Tailwind for the surrounding product shell.

The agent is local and deterministic, so the demo runs without an API key or
backend. It emits AG-UI reasoning, tool-call, text-stream, and run lifecycle
events.

```bash
./run.sh
```

Then open <http://127.0.0.1:4174/>.
