// Battery charge, charging state and time left from the tablet host bridge (ctx.tablet.system).
function duration(seconds) {
    if (!Number.isFinite(seconds) || seconds <= 0) return "";
    const h = Math.floor(seconds / 3600), m = Math.round(seconds % 3600 / 60);
    return h > 0 ? `${h} h ${String(m).padStart(2, "0")} min` : `${m} min`;
}

function render(ctx) {
    document.documentElement.dataset.theme = ctx.theme?.mode === "light" ? "light" : "dark";
    const s = ctx.tablet?.system, card = document.getElementById("card");
    const percent = s?.batteryPercent;
    const has = Number.isFinite(percent);
    card.classList.toggle("charging", !!s?.charging);
    card.classList.toggle("saver", !!s?.batterySaver && !s?.pluggedIn);
    card.classList.toggle("low", has && percent <= 20 && percent > 10 && !s.pluggedIn);
    card.classList.toggle("critical", has && percent <= 10 && !s.pluggedIn);
    document.getElementById("percent").textContent = has ? percent + "%" : "—";
    document.getElementById("arc").style.strokeDashoffset = String(263.9 * (1 - (has ? percent : 0) / 100));
    let detail;
    if (!s) detail = "Reading…";
    else if (!has) detail = "No battery";
    else if (s.charging) detail = s.secondsToFull ? `Charging · ${duration(s.secondsToFull)} to full` : "Charging";
    else if (s.pluggedIn) detail = percent >= 99 ? "Fully charged" : "Plugged in";
    else detail = s.secondsLeft ? `${duration(s.secondsLeft)} left` : "On battery";
    if (s?.batterySaver && !s.pluggedIn) detail += " · Saver";
    if (ctx.size !== "medium" && Number.isFinite(s?.chargeRateWatts) && Math.abs(s.chargeRateWatts) >= 0.5)
        detail += ` · ${Math.abs(s.chargeRateWatts).toFixed(1)} W`;
    document.getElementById("detail").textContent = detail;
    document.title = has ? `Battery ${percent}% ${detail}` : "Battery";
}

document.addEventListener("click", () => ReStart.action("system.battery"));
