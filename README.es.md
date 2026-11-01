<p align="center"><img src="assets/ReStart.png" width="96" alt="ReStart" /></p>
<h1 align="center">ReStart</h1>
<p align="center"><strong>Recupera Inicio a pantalla completa.</strong><br>Tus aplicaciones, tu diseño y tus propios mosaicos dinámicos.</p>

<!-- languages:start -->
<p align="center">
  <a href="README.md">English</a> · <a href="README.de.md">Deutsch</a> · <a href="README.es.md">Español</a> · <a href="README.fr.md">Français</a> · <a href="README.ja.md">日本語</a> · <a href="README.ko.md">한국어</a> · <a href="README.zh-CN.md">简体中文</a> · <a href="README.zh-TW.md">繁體中文</a>
</p>
<!-- languages:end -->

<p align="center"><a href="https://lastresolution.github.io/ReStart/">Sitio web (inglés)</a> · <a href="https://lastresolution.github.io/ReStart/developers/">Guía para desarrolladores (inglés)</a></p>

> **Traducción con IA:** esta guía se ha traducido del inglés mediante IA y puede contener errores. El [README en inglés](README.md) es la referencia principal e incluye más ejemplos. Se agradecen las correcciones. Las capturas usan una demostración en inglés con datos de ejemplo.

<p align="center"><strong>Ver la demostración de 60 segundos</strong><br><sub>1080p · Inglés · Sin audio</sub></p>

https://github.com/user-attachments/assets/a893cd20-8c82-4c47-b5fb-bf22743f67e1

¿Echas de menos Inicio a pantalla completa de Windows 10? ReStart ofrece un espacio familiar para tus aplicaciones en Windows 11 y Windows 10. Ancla lo que utilizas, organiza la pantalla y añade mosaicos que puedes escribir en JavaScript.

**Portátil · Mosaicos JavaScript · x64 y ARM64 · Ocho idiomas · Sin instalar .NET por separado**

**Funciones táctiles y barra de tareas.** El uso táctil admite arrastrar mosaicos y grupos, menús con pulsación larga, carpetas y desplazamiento. Los ajustes de inicio permiten mostrar la barra de tareas oculta automáticamente en la pantalla de ReStart mientras está abierto. Windows controla la ocultación automática; ReStart no tiene una opción independiente para ocultarla. Las conexiones de datos requieren «Guardar y autorizar» para cada mosaico colocado; los cambios de acceso requieren una nueva autorización. [Comportamiento y cobertura comprobada (inglés)](docs/fidelity.md).

## Fiabilidad y copias privadas

Los mosaicos fuera del área visible se pausan y los fallos del navegador se recuperan con reintentos limitados. Ambas opciones se pueden desactivar por separado. En **Configuración → Distribución y datos** puedes respaldar y restaurar distribución, apariencia, mosaicos, borradores y fondos. Excluye conexiones, credenciales, rutas de fotos y cachés. La copia sigue siendo privada: contiene nombres, rutas y código propio. [Guía en inglés](docs/backups.md).

## Más control y espacio para crear

Bloquea todo el diseño o grupos individuales. Ctrl+Mayús+L cambia el bloqueo global. Ctrl+E selecciona; Ctrl+flecha mueve mosaicos, Ctrl+Mayús+flecha mueve el grupo y Alt+1–4 cambia el tamaño. Ctrl+G agrupa la selección; Mayús+F10 abre su menú. Se mantiene Deshacer/Rehacer. Puedes guardar un fondo propio, independiente del escritorio.

En **Configuración → Apariencia**, ajusta el tinte del fondo entre **10–100 %** y el desenfoque de la imagen entre **0–50**. El desenfoque predeterminado es **12**; **0** conserva la imagen nítida. El alto contraste tiene prioridad. **Finalizar selección** o pulsar **Mosaicos anclados** vuelve directamente a la vista de mosaicos.

