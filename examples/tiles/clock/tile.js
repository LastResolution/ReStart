// Edit, save and return to ReStart. No rebuild is needed.
function render(ctx) {
  const now = new Date(ctx.now);
  const hh = String(now.getHours()).padStart(2, "0");
  const mm = String(now.getMinutes()).padStart(2, "0");
  const ss = String(now.getSeconds()).padStart(2, "0");
  return {
    text: `${hh}:${mm}:${ss}`,
    detail: ctx.settings.caption,
    background: ctx.settings.color,
    progress: now.getSeconds() / 59,
    state: { renders: (ctx.state?.renders ?? 0) + 1 }
  };
}
