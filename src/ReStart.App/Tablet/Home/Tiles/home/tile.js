// Home Assistant lights and scenes through the tablet host bridge (ctx.tablet.home).
// The host owns the address and token (DPAPI, never sent here) and accepts "home.run" only for
// the entities listed in this manifest's settings.
const ICONS = {
    bulb: '<path d="M12 2.5a6.5 6.5 0 0 0-3.9 11.7c.6.5.9 1.1.9 1.8v.5h6v-.5c0-.7.3-1.3.9-1.8A6.5 6.5 0 0 0 12 2.5z" fill="currentColor"/><path d="M9.2 18.3h5.6M10 21h4" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/>',
    relax: '<path d="M5 11V9a3 3 0 0 1 3-3h8a3 3 0 0 1 3 3v2" stroke="currentColor" stroke-width="1.8" fill="none"/><path d="M3.5 12.5a1.8 1.8 0 0 1 3.5.5v1.5h10V13a1.8 1.8 0 1 1 3.5-.5V18H3.5z" fill="currentColor"/><path d="M6 18v2M18 18v2" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/>',
    bright: '<circle cx="12" cy="12" r="4.2" fill="currentColor"/>' + [0, 45, 90, 135, 180, 225, 270, 315].map(a => `<line x1="12" y1="2.6" x2="12" y2="5" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" transform="rotate(${a} 12 12)"/>`).join(""),
    read: '<path d="M3 5.5c3-1 6-.6 9 1.2 3-1.8 6-2.2 9-1.2V19c-3-1-6-.6-9 1.2-3-1.8-6-2.2-9-1.2z" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linejoin="round"/><path d="M12 6.7v13.5" stroke="currentColor" stroke-width="1.6"/>',
    focus: '<circle cx="12" cy="12" r="8" fill="none" stroke="currentColor" stroke-width="1.8"/><circle cx="12" cy="12" r="4" fill="none" stroke="currentColor" stroke-width="1.8"/><circle cx="12" cy="12" r="1.4" fill="currentColor"/>',
    night: '<path d="M15.5 3.5a8.5 8.5 0 1 0 5 13.9A7 7 0 0 1 15.5 3.5z" fill="currentColor"/>',
    house: '<path d="M4 11.5 12 4.5l8 7V20h-5.5v-5h-5v5H4z" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linejoin="round"/>'
};
const svg = (name, extra = "") => `<svg viewBox="0 0 24 24" aria-hidden="true"${extra}>${ICONS[name] || ICONS.bulb}</svg>`;
const esc = s => String(s ?? "").replace(/[&<>"]/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]));
const STATUS = { online: "Connected", connecting: "Connecting…", offline: "Not reachable", unauthorized: "Token rejected", error: "Error", unconfigured: "Not connected" };
let home = null, pending = {}, toastTimer = 0, lastLights = "", lastScenes = "";

function toast(text) {
    const t = document.getElementById("toast");
    t.textContent = text; t.classList.add("show");
    clearTimeout(toastTimer); toastTimer = setTimeout(() => t.classList.remove("show"), 2200);
}

function render(ctx) {
    document.documentElement.dataset.theme = ctx.theme?.mode === "light" ? "light" : "dark";
    home = ctx.tablet?.home || { status: ctx.tablet ? "unconfigured" : "connecting", states: {} };
    const settings = ctx.settings || {}, states = home.states || {};
    const card = document.getElementById("card");
    card.className = "card " + home.status;
    document.getElementById("status").textContent = STATUS[home.status] || home.status;
    // Optimistic toggles stay until the host reports the new state (or 6 s pass).
    const now = Date.now();
    for (const [entity, p] of Object.entries(pending)) if (now - p.at > 6000 || states[entity]?.on === p.on) delete pending[entity];
    const lights = (settings.lights || []).slice(0, 6).map(l => {
        const s = states[l.entity], on = pending[l.entity] ? pending[l.entity].on : !!s?.on;
        const level = on && Number.isFinite(s?.brightness) ? s.brightness + "%" : on ? "On" : s ? "Off" : "";
        const tint = on && s?.color ? ` style="color:${esc(s.color)}"` : "";
        return `<div class="light${on ? " on" : ""}${s && !s.available ? " unavailable" : ""}" data-action="home.run" data-entity="${esc(l.entity)}">`
            + `<div class="top"><span${tint}>${svg("bulb")}</span><span class="level">${s && !s.available ? "—" : level}</span></div>`
            + `<div class="label">${esc(l.name || l.entity)}</div></div>`;
    }).join("");
    // Connected but nothing chosen yet: say where entities come from instead of showing an empty card.
    const empty = !lights && !(settings.scenes || []).length
        ? `<div class="empty">${svg("bulb")}<span>Choose lights and scenes in <b>tablet-settings.json</b> (see the tablet edition docs), then reset the layout.</span></div>` : "";
    if ((lights || empty) !== lastLights) { document.getElementById("lights").innerHTML = lights || empty; lastLights = lights || empty; }
    const scenes = (settings.scenes || []).slice(0, 5).map(s =>
        `<div class="scene" data-action="home.run" data-entity="${esc(s.entity)}">${svg(s.icon)}<span>${esc(s.name)}</span></div>`).join("");
    if (scenes !== lastScenes) { document.getElementById("scenes").innerHTML = scenes; lastScenes = scenes; }
    document.getElementById("scenesTitle").textContent = settings.scenesTitle || "Scenes";
    const setup = document.getElementById("setup");
    const message = home.status === "unauthorized"
        ? `${svg("house")}<b>Home Assistant rejected the token</b><span>Create a new long-lived access token and paste it here.</span><div class="button">Update token</div>`
        : `${svg("house")}<b>Add your Home Assistant token</b><span>Lights and scenes appear here once ReStart can reach Home Assistant.</span><div class="button">Connect</div>`;
    if (setup.dataset.message !== home.status) { setup.innerHTML = message; setup.dataset.message = home.status; }
    if (ctx.size === "medium") {
        let summary = card.querySelector(".summary");
        if (!summary) { summary = document.createElement("div"); summary.className = "summary"; card.insertBefore(summary, setup); }
        const count = (settings.lights || []).filter(l => states[l.entity]?.on).length;
        summary.innerHTML = `<b>${count}</b>${count === 1 ? "light on" : "lights on"}`;
    }
    const on = (settings.lights || []).filter(l => states[l.entity]?.on).length;
    document.title = home.status === "online" ? `Home: ${on} on` : "Home: " + (STATUS[home.status] || "");
}

document.addEventListener("click", event => {
    const target = event.target.closest?.("[data-action]");
    if (!target || !home) return;
    if (target.dataset.action === "home.connect") { ReStart.action("home.connect"); return; }
    if (home.status === "unconfigured" || home.status === "unauthorized") { ReStart.action("home.connect"); return; }
    if (home.status === "offline" || home.status === "connecting") { toast(home.status === "offline" ? "Home Assistant isn't reachable" : "Connecting…"); return; }
    const entity = target.dataset.entity;
    if (!entity) return;
    if (entity.startsWith("scene.")) {
        target.classList.add("fired"); setTimeout(() => target.classList.remove("fired"), 450);
    } else {
        const on = !(pending[entity] ? pending[entity].on : !!home.states?.[entity]?.on);
        pending[entity] = { on, at: Date.now() };
        target.classList.toggle("on", on);
        const level = target.querySelector(".level"); if (level) level.textContent = on ? "On" : "Off";
        lastLights = "";
    }
    ReStart.action("home.run", entity);
});