Prueba la apariencia, la escala, el ancho de los grupos y el idioma antes de guardar. Al cerrar, puedes guardar, descartar o seguir editando; descartar restaura la presentación guardada. Tras descargar una actualización expresamente, queda un aviso para reiniciar. La preparación verificada se recupera en el siguiente inicio, sin instalarse automáticamente.

Incluye 19 mosaicos HTML/CSS/JavaScript. Tile Studio permite editar código y manifiesto, probar cuatro tamaños con datos de ejemplo sin conexión y guardar borradores o anclarlos. Los nuevos ejemplos muestran tiempo atmosférico, panel del sistema, noticias RSS y arte Canvas. Las agujas del reloj SVG siguen la hora del PC. Los nombres se muestran por defecto y pueden ocultarse en los ajustes.

Al arrastrar, una copia semitransparente del mosaico, grupo o selección sigue al puntero. La marca azul indica el destino y los mosaicos desplazados muestran su posición antes de soltar.

![Tile Studio: HTML, CSS and JavaScript with four live previews](docs/images/tile-studio.png)

![Weather scene, System dashboard, News board and Canvas garden](docs/images/web-examples.png)

**Controles del editor:** resaltado de JavaScript, HTML, CSS y JSON; **Tab / Mayús+Tab** para la sangría; **Format document / Ctrl+Mayús+F** para formatear sin conexión y deshacer en un solo paso. Feed headlines y News board se desplazan dentro de su mosaico. Las carpetas cerradas muestran vistas previas dinámicas del mismo tamaño. Los diseños nuevos y restablecidos usan Month dots y Orbit clock. La comprobación opcional al iniciar muestra un aviso descartable cuando hay una versión nueva.

## Primeros pasos

1. Descarga el **[ZIP x64](https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-x64.zip)** para un PC Intel/AMD o el **[ZIP ARM64](https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-arm64.zip)** para Windows en ARM.
2. Extrae **todo el ZIP**. Mantén `TileHost`, `Examples` y `third-party` junto a `ReStart.exe`.
3. Ejecuta **ReStart.exe**, pulsa la **tecla Windows** y escribe el nombre de una aplicación. **Ctrl+Alt+Espacio** y el menú del área de notificación también abren Inicio.

