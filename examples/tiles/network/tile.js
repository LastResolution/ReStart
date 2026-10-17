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
function rate(value) {
  return value >= 1048576 ? (value / 1048576).toFixed(1) + " MiB/s" : (value / 1024).toFixed(0) + " KiB/s";
}
function render(ctx) {
  const down = ctx.system?.networkReceiveBytesPerSecond, up = ctx.system?.networkSendBytesPerSecond;
  const sending = ctx.event?.type === "click" ? !ctx.state?.sending : !!ctx.state?.sending;
  const history = Array.isArray(ctx.state?.history) ? ctx.state.history.slice(-23) : [];
  if (down != null) history.push(Math.min(1, Math.log10(1 + down) / 8));
  return styled(ctx, { text: down == null ? "—" : (ctx.size === "small" || ctx.size === "medium") ? rate(sending ? up ?? 0 : down).replace(" KiB/s", "\nK/s").replace(" MiB/s", "\nM/s") : rate(sending ? up ?? 0 : down), kicker: labels(ctx).caption,
    detail: up == null ? labels(ctx).waiting : (sending ? "↓ " + rate(down) : "↑ " + rate(up)), visual: "sparkline", series: history, state: { history, sending } });
}
