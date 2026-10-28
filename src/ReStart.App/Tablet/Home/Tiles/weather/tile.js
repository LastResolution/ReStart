// Open-Meteo weather for the coordinates in the data connection (Configure data sources…).
// settings.view: "current" (now, today's range) or "forecast" (next days). Data © Open-Meteo, CC BY 4.0.
const CONDITIONS = [
    [[0], "clear", "Clear", "Sunny"], [[1], "partly", "Mostly clear", "Mostly sunny"], [[2], "partly", "Partly cloudy"],
    [[3], "cloudy", "Cloudy"], [[45, 48], "fog", "Fog"], [[51, 53, 55], "rain", "Drizzle"], [[56, 57], "rain", "Freezing drizzle"],
    [[61, 63], "rain", "Rain"], [[65], "rain", "Heavy rain"], [[66, 67], "rain", "Freezing rain"], [[71, 73, 77], "snow", "Snow"],
    [[75], "snow", "Heavy snow"], [[80, 81, 82], "rain", "Showers"], [[85, 86], "snow", "Snow showers"], [[95, 96, 99], "storm", "Thunderstorm"]
];
const SKIES = {
    clear: ["#2f86d6", "#71b9f0"], partly: ["#3f84c4", "#86b3d8"], cloudy: ["#5b6f84", "#8a9bad"], fog: ["#6f7a86", "#a3acb5"],
    rain: ["#3e5263", "#617789"], snow: ["#6d86a0", "#a9bccf"], storm: ["#2e3340", "#545068"],
    night: ["#0d1733", "#2c3f6e"], nightCloudy: ["#1b2330", "#3c4757"]
};

function condition(code, day = true) {
    const entry = CONDITIONS.find(c => c[0].includes(Number(code))) || [[], "cloudy", "—"];
    return { kind: entry[1], label: day && entry[3] ? entry[3] : entry[2] };
}

function icon(kind, day = true) {
    const sun = '<circle cx="16" cy="16" r="6.2" fill="#ffd54a"/>' + [0, 45, 90, 135, 180, 225, 270, 315].map(a =>
        `<line x1="16" y1="5" x2="16" y2="7.6" stroke="#ffd54a" stroke-width="2" stroke-linecap="round" transform="rotate(${a} 16 16)"/>`).join("");
    const moon = '<path d="M20.5 6.5a9.5 9.5 0 1 0 5 15.6A8 8 0 0 1 20.5 6.5z" fill="#eef3fb"/>';
    const cloud = (fill = "#fff", dx = 0, dy = 0) => `<path transform="translate(${dx} ${dy})" d="M10 26h13.2a5.3 5.3 0 0 0 .7-10.55 7.4 7.4 0 0 0-14.1 1.75A4.4 4.4 0 0 0 10 26z" fill="${fill}"/>`;
    const small = (body) => `<g transform="translate(-4 -5) scale(.78)">${body}</g>`;
    let body;
    switch (kind) {
        case "clear": body = day ? sun : moon; break;
        case "partly": body = small(day ? sun : moon) + cloud("#fff", 2, 2); break;
        case "cloudy": body = cloud("#dfe6ee", -3, -3) + cloud("#fff", 1, 1); break;
        case "fog": body = [10, 15, 20, 25].map((y, i) => `<line x1="${5 + i % 2 * 3}" y1="${y}" x2="${27 - i % 2 * 3}" y2="${y}" stroke="#eef2f6" stroke-width="2.4" stroke-linecap="round"/>`).join(""); break;
        case "rain": body = cloud("#fff", 0, -4) + [10, 16, 22].map(x => `<line x1="${x}" y1="25" x2="${x - 2}" y2="30" stroke="#8fd0ff" stroke-width="2.2" stroke-linecap="round"/>`).join(""); break;
        case "snow": body = cloud("#fff", 0, -4) + [[10, 27], [16, 29], [22, 27]].map(([x, y]) => `<circle cx="${x}" cy="${y}" r="1.7" fill="#fff"/>`).join(""); break;
        case "storm": body = cloud("#e6e9ef", 0, -4) + '<path d="M17 20l-4.5 6.5h3.3L14 31l6-7.5h-3.4l2.4-3.5z" fill="#ffd54a"/>'; break;
        default: body = cloud();
    }
    return `<svg class="icon" viewBox="0 0 32 32" aria-hidden="true">${body}</svg>`;
}

