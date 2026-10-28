// Performance mode (read-only, from G-Helper) and load meters from the tablet host bridge.
const MODES = { silent: "Silent", balanced: "Balanced", turbo: "Turbo", custom: "Custom" };

function meter(id, value) {
    const row = document.getElementById(id), ok = Number.isFinite(value);
    row.querySelector("i").style.width = (ok ? Math.max(2, Math.min(100, value)) : 0) + "%";
    row.querySelector(".num").textContent = ok ? Math.round(value) + "%" : "—";
    row.classList.toggle("hot", ok && value >= 85);
}

function render(ctx) {
    document.documentElement.dataset.theme = ctx.theme?.mode === "light" ? "light" : "dark";
    const s = ctx.tablet?.system, mode = s?.performanceMode;
    document.getElementById("card").className = "card " + (mode || "none");
    document.getElementById("modeName").textContent = MODES[mode] || "";
    meter("cpu", s?.cpu); meter("gpu", s?.gpu); meter("ram", s?.memory);
    document.title = `${MODES[mode] || "System"} · CPU ${Math.round(s?.cpu ?? 0)}% · GPU ${Math.round(s?.gpu ?? 0)}%`;
}

document.addEventListener("click", () => ReStart.action("system.performance"));
