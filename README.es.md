<p align="center"><img src="assets/ReStart.png" width="96" alt="ReStart" /></p>
<h1 align="center">ReStart</h1>
<p align="center"><strong>Recupera Inicio a pantalla completa.</strong><br>Tus aplicaciones, tu diseño y tus propios mosaicos dinámicos.</p>

<!-- languages:start -->
<p align="center">
  <a href="README.md">English</a> · <a href="README.de.md">Deutsch</a> · <a href="README.es.md">Español</a> · <a href="README.fr.md">Français</a> · <a href="README.ja.md">日本語</a> · <a href="README.ko.md">한국어</a> · <a href="README.zh-CN.md">简体中文</a> · <a href="README.zh-TW.md">繁體中文</a>
</p>
<!-- languages:end -->

> **Traducción con IA:** esta guía se ha traducido del inglés mediante IA y puede contener errores. El [README en inglés](README.md) es la referencia principal e incluye más ejemplos. Se agradecen las correcciones. Las capturas usan una demostración en inglés con datos de ejemplo.

![ReStart con aplicaciones, carpetas y mosaicos dinámicos](docs/images/start.png)

¿Echas de menos Inicio a pantalla completa de Windows 10? ReStart ofrece un espacio familiar para tus aplicaciones en Windows 11 y Windows 10. Ancla lo que utilizas, organiza la pantalla y añade mosaicos que puedes escribir en JavaScript.

**Portátil · Mosaicos JavaScript · x64 y ARM64 · Ocho idiomas · Sin instalar .NET por separado**

## Primeros pasos

1. Descarga el **[ZIP x64](https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-x64.zip)** para un PC Intel/AMD o el **[ZIP ARM64](https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-arm64.zip)** para Windows en ARM.
2. Extrae **todo el ZIP**. Mantén `TileHost`, `Examples` y `third-party` junto a `ReStart.exe`.
3. Ejecuta **ReStart.exe**, pulsa la **tecla Windows** y escribe el nombre de una aplicación. **Ctrl+Alt+Espacio** y el menú del área de notificación también abren Inicio.

