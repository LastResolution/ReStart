// The default URL is Berlin, not the PC's location. Change the URL and settings.location together.
function render(ctx) {
  const labels = ctx.settings.labels[ctx.language] || ctx.settings.labels.en;
  const source = ctx.data?.weather, value = source?.value;
  const small = ctx.size === "small";
  let text = source?.status === "loading" ? labels.loading : source?.status === "error" ? labels.unavailable : labels.connect;
  let detail = "", progress;
  if (value && Number.isFinite(value.temperature_2m)) {
    text = Math.round(value.temperature_2m) + "°C";
    const code = Number(value.weather_code);
    const condition = code === 0 ? labels.clear : code <= 3 ? labels.cloud : code <= 48 ? labels.fog
      : code >= 95 ? labels.storm : (code >= 71 && code <= 77) || code === 85 || code === 86 ? labels.snow : labels.rain;
    detail = condition + (Number.isFinite(value.wind_speed_10m) ? " · " + value.wind_speed_10m + " km/h" : "");
    progress = Math.max(0, Math.min(1, (value.temperature_2m + 20) / 60));
  } else if (small) text = "…";
  return { text, detail: small ? "" : detail, kicker: small ? "" : ((ctx.settings.location || "") + " · Open-Meteo" + (source?.stale ? " · " + labels.cached : "")).slice(0, 48), progress,
    visual: small || !value ? "" : "ring", layout: small || !value || ctx.size === "medium" ? "centered" : ctx.size === "large" ? "stacked" : "split",
    background: ctx.settings.color, backgroundEnd: ctx.settings.colorEnd, foreground: "#FFFFFF", accent: ctx.settings.accent };
}
