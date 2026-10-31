import { RenderMode, ServerRoute } from '@angular/ssr';

const privateCapabilityHeaders = {
  "Cache-Control": "no-store, private",
  "X-Robots-Tag": "noindex, nofollow",
  "Referrer-Policy": "no-referrer",
} as const;

export const serverRoutes: ServerRoute[] = [
  { path: "pay", renderMode: RenderMode.Client, headers: { ...privateCapabilityHeaders } },
  { path: "documents/**", renderMode: RenderMode.Client, headers: { ...privateCapabilityHeaders } },
  { path: "app/**", renderMode: RenderMode.Client, headers: { ...privateCapabilityHeaders } },
  {
    path: "businesses/**",
    renderMode: RenderMode.Server,
  },
  {
    path: "**",
    renderMode: RenderMode.Server,
  },
];
