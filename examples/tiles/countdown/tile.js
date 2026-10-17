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
  const now = new Date(ctx.now);
  const requested = ctx.settings.target ? new Date(ctx.settings.target) : new Date(now.getFullYear() + 1, 0, 1);
  const target = Number.isFinite(requested.getTime()) ? requested : new Date(now.getFullYear() + 1, 0, 1);
  const seconds = Math.max(0, Math.ceil((target - now) / 1000));
  const days = Math.floor(seconds / 86400);
  return styled(ctx, { text: ctx.size === "small" || ctx.size === "medium" ? String(days) : days + " " + labels(ctx).days, kicker: labels(ctx).caption,
    detail: target.getFullYear() + "-" + pad(target.getMonth() + 1) + "-" + pad(target.getDate()), visual: "ring", progress: 1 - (seconds % 86400) / 86400 });
}
