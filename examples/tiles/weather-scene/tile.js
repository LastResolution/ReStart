// HTML/CSS/browser JavaScript example. Host connections remain opt-in.
function labels(ctx) {
    return ctx.settings.labels?.[ctx.language] || ctx.settings.labels?.en || {};
}
function field(id, value) {
    document.getElementById(id).textContent = value ?? "";
}
function setup(ctx) {
    document.title = ctx.title;
    field("tile-name", ctx.title);
    document.documentElement.dataset.reducedMotion = String(
        !!ctx.theme?.reducedMotion,
    );
    const tile = document.querySelector(".tile"),
        color = ctx.theme?.customColor;
    if (color) {
        tile.style.background = color;
        const rgb = color
            .match(/[0-9a-f]{2}/gi)
            .map((x) => parseInt(x, 16) / 255)
            .map((v) =>
                v <= 0.04045 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4),
            );
        tile.style.color =
            0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2] > 0.179
                ? "#000000"
                : "#FFFFFF";
    } else {
        tile.style.removeProperty("background");
        tile.style.removeProperty("color");
    }
}
function render(ctx) {
    const l = labels(ctx),
        data = ctx.data?.weather,
        v = data?.value;
    const ready =
        data?.status === "ready" && v && Number.isFinite(v.temperature_2m);
    const code = v?.weather_code ?? 0;
    const type =
        code >= 71 && code <= 86
            ? "snow"
            : code >= 51
              ? "rain"
              : code > 1
                ? "cloud"
                : "clear";
    return {
        text: ready ? Math.round(v.temperature_2m) + "°" : "—",
        detail: ready
            ? l[type]
            : data?.status === "loading"
              ? l.loading
              : l.connect,
        state: { type, wind: ready ? v.wind_speed_10m : null },
    };
}
function present(frame, ctx) {
    setup(ctx);
    field("temperature", frame.text);
    field("condition", frame.detail);
    field(
        "wind",
        frame.state.wind == null
            ? ""
            : labels(ctx).wind + " " + frame.state.wind + " km/h",
    );
    document.querySelector(".sky").dataset.weather = frame.state.type;
}