const pin = '<svg viewBox="0 0 16 16" aria-hidden="true"><path fill="currentColor" d="M8 1.5a4.5 4.5 0 0 0-4.5 4.5c0 3.4 4.5 8.5 4.5 8.5s4.5-5.1 4.5-8.5A4.5 4.5 0 0 0 8 1.5zm0 6.2a1.7 1.7 0 1 1 0-3.4 1.7 1.7 0 0 1 0 3.4z"/></svg>';
const deg = v => Number.isFinite(v) ? Math.round(v) + "°" : "—";
const esc = s => String(s ?? "").replace(/[&<>"]/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]));

function dayName(iso, locale, index) {
    if (index === 0) return "Today";
    return new Date(iso + "T12:00").toLocaleDateString(locale, { weekday: "short" }).replace(".", "");
}

function dayRow(w, i, locale) {
    const d = w.daily, rain = d.precipitation_probability_max?.[i];
    return `<div class="day"><span>${esc(dayName(d.time[i], locale, i))}</span>${icon(condition(d.weather_code[i]).kind)}`
        + `<span class="rain">${rain >= 30 ? rain + "%" : ""}</span><span class="hilo">${deg(d.temperature_2m_max[i])}<span class="lo">${deg(d.temperature_2m_min[i])}</span></span></div>`;
}

let last = "";
function render(ctx) {
    const source = ctx.data?.forecast, w = source?.value;
    const view = ctx.settings?.view === "forecast" ? "forecast" : "current";
    const size = ctx.size, locale = ctx.locale || undefined;
    const place = esc(ctx.settings?.location || "Weather");
    const current = w?.current, day = current ? current.is_day !== 0 : true;
    const now = current ? condition(current.weather_code, day) : { kind: "cloudy", label: "" };
    const sky = !day ? (now.kind === "clear" || now.kind === "partly" ? SKIES.night : SKIES.nightCloudy) : SKIES[now.kind] || SKIES.cloudy;
    document.documentElement.style.setProperty("--top", sky[0]);
    document.documentElement.style.setProperty("--bottom", sky[1]);
    const stale = source?.stale ? '<span class="badge">Offline</span>' : "";
    let html;
    if (!current || !w.daily) {
        const text = source?.status === "loading" || source?.status === "ready" ? ["Weather", "Loading…"]
            : source?.status === "error" ? ["Weather", "Not reachable right now."]
            : ["Weather", "Not connected. Touch and hold → Configure data sources."];
        html = `<div class="message"><b>${text[0]}</b>${size === "small" ? "" : text[1]}</div>`;
    } else if (size === "small") {
        html = `<div class="temp">${deg(current.temperature_2m)}</div>`;
    } else if (view === "forecast" && size === "medium") {
        const rows = [1, 2, 3].filter(i => i < w.daily.time.length).map(i => dayRow(w, i, locale)).join("");
        html = `<div class="title"><span>Forecast${stale}</span><span class="credit">Open-Meteo</span></div><div class="days compact">${rows}</div>`;
    } else {
        const range = `H:${deg(w.daily.temperature_2m_max[0])} L:${deg(w.daily.temperature_2m_min[0])}`;
        const head = `<div class="head"><div class="place">${pin}<span>${place}</span>${stale}</div>${size === "medium" ? icon(now.kind, day) : ""}</div>`;
        const nowBlock = `${head}<div class="temp">${deg(current.temperature_2m)}</div><div class="grow"></div>`
            + `${size === "medium" ? "" : `<div class="condition">${icon(now.kind, day).replace('class="icon"', 'class="icon" style="width:1.6em;height:1.6em;vertical-align:-.45em;margin-right:.2em"')}${esc(now.label)}</div>`}`
            + `${size === "medium" ? `<div class="condition">${esc(now.label)}</div>` : ""}<div class="range">${range}</div>`;
        if (size === "wide") {
            const days = [1, 2, 3, 4, 5].filter(i => i < w.daily.time.length).map(i =>
                `<div><span>${esc(dayName(w.daily.time[i], locale, i))}</span>${icon(condition(w.daily.weather_code[i]).kind)}<span>${deg(w.daily.temperature_2m_max[i])} <span class="lo">${deg(w.daily.temperature_2m_min[i])}</span></span></div>`).join("");
            html = `<div class="now">${nowBlock}</div><div class="strip">${days}</div><div class="credit footer">Open-Meteo</div>`;
        } else if (size === "large") {
            const hours = (w.hourly?.time || []).slice(1, 7).map((t, i) => {
                const h = new Date(t).toLocaleTimeString(locale, { hour: "numeric" });
                const hourDay = (() => { const x = new Date(t).getHours(); return x >= 7 && x < 19; })();
                return `<div><span>${esc(h)}</span>${icon(condition(w.hourly.weather_code[i + 1], hourDay).kind, hourDay)}<span>${deg(w.hourly.temperature_2m[i + 1])}</span></div>`;
            }).join("");
            const rows = [1, 2, 3, 4].filter(i => i < w.daily.time.length).map(i => dayRow(w, i, locale)).join("");
            html = `<div class="head"><div><div class="place">${pin}<span>${place}</span>${stale}</div><div class="temp">${deg(current.temperature_2m)}</div></div>`
                + `<div style="text-align:right">${icon(now.kind, day).replace('class="icon"', 'class="icon" style="width:2.6em;height:2.6em"')}<div class="condition">${esc(now.label)}</div><div class="range">${range}</div></div></div>`
                + `<div class="hours">${hours}</div><div class="days">${rows}</div><div class="credit footer">Open-Meteo</div>`;
        } else html = nowBlock;
    }
    const content = document.getElementById("content");
    content.classList.toggle("wide", size === "wide" && !!current);
    if (html !== last) { content.innerHTML = html; last = html; }
    document.title = current ? `${place} ${deg(current.temperature_2m)} ${now.label}` : "Weather";
}
