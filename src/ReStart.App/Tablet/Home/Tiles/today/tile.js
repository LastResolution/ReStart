// Big date and time from the PC clock (no network). The third line greets, and shows the next
// agenda item once a calendar bridge provides ctx.tablet.agenda = [{ title, start }].
let context = null, timer = 0;

function paint() {
    const ctx = context; if (!ctx) return;
    const now = ctx.time?.preview ? new Date(ctx.now) : new Date();
    const locale = ctx.locale || undefined;
    document.getElementById("weekday").textContent = now.toLocaleDateString(locale, { weekday: "long" });
    document.getElementById("day").textContent = now.toLocaleDateString(locale, { month: "long", day: "numeric" });
    const parts = new Intl.DateTimeFormat(locale, { hour: "numeric", minute: "2-digit" }).formatToParts(now);
    document.getElementById("clock").textContent = parts.filter(p => p.type !== "dayPeriod").map(p => p.value).join("").trim();
    document.getElementById("period").textContent = parts.find(p => p.type === "dayPeriod")?.value || "";
    const hour = now.getHours();
    const greeting = hour < 5 ? "Good night" : hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";
    const next = (ctx.tablet?.agenda || []).find(e => new Date(e.start) > now);
    const line = document.getElementById("line");
    line.replaceChildren(greeting);
    if (next) {
        const at = new Date(next.start).toLocaleTimeString(locale, { hour: "numeric", minute: "2-digit" });
        const title = document.createElement("b"); title.textContent = String(next.title || "").slice(0, 80);
        line.append(" · Up next ", title, " " + at);
    }
    document.title = document.getElementById("clock").textContent;
}

function render(ctx) {
    context = ctx;
    document.documentElement.dataset.theme = ctx.theme?.mode === "light" ? "light" : "dark";
    paint();
    // Repaint on each minute boundary between host updates.
    clearTimeout(timer);
    if (ctx.time?.preview) return;
    const wait = () => 60000 - Date.now() % 60000 + 40;
    timer = setTimeout(function tick() { paint(); timer = setTimeout(tick, wait()); }, wait());
}
