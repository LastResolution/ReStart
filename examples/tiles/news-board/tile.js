// HTML/CSS/browser JavaScript example. Host connections remain opt-in.
function labels(ctx) {
    return ctx.settings.labels?.[ctx.language] || ctx.settings.labels?.en || {};
}
function field(id, value) {
    const element = document.getElementById(id),
        text = String(value ?? "");
    if (element.textContent !== text) element.textContent = text;
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
    const data = ctx.data?.feed,
        items = data?.value?.items || [];
    const index = items.length
        ? ((ctx.state.index || 0) + (ctx.event?.type === "click" ? 1 : 0)) %
          items.length
        : 0;
    const item = items[index];
    return {
        text: item?.title || labels(ctx).connect,
        detail: item?.summary || "",
        state: {
            index,
            count: items.length,
            feed: data?.value?.title || ctx.title,
        },
    };
}
function present(frame, ctx) {
    setup(ctx);
    const changed =
        document.getElementById("headline").textContent !== frame.text;
    field("caption", frame.state.feed);
    field(
        "number",
        frame.state.count
            ? frame.state.index + 1 + " / " + frame.state.count
            : "",
    );
    field("headline", frame.text);
    field("summary", frame.detail);
    if (changed) document.getElementById("story").scrollTop = 0;
    const paper = document.querySelector(".paper");
    paper.classList.remove("changed");
    if (ctx.event?.type === "click") {
        void paper.offsetWidth;
        paper.classList.add("changed");
    }
}
