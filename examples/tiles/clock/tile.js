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
  const twelve = ctx.event?.type === "click" ? !ctx.state?.twelve : !!ctx.state?.twelve;
  const h = twelve ? now.getHours() % 12 || 12 : now.getHours();
  const digits = pad(h) + pad(now.getMinutes()) + pad(now.getSeconds());
  return styled(ctx, { text: pad(h) + ":" + pad(now.getMinutes()) + (ctx.size === "small" || ctx.size === "medium" ? "" : ":" + pad(now.getSeconds())), kicker: labels(ctx).localTime,
    detail: ctx.calendar?.shortDate || ctx.now.slice(0, 10), visual: "bars", series: digits.split("").map(value => Number(value) / 9), state: { twelve } });
}
