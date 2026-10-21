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
    return {
        text: ctx.title || labels(ctx).caption,
        state: {
            palette:
                ((ctx.state.palette || 0) +
                    (ctx.event?.type === "click" ? 1 : 0)) %
                3,
        },
    };
}
let gardenAnimation = 0;
function present(frame, ctx) {
    setup(ctx);
    cancelAnimationFrame(gardenAnimation);
    const canvas = document.getElementById("garden"),
        ratio = Math.min(devicePixelRatio || 1, 2),
        width = innerWidth,
        height = innerHeight;
    canvas.width = Math.ceil(width * ratio);
    canvas.height = Math.ceil(height * ratio);
    const g = canvas.getContext("2d");
    g.scale(ratio, ratio);
    const palettes = [
        ["#5BE1DC", "#7CA0F4", "#E0C8FF"],
        ["#FFB272", "#F47DAD", "#D3B6FF"],
        ["#B0E58A", "#5BD6BD", "#A4C6F7"],
    ];
    const colors = palettes[frame.state.palette];
    const draw = (seconds) => {
        g.clearRect(0, 0, width, height);
        for (let row = 0; row < 6; row++) {
            g.beginPath();
            for (let x = 0; x <= width + 3; x += 3) {
                const y =
                    height * (0.25 + row * 0.1) +
                    Math.sin((x / width) * 5 + seconds * 0.45 + row * 0.65) *
                        height *
                        0.1 +
                    Math.cos((x / width) * 8 - seconds * 0.25) * height * 0.045;
                x ? g.lineTo(x, y) : g.moveTo(x, y);
            }
            g.strokeStyle = colors[row % colors.length];
            g.globalAlpha = 0.25 + row * 0.1;
            g.lineWidth = row === 5 ? 1.6 : 0.8;
            g.stroke();
        }
        for (let i = 0; i < 12; i++) {
            const x = ((i * 0.618) % 1) * width,
                y = (0.5 + 0.3 * Math.sin(i * 2.4 + seconds * 0.3)) * height;
            g.globalAlpha = 0.25 + 0.2 * Math.sin(seconds + i);
            g.fillStyle = colors[i % 3];
            g.beginPath();
            g.arc(x, y, 1.2, 0, Math.PI * 2);
            g.fill();
        }
        g.globalAlpha = 1;
    };
    draw(ctx.time.unixMs / 1000);
    if (!ctx.theme?.reducedMotion && !ctx.time.preview) {
        let previous = 0;
        const animate = (timestamp) => {
            if (timestamp - previous >= 1000 / 30) {
                draw(Date.now() / 1000);
                previous = timestamp;
            }
            gardenAnimation = requestAnimationFrame(animate);
        };
        gardenAnimation = requestAnimationFrame(animate);
    }
}
