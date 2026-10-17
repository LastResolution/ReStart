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
  const available = !!ctx.slideshow?.available;
  return styled(ctx, { layout: ctx.size === "small" ? "centered" : "caption", text: available ? (ctx.size === "small" ? String(ctx.slideshow.index + 1) : ctx.size === "medium" ? (ctx.slideshow.index + 1) + " / " + ctx.slideshow.count : labels(ctx).caption) : "＋", image: available ? "current" : "",
    detail: ctx.size === "medium" ? "" : available ? (ctx.slideshow.index + 1) + " / " + ctx.slideshow.count : labels(ctx).choose });
}
