"""Serve the project-prefix preview and offline, high-resolution bundled tile captures."""
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlsplit
import json

repo = Path(__file__).resolve().parents[2]
root = repo / 'site' / 'dist'
packages = {'orbit', 'cpu', 'memory', 'calendar', 'weather-scene', 'canvas-garden'}

class Handler(SimpleHTTPRequestHandler):
    """Serve only the static deployment and an allowlist of synthetic tile renderers."""

    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=str(root), **kwargs)

    def do_GET(self):
        route = urlsplit(self.path).path
        if route.startswith('/capture/'):
            package = route.strip('/').split('/')[-1]
            if package not in packages:
                self.send_error(404)
                return
            folder = repo / 'examples' / 'tiles' / package
            manifest = json.loads((folder / 'manifest.json').read_text(encoding='utf-8'))
            ctx = dict(title=manifest['title'], size='large', language='en', locale='en-US',
                settings=manifest['settings'], now='2026-10-18T09:41:30',
                time=dict(preview=True, monotonicMilliseconds=0),
                theme=dict(mode='dark', reducedMotion=True),
                calendar=dict(weekday='Sunday', month='October', shortDate='10/18/2026'),
                system=dict(cpuPercent=37, logicalProcessors=16, uptimeSeconds=7200, memoryPercent=63,
                    totalMemoryBytes=34359738368, availableMemoryBytes=12713103196),
                state=dict(history=[.25,.27,.25,.35,.3,.29,.46,.35,.32,.36,.3,.27,.34,.29,.39,.41,.37]),
                data=dict(weather=dict(status='ready', value=dict(temperature_2m=21, weather_code=0, wind_speed_10m=8))))
            source = (folder / 'index.html').read_text(encoding='utf-8')
            source = source.replace('<html lang="en">', '<html lang="en" data-size="large" data-show-title="true" style="--tile-scale:3.6;background:#0f1724">')
            source = source.replace('<link rel="stylesheet" href="/tile.css" />', '<style>'+(folder/'tile.css').read_text(encoding='utf-8')+'</style>')
            bootstrap = 'window.ReStart={imageUrl:null};\n' + (folder/'tile.js').read_text(encoding='utf-8')
            bootstrap += '\nconst fixtureContext='+json.dumps(ctx)+'; fixtureContext.time.unixMs=new Date(fixtureContext.now).getTime(); const fixtureFrame=render(fixtureContext);present(fixtureFrame,fixtureContext);'
            source = source.replace('<script src="/tile.js"></script>', '<script>'+bootstrap+'</script>')
            source = source.replace('</head>', '<style>html,body{width:720px;height:720px}body{background:#0f1724}</style></head>')
            data = source.encode('utf-8')
            self.send_response(200)
            self.send_header('Content-Type', 'text/html; charset=utf-8')
            self.send_header('Content-Length', str(len(data)))
            self.send_header('Content-Security-Policy', "default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; img-src data:")
            self.end_headers()
            self.wfile.write(data)
        else:
            super().do_GET()

    def translate_path(self, path):
        if path.startswith('/ReStart/'):
            path = path[len('/ReStart'):]
        return super().translate_path(path)

    def log_message(self, *args):
        pass

print('Preview: http://127.0.0.1:4173/ReStart/', flush=True)
ThreadingHTTPServer(('127.0.0.1', 4173), Handler).serve_forever()
