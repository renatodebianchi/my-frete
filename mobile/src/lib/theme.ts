export const theme = {
  colors: {
    brand: '#0F766E',
    brandFg: '#FFFFFF',
    bg: '#FFFFFF',
    text: '#0A0A0A',
    muted: '#6B7280',
    border: '#E5E7EB',
    danger: '#DC2626',
    success: '#059669',
  },
  spacing: (n: number) => n * 4,
  radius: { sm: 6, md: 10, lg: 16, full: 9999 },
} as const;

export type Theme = typeof theme;