El engranaje permite elegir temas, inicio con Windows, integración de la tecla Windows/botón Inicio y actualizaciones. El uso normal no requiere permisos de administrador. Se incluyen [sumas SHA-256](https://github.com/LastResolution/ReStart/releases/latest).

## Funciones

![Apertura y cierre de ReStart](docs/images/start-motion.gif)

| Área | Incluye |
| --- | --- |
| Abrir y cerrar | Captura opcional de la tecla Windows y del botón Inicio, Ctrl+Alt+Espacio, área de notificación, activación en primer plano, cierre al cambiar de aplicación/barra de tareas, área según monitor e instancia única |
| Arranque | Inicio de sesión opcional, modo en segundo plano, pantalla de preparación con tema y escritura anticipada, caché de iconos y actualización del catálogo |
| Mosaicos y grupos | Cuatro tamaños, anclajes independientes, grupos rectangulares movibles ajustados al contenido, anclajes horizontales de media celda, colocación automática y desplazamiento en ambos ejes |
| Carpetas | Creación al superponer dentro de un grupo, apertura en el diseño, añadir/quitar/ordenar, renombrar en línea y desagrupar; un único elemento sigue en su carpeta |
| Edición | Modo de edición, selección múltiple, movimiento conjunto y deshacer/rehacer durante la sesión; cancelar un arrastre conserva el diseño |
| Indicaciones al arrastrar | Destinos de grupo/carpeta distintos, progreso de espera, pistas y límites opcionales, desplazamiento en los bordes; creación automática de grupos/carpetas desactivable |
| Anclar | Aplicaciones, archivos, directorios, accesos directos y sitios; comando **Pin to ReStart** de Explorer, menús contextuales, arrastre desde listas y destinos explícitos |
| Búsqueda | Nombres de aplicaciones normalizados, 15 páginas de configuración de Windows traducidas con alias ingleses, nombres de archivos locales indexados opcionales, flechas/Intro y descarte de consultas antiguas |
| Todas las aplicaciones | Columnas alfabéticas adaptables, carpetas del menú Inicio desplegables, selector de letra, teclado, actualización al instalar/desinstalar, agregadas recientemente y más usadas opcionales |
| Comandos | Acciones Windows disponibles para administrador, ubicación, propiedades y desinstalación; documentos recientes/frecuentes opcionales según ajustes de Windows y disponibilidad de API |
| Opciones de ejecución | Activación por mosaico, argumentos, directorio de trabajo y administrador para aplicaciones/accesos directos de escritorio compatibles; Windows controla UAC |
| Navegación | Expansión opcional al pasar el puntero, Documentos/Descargas/Música/Imágenes/Vídeos/Red/carpeta personal seleccionables, cuenta y energía |
| Apariencia | Cinco familias con variantes claras/oscuras, modo del sistema, fondo/acento RGB personalizados, papel tapiz, tinte de Inicio, colores por mosaico, controles y barras a juego, alto contraste y animación opcional |
| Mosaicos dinámicos | 15 ejemplos JavaScript, cuatro tamaños adaptables, acciones locales al pulsar, actualizaciones activables, fuentes de datos configurables, creación de ejemplos, importación y biblioteca observada automáticamente |
| API de mosaicos | Hora del PC, idioma/tamaño, estado conservado, CPU/memoria/red/batería/almacenamiento, fotos elegidas, datos JSON/Web API/RSS/Atom/archivos en caché e integración local |
| Idiomas | Inglés, japonés, alemán, francés, español, coreano, chino simplificado y tradicional, incluidos los textos de mosaicos suministrados |
| Datos y actualizaciones | Ajustes locales, importar/exportar/restablecer, copias y migración, actividad de aplicaciones borrable, versión/build/licencias, comprobación sin clave API e instalación manual |
| Distribución | Paquetes portátiles Windows 10/11 x64 y ARM64 con runtime y avisos; integración reversible sin modificar Explorer |

[Interruptores y valores predeterminados (inglés)](docs/start-behavior.md) · [Compatibilidad (inglés)](docs/fidelity.md)

## Buscar y configurar

![Resultados de aplicaciones, configuración y archivos locales](docs/images/unified-search.png)

En **Settings → Start behavior** puedes desactivar por separado la búsqueda de ajustes/archivos, menús de documentos, efectos al pasar el puntero, grupos/carpetas automáticos, desplazamiento en bordes e indicaciones. El registro de aplicaciones más usadas está desactivado inicialmente.

| Comportamiento | Ejecución por mosaico |
| --- | --- |
| ![Opciones de comportamiento](docs/images/start-behavior.png) | ![Argumentos, directorio y administrador](docs/images/launch-options.png) |

Puedes desactivar **Use custom launch options** conservando sus campos. Sin esta opción, los accesos directos mantienen sus valores normales de Windows.

## Mosaicos y tus datos

![Mosaicos dinámicos incluidos](docs/images/live-tiles.png)

- **Hora y concentración:** Orbit clock, Digital clock, Month dots, Focus cycle, Year in motion y Countdown.
- **PC:** CPU pulse, Memory flow, Network flow, Battery y Storage.
- **Fotos:** Photo moments con una carpeta elegida y avance al pulsar.
- **Conexiones:** Weather, Feed headlines y JSON monitor.

Clic derecho en el fondo → **Add live tile**. Los clics actúan dentro del mosaico: pausa del temporizador, siguiente foto, etc. Para Weather/Feed/JSON, abre **Configure data sources…**, introduce una URL o archivo y guarda. **Refresh data now** solicita datos nuevos.

**El tiempo usa Berlín como ejemplo inicial, no la ubicación de tu PC.** ReStart no estima tu región. Cambia coordenadas y nombre con la [guía meteorológica](docs/javascript-tiles.md#weather-location). Las credenciales se cifran para tu usuario de Windows y se excluyen de las exportaciones. La caché sigue siendo útil si falla la conexión; ocultar Inicio detiene las lecturas.

Un mosaico consta de `manifest.json` y `tile.js`. `render(ctx)` recibe una instantánea limitada y devuelve datos para dibujarlos de forma nativa, con diseño según tamaño. JavaScript no obtiene acceso general a archivos, red ni CLR. Coloca paquetes en `%LOCALAPPDATA%\ReStart\TileLibrary`; se detectan automáticamente. [Ejemplos](examples/tiles) · [API JavaScript (inglés)](docs/javascript-tiles.md) · [Productor local](examples/integrations/write-tile-data.ps1)

## Controles

| Acción | Resultado |
| --- | --- |
| Tecla Windows / `Ctrl+Alt+Space` | Abrir o cerrar |
| Escribir / `Ctrl+F` | Buscar; Intro abre el resultado resaltado y las flechas cambian la selección |
| `Esc` | Cerrar búsqueda, carpeta o superposición y después Inicio |
| Arrastrar mosaico / cabecera | Mover; el espacio vacío crea un anclaje independiente |
| Superponer un mosaico independiente unos 0,3 s | Crear un grupo cuando la vista previa esté preparada |
| Superponer un mosaico de un grupo unos 0,6 s | Crear una carpeta; las existentes aceptan elementos |
| **Seleccionar mosaicos** en la barra lateral/el menú del fondo / `Ctrl+E` | Selección múltiple sin abrir aplicaciones; siempre puedes arrastrar directamente |
| `Ctrl+A` / `Delete` en edición | Seleccionar mosaicos raíz / desanclar la selección |
| `Ctrl+Z` / `Ctrl+Y` o `Ctrl+Shift+Z` | Deshacer / rehacer diseño |
| Lápiz / doble clic en nombre | Renombrar grupo o carpeta en línea |
| `Shift` + rueda | Desplazar horizontalmente |

Los atajos de Windows se reenvían al sistema. El historial conserva hasta 100 cambios en 32 MiB durante la sesión; los campos de texto mantienen su propio deshacer. Al volver a abrir se muestra la pantalla de anclados.

## Ajustes, privacidad y actualizaciones

Ajustes: `%LOCALAPPDATA%\ReStart\layout.json`. La actividad opcional está separada en `activity.json` y se puede borrar. Las migraciones guardan copias; [detalles (inglés)](docs/migration.md). About muestra versión, build y licencias.

Las actualizaciones leen GitHub Releases públicas sin clave API. Cierra ReStart y sustituye manualmente toda la instalación extraída. Si cambia su ubicación, guarda de nuevo el inicio y la integración Explorer. No hay publicidad ni analítica de uso; no se suben listas de aplicaciones, diseños ni selecciones de fotos. Los proveedores de datos configurados reciben sus solicitudes. [Seguridad (inglés)](SECURITY.md)

Para eliminarlo, desactiva inicio e integración Explorer, sal desde el área de notificación y borra la instalación. Borra los datos locales solo si también quieres quitar ajustes y mosaicos propios.

## Compilar

Utiliza Windows y el [SDK estable de .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0) seleccionado en `global.json`. Cierra la aplicación antes de reemplazar sus archivos.

```powershell
git clone https://github.com/LastResolution/ReStart.git
cd ReStart
dotnet restore ReStart.slnx --locked-mode
dotnet build ReStart.slnx -c Release --no-restore
dotnet test ReStart.slnx -c Release --no-build
.\src\ReStart.App\bin\Release\net10.0-windows\ReStart.exe
# Vista previa aislada:
dotnet run --project src/ReStart.App -c Release -- --preview
# Paquete portátil con runtime:
./scripts/package.ps1 -Runtime win-x64
```

Para ARM64 usa `win-arm64`. ZIP y sumas se generan en `artifacts/packages`. Distribuye siempre todo el ZIP.

[Errores e ideas](https://github.com/LastResolution/ReStart/issues/new/choose) · [Contribuir (inglés)](CONTRIBUTING.md) · [Arquitectura (inglés)](docs/architecture.md) · [Pruebas (inglés)](docs/testing.md) · [Versiones](https://github.com/LastResolution/ReStart/releases)

ReStart es un proyecto MIT independiente inspirado en Inicio a pantalla completa de las últimas versiones de Windows 10, sin afiliación con Microsoft. Las imágenes muestran la aplicación real con una demostración inglesa, métricas sintéticas y colores de fotos generados. Los nombres e iconos pertenecen a sus propietarios. [Licencia MIT](LICENSE) · [Soporte (inglés)](SUPPORT.md)

---

<!-- support:start -->
<h2 align="center">Apoya el desarrollo independiente</h2>
<p align="center">
  ¿Te gusta ReStart? Ayuda a LastResolution a crear y mejorar herramientas útiles, tanto ReStart como futuros proyectos.<br>
  <sub>ReStart es gratuito y de código abierto. El apoyo es siempre opcional.</sub>
</p>
<p align="center">
  <a href="https://buymeacoffee.com/LastResolution"><img src="https://cdn.buymeacoffee.com/buttons/v2/default-yellow.png" height="48" alt="Buy Me a Coffee · LastResolution" /></a>
</p>
<!-- support:end -->
