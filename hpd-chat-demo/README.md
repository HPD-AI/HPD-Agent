# HPD Chat Lab

A standalone Vite and TypeScript chat interface built with:

- `@hpd-research/hpd-agent-client`
- `@hpd-research/hpd-agent-headless-ui`
- Tailwind CSS

The default mode uses a local event simulation but exercises the real headless
thread controller and projection. To connect the same UI to a hosted HPD Agent:

```text
http://127.0.0.1:4173/?base=http://localhost:5135&agent=assistant
```

You can also set `VITE_HPD_BASE_URL` and `VITE_HPD_AGENT_ID`.

## Run

```bash
pnpm install
pnpm dev
```

If Node is not installed system-wide, run:

```bash
./run.sh
```
