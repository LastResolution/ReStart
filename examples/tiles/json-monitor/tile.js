// Connect any app that writes JSON or exposes a local read-only HTTP endpoint.
// Expected fields: text, detail, progress (optional fraction 0..1).
function render(ctx) {
  const labels = ctx.settings.labels[ctx.language] || ctx.settings.labels.en;
  const source = ctx.data?.service, value = source?.value, small = ctx.size === "small";
  const fallback = source?.status === "loading" ? labels.loading : source?.status === "error" ? labels.unavailable : labels.connect;
  const supplied = value == null ? null : typeof value === "object" ? value.text ?? JSON.stringify(value) : value;
  return { text: String(supplied ?? (small ? "…" : fallback)).slice(0, 256), detail: small ? "" : String(value?.detail ?? "").slice(0, 512),
    kicker: small ? "" : "JSON", badge: small || !source?.stale ? "" : labels.cached,
    progress: Number.isFinite(value?.progress) ? Math.max(0, Math.min(1, value.progress)) : undefined,
    layout: small || ctx.size === "medium" ? "centered" : "overlay", background: ctx.settings.color, backgroundEnd: ctx.settings.colorEnd,
    foreground: "#FFFFFF", accent: ctx.settings.accent };
}
