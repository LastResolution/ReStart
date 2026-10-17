function render(ctx) {
  const remaining = Math.max(0, Math.ceil((Date.parse(ctx.settings.target) - Date.parse(ctx.now)) / 1000));
  const days = Math.floor(remaining / 86400);
  const hours = Math.floor(remaining / 3600) % 24;
  return { text: `${days}d ${hours}h`, detail: ctx.settings.caption, background: "#107C10" };
}
