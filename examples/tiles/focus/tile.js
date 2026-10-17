// Local, deterministic display data. All drawing is performed by ReStart's native renderer.
function labels(ctx) {
  const entries = ctx.settings.labels || {};
  return entries[ctx.language] || entries.en || {};
}
function styled(ctx, value) {
  if (ctx.size === "small") {
    // Compact square tiles show one value; two clock rows avoid clipped digits.
    if (/^\d{1,3}:\d{2}/.test(value.text)) value.text = value.text.split(":").slice(0, 2).join("\n");
    if (/^\d+(\.\d+)?%$/.test(value.text)) value.text = Math.round(parseFloat(value.text)) + "%";
  }
  if (ctx.size === "medium" && value.text.includes("\n")) value.detail = "";
  return Object.assign({ background: ctx.settings.color, backgroundEnd: ctx.settings.colorEnd,
    foreground: "#FFFFFF", accent: ctx.settings.accent,
    // The frame layout is native, bounded and independent of HTML/CSS.
    layout: ctx.size === "small" || ctx.size === "medium" ? "centered" : ctx.size === "large" ? "stacked" : "split" },
    value, ctx.size === "small" ? { kicker: "", detail: "", badge: "", visual: "", series: [] } : {});
}
function pad(value) { return String(value).padStart(2, "0"); }
function render(ctx) {
  const tick = ctx.time?.monotonicMs ?? Date.parse(ctx.now);
  let start = ctx.state?.start ?? tick;
  let pausedAt = ctx.state?.pausedAt ?? null;
  if (ctx.event?.type === "click") {
    if (pausedAt == null) pausedAt = tick;
    else { start += tick - pausedAt; pausedAt = null; }
  }
  const focus = Math.max(1, Math.min(180, Number(ctx.settings.focusMinutes) || 25)) * 60;
  const rest = Math.max(1, Math.min(60, Number(ctx.settings.restMinutes) || 5)) * 60;
  const elapsed = Math.floor(Math.max(0, (pausedAt ?? tick) - start) / 1000) % (focus + rest);
  const resting = elapsed >= focus;
  const span = resting ? rest : focus;
  const remaining = span - (resting ? elapsed - focus : elapsed);
  return styled(ctx, { text: pad(Math.floor(remaining / 60)) + ":" + pad(remaining % 60), kicker: labels(ctx).caption,
    detail: pausedAt != null ? labels(ctx).paused : resting ? labels(ctx).rest : labels(ctx).focus,
    visual: "ring", progress: 1 - remaining / span, state: { start, pausedAt } });
}
