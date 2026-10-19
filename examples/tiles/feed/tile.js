// RSS and Atom share the same bounded data shape. Click to cycle headlines locally.
function render(ctx) {
  const labels = ctx.settings.labels[ctx.language] || ctx.settings.labels.en;
  const source = ctx.data?.feed, feed = source?.value;
  const items = Array.isArray(feed?.items) ? feed.items : [];
  const index = items.length ? ((ctx.state.index || 0) + (ctx.event?.type === "click" ? 1 : 0)) % items.length : 0;
  const item = items[index], small = ctx.size === "small";
  return { text: small ? String(items.length || "…") : item?.title || (source?.status === "loading" ? labels.loading : source?.status === "error" ? labels.unavailable : labels.connect),
    kicker: small ? "" : (String(feed?.title || "RSS / Atom") + (source?.stale ? " · " + labels.cached : items.length ? " · " + (index + 1) + "/" + items.length : "")).slice(0, 48),
    detail: ctx.size === "large" ? item?.summary || "" : "",
    layout: small || ctx.size === "medium" ? "centered" : "overlay", state: { index },
    background: ctx.settings.color, backgroundEnd: ctx.settings.colorEnd, foreground: "#FFFFFF", accent: ctx.settings.accent };
}
