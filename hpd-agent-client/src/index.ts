// Main client
export { AgentClient } from './client.js';
export type {
  AgentClientConfig,
  EventHandlers,
  PermissionResponse,
  StreamOptions,
  TransportType,
} from './client.js';

// Types
export * from './types/index.js';

// Branching utilities
export * from './branching.js';

// Transports (for advanced usage)
export { SseTransport, WebSocketTransport } from './transports/index.js';

// Parser (for advanced usage)
export { SseParser } from './parser.js';
