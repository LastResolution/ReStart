// The default URL is Berlin, not the PC's location. Change the URL and settings.location together.
function render(ctx) {
    const labels = ctx.settings.labels[ctx.language] || ctx.settings.labels.en;
    const source = ctx.data?.weather,
        value = source?.value;
    const small = ctx.size === "small";
    let text =
        source?.status === "loading"
            ? labels.loading
            : source?.status === "error"
              ? labels.unavailable
              : labels.connect;
    let detail = "",
        progress;
    if (value && Number.isFinite(value.temperature_2m)) {
        text = Math.round(value.temperature_2m) + "°C";
        const code = Number(value.weather_code);
        const condition =
            code === 0
                ? labels.clear
                : code <= 3
                  ? labels.cloud
                  : code <= 48
                    ? labels.fog
                    : code >= 95
                      ? labels.storm
                      : (code >= 71 && code <= 77) || code === 85 || code === 86
                        ? labels.snow
                        : labels.rain;
        detail =
            condition +
            (Number.isFinite(value.wind_speed_10m)
                ? " · " + value.wind_speed_10m + " km/h"
                : "");
        progress = Math.max(0, Math.min(1, (value.temperature_2m + 20) / 60));
    } else if (small) text = "…";
    return {
        text,
        detail: small ? "" : detail,
        kicker: small
            ? ""
            : (
                  (ctx.settings.location || "") +
                  " · Open-Meteo" +
                  (source?.stale ? " · " + labels.cached : "")
              ).slice(0, 48),
        progress,
        visual: small || !value ? "" : "ring",
        layout:
            small || !value || ctx.size === "medium"
                ? "centered"
                : ctx.size === "large"
                  ? "stacked"
                  : "split",
        background: ctx.settings.color,
        backgroundEnd: ctx.settings.colorEnd,
        foreground: "#FFFFFF",
        accent: ctx.settings.accent,
    };
}

// The web renderer calls present after render. This is ordinary DOM/SVG code;
// custom tiles can instead update the DOM directly in render or ReStart.onUpdate.
function present(frame, ctx) {
    const card = document.querySelector(".card"),
        svg = document.getElementById("visual");
    const text = (id, value) => {
        document.getElementById(id).textContent = value || "";
    };
    const compact =
        ctx.size !== "small" &&
        String(frame.text).length >
            (ctx.size === "medium" ? 8 : ctx.size === "wide" ? 16 : 20);
    document
        .querySelector(".content")
        .classList.toggle("compact-copy", compact);
    text(
        "kicker",
        [frame.kicker, compact ? frame.badge : ""].filter(Boolean).join(" · "),
    );
    text("value", frame.text);
    text("detail", frame.detail);
    text("badge", frame.badge);
    text("title", ctx.title || document.title);
    document.title = ctx.title || document.title;
    document.documentElement.dataset.reducedMotion = String(
        !!ctx.theme?.reducedMotion,
    );
    document.documentElement.dataset.layout = frame.layout || "split";
    const custom = ctx.theme?.customColor;
    card.style.setProperty("--start", custom || frame.background || "#0078D7");
    card.style.setProperty(
        "--end",
        custom || frame.backgroundEnd || frame.background || "#142335",
    );
    card.style.setProperty(
        "--ink",
        custom ? readableInk(custom) : frame.foreground || "#FFFFFF",
    );
    card.style.setProperty(
        "--accent",
        custom ? readableInk(custom) : frame.accent || "#91DDE2",
    );
    const photo = document.getElementById("photo");
    const image = frame.image === "current" ? ReStart.imageUrl : null;
    card.classList.toggle("has-photo", !!image);
    if (image) photo.src = image;
    else photo.removeAttribute("src");
    svg.replaceChildren();
    const add = (name, attrs) => {
        const node = document.createElementNS(
            "http://www.w3.org/2000/svg",
            name,
        );
        for (const [k, v] of Object.entries(attrs))
            node.setAttribute(k, String(v));
        svg.append(node);
        return node;
    };
    const progress = Math.max(0, Math.min(1, frame.progress || 0));
    if (frame.visual === "ring") {
        add("circle", {
            cx: 50,
            cy: 50,
            r: 42,
            fill: "none",
            stroke: "currentColor",
            "stroke-width": 6,
            opacity: 0.2,
        });
        add("circle", {
            cx: 50,
            cy: 50,
            r: 42,
            fill: "none",
            stroke: "currentColor",
            "stroke-width": 6,
            "stroke-dasharray": 2 * Math.PI * 42,
            "stroke-dashoffset": 2 * Math.PI * 42 * (1 - progress),
            transform: "rotate(-90 50 50)",
            "stroke-linecap": "round",
        });
    } else if (frame.visual === "sparkline") {
        const series = frame.series || [];
        add("path", {
            d: series
                .map(
                    (v, i) =>
                        (i ? "L" : "M") +
                        ((i * 96) / Math.max(1, series.length - 1) + 2) +
                        "," +
                        (92 - v * 80),
                )
                .join(" "),
            fill: "none",
            stroke: "currentColor",
            "stroke-width": 2,
            "vector-effect": "non-scaling-stroke",
        });
    } else if (frame.visual === "bars") {
        const values = frame.series?.length
            ? frame.series
            : Array.from({ length: 12 }, (_, i) =>
                  Math.max(0, Math.min(1, progress * 12 - i)),
              );
        values.forEach((v, i) =>
            add("rect", {
                x: (i * 100) / values.length + 1,
                y: 95 - v * 85,
                width: Math.max(1, 100 / values.length - 3),
                height: Math.max(1, v * 85),
                rx: 1.5,
                fill: "currentColor",
                opacity: 0.6,
            }),
        );
    } else if (frame.visual === "dots") {
        for (let i = 0; i < 35; i++)
            add("circle", {
                cx: 8 + (i % 7) * 14,
                cy: 10 + Math.floor(i / 7) * 19,
                r: 3,
                fill: "currentColor",
                opacity: i / 35 < progress ? 0.75 : 0.2,
            });
    }
    // SVG's square viewBox keeps circular geometry circular at every tile size.
    svg.setAttribute("preserveAspectRatio", "xMidYMid meet");
}
function readableInk(hex) {
    const rgb = hex
        .match(/[0-9a-f]{2}/gi)
        ?.map((x) => parseInt(x, 16) / 255) || [0, 0, 0];
    const lin = rgb.map((v) =>
        v <= 0.04045 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4),
    );
    return 0.2126 * lin[0] + 0.7152 * lin[1] + 0.0722 * lin[2] > 0.179
        ? "#000000"
        : "#FFFFFF";
}
