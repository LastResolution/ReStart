// Local data model; present() below draws this package with ordinary HTML/CSS/SVG.
function labels(ctx) {
    const entries = ctx.settings.labels || {};
    return entries[ctx.language] || entries.en || {};
}
function styled(ctx, value) {
    if (ctx.size === "small") {
        // Compact square tiles show one value; two clock rows avoid clipped digits.
        if (/^\d{1,3}:\d{2}/.test(value.text))
            value.text = value.text.split(":").slice(0, 2).join("\n");
        if (/^\d+(\.\d+)?%$/.test(value.text))
            value.text = Math.round(parseFloat(value.text)) + "%";
    }
    if (ctx.size === "medium" && value.text.includes("\n")) value.detail = "";
    return Object.assign(
        {
            background: ctx.settings.color,
            backgroundEnd: ctx.settings.colorEnd,
            foreground: "#FFFFFF",
            accent: ctx.settings.accent,
            // The data model is shared; present() controls this package’s HTML/CSS layout.
            layout:
                ctx.size === "small" || ctx.size === "medium"
                    ? "centered"
                    : ctx.size === "large"
                      ? "stacked"
                      : "split",
        },
        value,
        ctx.size === "small"
            ? { kicker: "", detail: "", badge: "", visual: "", series: [] }
            : {},
    );
}
function pad(value) {
    return String(value).padStart(2, "0");
}
function render(ctx) {
    const now = new Date(ctx.now);
    const mode =
        ctx.event?.type === "click"
            ? !ctx.state?.seconds
            : !!ctx.state?.seconds;
    return styled(ctx, {
        text:
            pad(now.getHours()) +
            ":" +
            pad(now.getMinutes()) +
            (mode && ctx.size !== "small" && ctx.size !== "medium"
                ? ":" + pad(now.getSeconds())
                : ""),
        kicker: ctx.calendar?.weekday || labels(ctx).caption,
        detail: ctx.calendar?.shortDate || ctx.now.slice(0, 10),
        visual: "ring",
        progress: now.getSeconds() / 60,
        state: { seconds: mode },
    });
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

// This clock is a real SVG dial. Its hands follow the PC time supplied by ReStart.
const paintCard = present;
let clockAnimation = 0;
present = function (frame, ctx) {
    paintCard(frame, ctx);
    const card = document.querySelector(".card");
    card.classList.add("clock-card");
    card.style.setProperty(
        "--ink",
        ctx.theme?.customColor
            ? readableInk(ctx.theme.customColor)
            : ctx.theme?.mode === "light"
              ? "#152B3B"
              : "#FFFFFF",
    );
    cancelAnimationFrame(clockAnimation);
    const svg = document.getElementById("visual");
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
    add("circle", {
        cx: 50,
        cy: 50,
        r: 46,
        fill: frame.background || "#123448",
        stroke: "currentColor",
        "stroke-width": 1,
    });
    for (let i = 0; i < 12; i++) {
        const a = (i * Math.PI) / 6;
        add("line", {
            x1: 50 + Math.sin(a) * 39,
            y1: 50 - Math.cos(a) * 39,
            x2: 50 + Math.sin(a) * 42,
            y2: 50 - Math.cos(a) * 42,
            stroke: "currentColor",
            "stroke-width": i % 3 ? 1 : 2,
            opacity: 0.75,
        });
    }
    const makeHand = (width, opacity = 1) =>
        add("line", {
            x1: 50,
            y1: 50,
            stroke: "currentColor",
            "stroke-width": width,
            "stroke-linecap": "round",
            opacity,
        });
    const hour = makeHand(3),
        minute = makeHand(2),
        second = makeHand(1, 0.75);
    add("circle", { cx: 50, cy: 50, r: 2.5, fill: "currentColor" });
    const hand = (node, angle, length) => {
        node.setAttribute("x2", 50 + Math.sin(angle) * length);
        node.setAttribute("y2", 50 - Math.cos(angle) * length);
    };
    const paint = (unixMs) => {
        const time = new Date(unixMs),
            seconds = time.getSeconds() + time.getMilliseconds() / 1000;
        hand(
            hour,
            (((time.getHours() % 12) + time.getMinutes() / 60) * Math.PI) / 6,
            22,
        );
        hand(minute, ((time.getMinutes() + seconds / 60) * Math.PI) / 30, 33);
        hand(second, (seconds * Math.PI) / 30, 36);
    };
    // Date.now() reads the same PC wall clock as the host, without a network request.
    // It keeps the dial current while another tile waits for data or a host update.
    // Documentation uses the host's synthetic clock so exported frames stay reproducible.
    paint(ctx.time.preview ? ctx.time.unixMs : Date.now());
    if (!ctx.time.preview && !ctx.theme?.reducedMotion) {
        let previous = 0;
        const animate = (timestamp) => {
            if (timestamp - previous >= 1000 / 30) {
                paint(Date.now());
                previous = timestamp;
            }
            clockAnimation = requestAnimationFrame(animate);
        };
        clockAnimation = requestAnimationFrame(animate);
    }
};