El engranaje permite elegir temas, inicio con Windows, integración de la tecla Windows/botón Inicio y actualizaciones. El uso normal no requiere permisos de administrador. Se incluyen [sumas SHA-256](https://github.com/LastResolution/ReStart/releases/latest).

## Integración con aplicaciones de Windows

La búsqueda añade palabras clave de los ajustes de Windows y elementos del Panel de control. La sintaxis AQS opcional ofrece filtros y relevancia nativos; **Buscar en Windows** deriva explícitamente la consulta a otras fuentes de Windows.

Dos opciones independientes están desactivadas al principio: los contadores de uso de Windows para las aplicaciones más usadas y la compatibilidad de solo lectura con mosaicos e insignias de Windows. Se admiten formatos habituales, imágenes locales y enlaces de datos. No se restauran servicios push retirados; si no hay datos, se conserva el icono.

[Ajustes y compatibilidad](docs/start-behavior.md) · [Alcance](docs/fidelity.md)

## Funciones

![Apertura y cierre de ReStart](docs/images/start-motion.gif)

**Grupos por defecto, colocación libre cuando la necesites.** Soltar en vacío crea un grupo sin nombre; esperar sobre un mosaico crea una carpeta. <kbd>Shift</kbd> al arrastrar reorganiza sin carpetas; <kbd>Alt</kbd> permite colocar de forma independiente en vacío. **Use free tile placement**, en Settings → Start behavior, lo convierte en el modo predeterminado. Se conservan los diseños y carpetas existentes. Los vecinos se deslizan sin cambiar el destino bajo el puntero.

Al arrastrar aparecen brevemente consejos de teclado en la parte inferior. Se pueden desactivar en los ajustes de comportamiento. <kbd>F1</kbd> o **Atajos de teclado** en esos ajustes abre la lista completa.


| Área | Incluye |
| --- | --- |
| Abrir y cerrar | Captura opcional de la tecla Windows y del botón Inicio, Ctrl+Alt+Espacio, área de notificación, activación en primer plano, cierre al cambiar de aplicación/barra de tareas, área según monitor e instancia única |
| Arranque | Inicio de sesión opcional, modo en segundo plano, pantalla de preparación con tema y escritura anticipada, caché de iconos y actualización del catálogo |
| Mosaicos y grupos | Cuatro tamaños, anclajes independientes, grupos rectangulares movibles ajustados al contenido, anclajes horizontales de media celda, colocación automática y desplazamiento en ambos ejes |
| Carpetas | Creación al mantener la superposición, apertura en el diseño, añadir/quitar/ordenar, renombrar en línea y desagrupar; un único elemento sigue en su carpeta |
| Edición | Modo de edición, selección múltiple, movimiento conjunto y deshacer/rehacer durante la sesión; cancelar un arrastre conserva el diseño |
| Indicaciones al arrastrar | Destinos de grupo/carpeta distintos, progreso de espera, pistas y límites opcionales, desplazamiento en los bordes; creación automática de grupos/carpetas desactivable |
| Anclar | Aplicaciones, archivos, directorios, accesos directos y sitios; comando **Pin to ReStart** de Explorer, menús contextuales, arrastre desde listas y destinos explícitos |
| Búsqueda | Nombres normalizados, ejecutables y alias de Windows; 63 páginas de configuración traducidas con alias ingleses, búsqueda opcional de nombres indexados y contenido de documentos al activarla (hasta 50 resultados locales), flechas/Intro y cancelación de consultas antiguas |
| Sesión de Windows | Cambio de usuario mediante el inicio de sesión de Windows; suspensión e hibernación según el equipo y las directivas, apagado/reinicio y opciones de Windows Update si se requiere reiniciar |
| Todas las aplicaciones | Columnas adaptables con grupos de Windows según el idioma (kana, hangul, pinyin y trazos), carpetas del menú Inicio desplegables, selector de letras desplazable, navegación por iniciales Unicode, actualización al instalar/desinstalar, agregadas recientemente y más usadas opcionales |
| Comandos | Menús según el contexto: acciones de Inicio en Más, acciones directas en búsquedas, ubicación de archivos/carpetas, gestión y reseñas de aplicaciones y comandos Windows disponibles. Los documentos recientes/frecuentes opcionales tienen sus propias acciones; también se pueden ocultar entradas del historial y personalizar la lista de navegación. [Guía y cobertura de Windows 10](docs/context-menus.md) También muestra tareas registradas por las aplicaciones y permite anclar o desanclar destinos en las listas compartidas con Windows. El historial, las tareas y los elementos anclados se controlan por separado. Compartir se limita a archivos accesibles en sistemas Windows compatibles; Propiedades requiere un archivo, carpeta o acceso directo de origen existente. Las acciones no disponibles se ocultan. Las categorías de listas de accesos directos de cada aplicación conservan sus nombres y acciones registrados. |
| Opciones de ejecución | Activación por mosaico, argumentos, directorio de trabajo y administrador para aplicaciones/accesos directos de escritorio compatibles; Windows controla UAC |
| Navegación | Expansión opcional al pasar el puntero, Documentos/Descargas/Música/Imágenes/Vídeos/Red/carpeta personal seleccionables, cuenta y energía |
| Apariencia | Cinco familias con variantes claras/oscuras, modo del sistema, fondo/acento RGB personalizados, papel tapiz, tinte de Inicio, colores por mosaico, controles y barras a juego, alto contraste y animación opcional |
| Mosaicos dinámicos | 19 ejemplos JavaScript, cuatro tamaños adaptables, acciones locales al pulsar, actualizaciones activables, fuentes de datos configurables, creación de ejemplos, importación y biblioteca observada automáticamente |
| API de mosaicos | Hora del PC, idioma/tamaño, estado conservado, CPU/memoria/red/batería/almacenamiento, fotos elegidas, datos JSON/Web API/RSS/Atom/archivos en caché e integración local |
| Idiomas | Inglés, japonés, alemán, francés, español, coreano, chino simplificado y tradicional, incluidos los textos de mosaicos suministrados |
| Datos y actualizaciones | Ajustes locales, importar/exportar/restablecer, copias y migración, actividad de aplicaciones borrable, versión/build/licencias, comprobación sin clave API, actualizaciones portátiles verificadas con restauración e informes de diagnóstico |
| Distribución | Paquetes portátiles Windows 10/11 x64 y ARM64 con runtime y avisos; integración reversible sin modificar Explorer |

[Interruptores y valores predeterminados (inglés)](docs/start-behavior.md) · [Compatibilidad (inglés)](docs/fidelity.md)

**Una distribución inicial útil.** Al iniciar por primera vez o restablecer, se eligen aplicaciones instaladas según su función, mediante sus identificadores y nombres traducidos exactos. Los mosaicos incluidos sin conexión completan los grupos hasta un mínimo de seis, sin repetir ejemplos. Las distribuciones guardadas se conservan. [Selección y restablecimiento (inglés)](docs/start-behavior.md#initial-layout-and-reset).

**Cobertura de plataforma medida.** Las pruebas nativas verifican la posición y el tamaño de las ventanas en dos pantallas al 100 %/125 %/150 %, incluidas orientaciones vertical y horizontal combinadas. Los proveedores de accesibilidad se comprueban en ocho idiomas y ambos temas; los anuncios básicos de Narrator se revisaron mediante el resumen de voz. En una interfaz de prueba aislada se comprobaron con entrada táctil los toques, los menús por pulsación prolongada, el arrastre de mosaicos y grupos, la creación de carpetas y el desplazamiento. La reconexión de pantallas, otros casos táctiles y los flujos completos con lector de pantalla requieren más pruebas en dispositivos. [Resultados y dispositivos (inglés)](docs/fidelity.md#observed-platform-coverage).

## Buscar y configurar

**Web y nube.** **Buscar … en la Web** abre la consulta actual en Bing con el navegador predeterminado. **Buscar en Windows y en la nube** abre la búsqueda de Windows; introduce allí la consulta. La disponibilidad de la nube depende de Windows y de las cuentas conectadas. Escribir no envía nada al exterior. Ambas acciones pueden desactivarse.

Usa <kbd>↑</kbd>/<kbd>↓</kbd> o <kbd>Tab</kbd>/<kbd>Shift</kbd>+<kbd>Tab</kbd> para elegir resultados locales o acciones web y de Windows/nube; <kbd>Enter</kbd> ejecuta la fila seleccionada. Si la búsqueda local termina sin coincidencias, se resalta una acción en línea habilitada. Escribir por sí solo nunca la abre.

**Historial de aplicaciones.** El historial activado de las aplicaciones más usadas puede ordenar coincidencias equivalentes; los nombres y alias ejecutables exactos conservan la prioridad. Los elementos recientes muestran inicialmente tres entradas con **Mostrar más / Mostrar menos**. Se respetan las exclusiones de historial de los accesos directos de Windows. No se copian el orden interno ni el historial de instalación de Windows Start. [Comportamiento y límites (inglés)](docs/start-behavior.md#app-and-document-history).

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

Un mosaico web contiene manifest.json, index.html, tile.css, tile.js y assets opcionales. Usa HTML/CSS, SVG, Canvas y transparencia dentro de cuatro tamaños fijos. Los paquetes JS nativos siguen funcionando. Requiere WebView2 Runtime. La red directa exige declarar la capacidad y autorizarla por mosaico; no hay acceso general a archivos, CLR ni shell. [HTML/CSS/JavaScript API](docs/javascript-tiles.md) · [Examples](examples/tiles)

## Controles

| Acción | Resultado |
| --- | --- |
| Tecla Windows / <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>Space</kbd> | Abrir o cerrar |
| Escribir / <kbd>Ctrl</kbd> + <kbd>F</kbd> | Buscar; Intro abre el resultado resaltado y las flechas cambian la selección |
| <kbd>Esc</kbd> | Cerrar búsqueda, carpeta o superposición y después Inicio |
| Arrastrar mosaico / cabecera | Mover; el espacio vacío crea un grupo sin nombre |
| <kbd>Alt</kbd> + arrastrar en vacío | Colocación independiente para este arrastre |
| <kbd>Shift</kbd> + arrastrar | Reorganizar y desplazar vecinos sin agrupación ni acciones de carpeta por superposición |
| Superponer un mosaico unos 0,6 s | Crear una carpeta tras la vista previa; las existentes aceptan elementos |
| **Seleccionar mosaicos** en la barra lateral/el menú del fondo / <kbd>Ctrl</kbd> + <kbd>E</kbd> | Selección múltiple sin abrir aplicaciones; siempre puedes arrastrar directamente |
| <kbd>Ctrl</kbd> + <kbd>A</kbd> / <kbd>Delete</kbd> en edición | Seleccionar mosaicos raíz / desanclar la selección |
| <kbd>Ctrl</kbd> + <kbd>Z</kbd> / <kbd>Ctrl</kbd> + <kbd>Y</kbd> o <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>Z</kbd> | Deshacer / rehacer diseño |
| Lápiz / doble clic en nombre | Renombrar grupo o carpeta en línea |
| <kbd>Shift</kbd> + rueda | Desplazar horizontalmente |

Los atajos de Windows se reenvían al sistema. El historial conserva hasta 100 cambios en 32 MiB durante la sesión; los campos de texto mantienen su propio deshacer. Al volver a abrir se muestra la pantalla de anclados.

## Ajustes, privacidad y actualizaciones

Ajustes: `%LOCALAPPDATA%\ReStart\layout.json`. La actividad opcional está separada en `activity.json` y se puede borrar. Las migraciones guardan copias; [detalles (inglés)](docs/migration.md). About muestra versión, build y licencias.

Las actualizaciones leen GitHub Releases públicas sin clave API. En Ajustes → Actualizaciones, elige **Descargar actualización** y después **Reiniciar y actualizar**. Los paquetes portátiles completos y sin modificar, en carpetas con permisos de escritura, admiten actualizaciones verificadas y restauración si falla la instalación o el inicio. Los paquetes antiguos y las versiones de desarrollo se pueden sustituir manualmente. Acerca de → Información de diagnóstico permite revisar y copiar un informe sin rutas, listas de aplicaciones, datos de mosaicos, credenciales ni registros completos. No se sube nada automáticamente. [Actualizaciones y diagnóstico (inglés)](docs/updates.md). Si cambia su ubicación, guarda de nuevo el inicio y la integración Explorer. No hay publicidad ni analítica de uso; no se suben listas de aplicaciones, diseños ni selecciones de fotos. Los proveedores de datos configurados reciben sus solicitudes. [Seguridad (inglés)](SECURITY.md)

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

[Preguntas y ejemplos](https://github.com/LastResolution/ReStart/discussions) · [Informar de errores](https://github.com/LastResolution/ReStart/issues/new/choose) · [Contribuir (inglés)](CONTRIBUTING.md) · [Arquitectura (inglés)](docs/architecture.md) · [Pruebas (inglés)](docs/testing.md) · [Versiones](https://github.com/LastResolution/ReStart/releases)

ReStart es un proyecto MIT independiente inspirado en Inicio a pantalla completa de las últimas versiones de Windows 10, sin afiliación con Microsoft. Las imágenes muestran la aplicación real con una demostración inglesa, métricas sintéticas y colores de fotos generados. Los nombres e iconos pertenecen a sus propietarios. [Licencia MIT](LICENSE) · [Avisos de terceros (inglés)](docs/third-party.md) · [Soporte (inglés)](SUPPORT.md)

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
