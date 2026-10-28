// System now playing (Spotify, Brave, ...) through the tablet host bridge (ctx.tablet.media).
// Buttons ask the host to act with ReStart.action; the host updates this tile when playback changes.
let media = null, ticker = 0;
const $ = id => document.getElementById(id);

function progress() {
    if (!media || !(media.duration > 0)) return;
    const elapsed = media.status === "playing" ? (Date.now() - media.sampledAt) / 1000 : 0;
    const position = Math.min(media.duration, Math.max(0, media.position + elapsed));
    $("bar").style.width = (100 * position / media.duration).toFixed(2) + "%";
}

function render(ctx) {
    document.documentElement.dataset.theme = ctx.theme?.mode === "light" ? "light" : "dark";
    const m = ctx.tablet?.media;
    const card = $("card");
    const playing = m && m.status !== "idle";
    media = playing ? m : null;
    card.classList.toggle("idle", !playing);
    card.classList.toggle("paused", playing && m.status === "paused");
    card.classList.toggle("timed", playing && m.duration > 0);
    card.classList.toggle("has-art", !!(playing && m.art));
    if (playing && m.art) {
        if ($("cover").getAttribute("src") !== m.art) { $("cover").src = m.art; $("backdrop").src = m.art; }
    }
    $("app").textContent = playing ? m.app || "Now playing" : "";
    $("title").textContent = playing ? m.title : ctx.tablet ? "Not playing" : "Now playing";
    $("artist").textContent = playing ? [m.artist, m.album && m.album !== m.title ? m.album : ""].filter(Boolean).join(" — ") : "Music and videos show up here";
    $("toggle").innerHTML = playing && m.status === "playing" ? "&#xE769;" : "&#xE768;";
    $("toggle").setAttribute("aria-label", playing && m.status === "playing" ? "Pause" : "Play");
    progress();
    clearInterval(ticker);
    if (media?.status === "playing" && media.duration > 0) ticker = setInterval(progress, 1000);
    document.title = playing ? `${m.title} — ${m.artist}` : "Not playing";
}

document.addEventListener("click", event => {
    const target = event.target.closest?.("[data-action]");
    if (!target || !media) return;
    const action = target.dataset.action;
    // Flip the button immediately; the host's next update confirms it.
    if (action === "media.toggle") {
        media = { ...media, status: media.status === "playing" ? "paused" : "playing", position: media.position + (media.status === "playing" ? (Date.now() - media.sampledAt) / 1000 : 0), sampledAt: Date.now() };
        $("toggle").innerHTML = media.status === "playing" ? "&#xE769;" : "&#xE768;";
        $("card").classList.toggle("paused", media.status === "paused");
    }
    ReStart.action(action);
});
