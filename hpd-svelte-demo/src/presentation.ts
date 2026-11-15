export const themePresets = [
  {
    id: 'studio',
    label: 'Studio',
    accent: '#d8ff70',
    page: '#f5f4ef',
    shell: '#fbfaf7',
    panel: '#f0efe9',
    sidebar: '#17211d',
  },
  {
    id: 'ocean',
    label: 'Ocean',
    accent: '#83e4f6',
    page: '#edf5f7',
    shell: '#f9fdfe',
    panel: '#e4f0f3',
    sidebar: '#102b33',
  },
  {
    id: 'clay',
    label: 'Clay',
    accent: '#ffc978',
    page: '#f8f1ea',
    shell: '#fffaf5',
    panel: '#f1e7dd',
    sidebar: '#3a251d',
  },
  {
    id: 'graphite',
    label: 'Graphite',
    accent: '#c7ff57',
    page: '#f0f0ec',
    shell: '#fafaf7',
    panel: '#e8e8e2',
    sidebar: '#161915',
  },
] as const;

export type ThemeId = typeof themePresets[number]['id'];
export type Density = 'comfortable' | 'compact';
export type Shape = 'soft' | 'sharp';
export type MessageStyle = 'bubbles' | 'document';

export interface PresentationConfig {
  theme: ThemeId;
  density: Density;
  shape: Shape;
  messageStyle: MessageStyle;
}

export const defaultPresentationConfig: PresentationConfig = {
  theme: 'studio',
  density: 'comfortable',
  shape: 'soft',
  messageStyle: 'bubbles',
};

const storageKey = 'hpd-svelte-demo.presentation.v1';

export function getTheme(theme: ThemeId) {
  return themePresets.find((preset) => preset.id === theme) ?? themePresets[0];
}

export function presentationStyle(theme: ThemeId): string {
  const selected = getTheme(theme);
  return [
    `--hpd-accent:${selected.accent}`,
    `--hpd-page:${selected.page}`,
    `--hpd-shell:${selected.shell}`,
    `--hpd-panel:${selected.panel}`,
    `--hpd-sidebar:${selected.sidebar}`,
  ].join(';');
}

export function loadPresentationConfig(): PresentationConfig {
  try {
    const parsed = JSON.parse(globalThis.localStorage.getItem(storageKey) ?? 'null') as
      Partial<PresentationConfig> | null;
    if (!parsed) return defaultPresentationConfig;
    return {
      theme: themePresets.some((preset) => preset.id === parsed.theme)
        ? parsed.theme as ThemeId
        : defaultPresentationConfig.theme,
      density: parsed.density === 'compact' ? 'compact' : defaultPresentationConfig.density,
      shape: parsed.shape === 'sharp' ? 'sharp' : defaultPresentationConfig.shape,
      messageStyle: parsed.messageStyle === 'document'
        ? 'document'
        : defaultPresentationConfig.messageStyle,
    };
  } catch {
    return defaultPresentationConfig;
  }
}

export function savePresentationConfig(config: PresentationConfig): void {
  try {
    globalThis.localStorage.setItem(storageKey, JSON.stringify(config));
  } catch {
    // Presentation persistence is best effort.
  }
}
