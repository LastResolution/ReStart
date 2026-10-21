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
    const s = ctx.system;
    return {
        text: s?.cpuPercent == null ? "—" : Math.round(s.cpuPercent) + "%",
        detail:
            s?.memoryPercent == null ? "—" : Math.round(s.memoryPercent) + "%",
        state: {
            cpu: s?.cpuPercent ?? 0,
            memory: s?.memoryPercent ?? 0,
            network: s?.networkReceiveBytesPerSecond ?? null,
        },
    };
}
function present(frame, ctx) {
    setup(ctx);
    const l = labels(ctx);
    field("caption", ctx.calendar.shortDate);
    field("cpu", frame.text);
    field("memory", frame.detail);
    field("cpu-label", l.cpu);
    field("memory-label", l.memory);
    field("network-label", l.receive);
    field(
        "network",
        frame.state.network == null
            ? "—"
            : Math.round(frame.state.network / 1024) + " KiB/s",
    );
    document
        .querySelector(".cpu .dial")
        .style.setProperty("--progress", frame.state.cpu);
    document
        .querySelector(".memory .dial")
        .style.setProperty("--progress", frame.state.memory);
    document
        .querySelector(".traffic-track")
        .style.setProperty(
            "--traffic",
            Math.min(100, frame.state.network / 1024 / 10) + "%",
        );
}
