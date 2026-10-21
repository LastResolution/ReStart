<p align="center"><img src="assets/ReStart.png" width="96" alt="ReStart" /></p>
<h1 align="center">ReStart</h1>
<p align="center"><strong>Retrouvez le menu Démarrer en plein écran.</strong><br>Vos applications, votre disposition, vos propres vignettes dynamiques.</p>

<!-- languages:start -->
[English](README.md) · [Deutsch](README.de.md) · [Español](README.es.md) · [Français](README.fr.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md)
<!-- languages:end -->

> **Traduction par IA :** ce guide a été traduit de l’anglais à l’aide de l’IA et peut contenir des erreurs. Le [README anglais](README.md) reste la référence principale et contient des exemples supplémentaires. Les corrections sont bienvenues. Les captures utilisent une démonstration en anglais et des données d’exemple.

![ReStart avec applications, dossiers et vignettes dynamiques](docs/images/start.png)

Le menu Démarrer plein écran de Windows 10 vous manque ? ReStart offre un espace familier à vos applications sous Windows 11 et Windows 10. Épinglez vos contenus, organisez l’écran à votre façon et ajoutez des vignettes que vous pouvez écrire en JavaScript.

**Portable · Vignettes JavaScript · x64 et ARM64 · Huit langues · Aucune installation séparée de .NET**

## Démarrage rapide

1. Téléchargez le **[ZIP x64](https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-x64.zip)** pour un PC Intel/AMD ou le **[ZIP ARM64](https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-arm64.zip)** pour Windows sur ARM.
2. Extrayez **l’intégralité du ZIP**. Conservez `TileHost`, `Examples` et `third-party` à côté de `ReStart.exe`.
3. Lancez **ReStart.exe**, appuyez sur la **touche Windows**, puis saisissez le nom d’une application. **Ctrl+Alt+Espace** et le menu de la zone de notification ouvrent également Démarrer.

La roue dentée permet de régler les thèmes, le lancement à l’ouverture de session, l’intégration de la touche Windows/du bouton Démarrer et les mises à jour. L’usage normal ne nécessite pas de droits administrateur. Les téléchargements actuels ne sont pas signés ; les [sommes SHA-256](https://github.com/LastResolution/ReStart/releases/latest) sont fournies.

## Fonctionnalités

![Ouverture et fermeture de ReStart](docs/images/start-motion.gif)

| Domaine | Fonctions incluses |
| --- | --- |
| Ouverture et fermeture | Capture facultative de la touche Windows et du bouton Démarrer, Ctrl+Alt+Espace, zone de notification, activation au premier plan, fermeture lors du changement d’application/barre des tâches, zone de travail selon l’écran, instance unique |
| Démarrage | Lancement à la connexion facultatif, mode arrière-plan, écran de préparation adapté au thème avec saisie anticipée, cache d’icônes et actualisation du catalogue |
| Vignettes et groupes | Quatre tailles, épingles indépendantes, groupes rectangulaires déplaçables aux dimensions adaptées au contenu, ancrage horizontal au demi-pas, placement automatique et défilement sur les deux axes |
| Dossiers | Création par superposition dans un groupe, ouverture dans la disposition, ajout/retrait/réorganisation, renommage direct et dissociation ; un enfant unique conserve son dossier |
| Édition | Mode édition, sélection multiple, déplacement collectif, annuler/rétablir pour la session ; l’annulation d’un glissement conserve la disposition |
| Retour pendant le glissement | Cibles de groupe/dossier distinctes, progression d’attente, indications et limites facultatives, défilement aux bords ; création automatique de groupes/dossiers désactivable |
| Épinglage | Applications, fichiers, répertoires, raccourcis et sites ; commande Explorer **Pin to ReStart**, menus contextuels, glissement depuis la liste et destinations explicites |
| Recherche | Noms d’applications normalisés, 15 pages Windows traduites avec alias anglais, noms de fichiers locaux indexés facultatifs, flèches/Entrée, rejet des requêtes périmées |
| Toutes les applications | Colonnes alphabétiques adaptatives, dossiers du menu Démarrer dépliables, choix d’une lettre, clavier, actualisation des installations/suppressions, ajouts récents et applications fréquentes en option |
| Commandes d’application | Actions Windows disponibles : administrateur, emplacement, propriétés, désinstallation ; documents récents/fréquents facultatifs selon les paramètres Windows et les API disponibles |
| Options de lancement | Activation par vignette, arguments, répertoire de travail et élévation pour les applications/raccourcis de bureau compatibles ; Windows gère l’UAC |
| Navigation | Dépliage au survol facultatif, Documents/Téléchargements/Musique/Images/Vidéos/Réseau/dossier personnel sélectionnables, compte et alimentation |
| Apparence | Cinq familles de couleurs avec variantes claires/sombres, mode système, arrière-plan/accent RVB personnalisés, fond d’écran, teinte de Démarrer, couleurs par vignette, contrôles et barres assortis, contraste élevé, animation facultative |
| Vignettes dynamiques | 15 exemples JavaScript, quatre tailles adaptatives, actions locales au clic, mises à jour activables, configuration des données, création d’exemples, import et bibliothèque surveillée automatiquement |
| API de vignette | Heure du PC, langue/taille, état conservé, CPU/mémoire/réseau/batterie/stockage, photos sélectionnées, données JSON/Web API/RSS/Atom/fichiers mises en cache et intégration locale |
| Langues | Anglais, japonais, allemand, français, espagnol, coréen, chinois simplifié et traditionnel, y compris les vignettes fournies |
| Données et mises à jour | Paramètres locaux, import/export/réinitialisation, sauvegardes et migration, historique d’applications effaçable, version/build/licences, vérification sans clé API et installation manuelle |
| Distribution | Paquets portables Windows 10/11 x64 et ARM64 avec runtime et notices ; intégration réversible sans modifier Explorer |

[Options et valeurs par défaut (anglais)](docs/start-behavior.md) · [Compatibilité (anglais)](docs/fidelity.md)

## Rechercher et configurer

![Résultats d’applications, de paramètres Windows et de fichiers locaux](docs/images/unified-search.png)

Dans **Settings → Start behavior**, désactivez séparément la recherche de paramètres/fichiers, les menus de documents, les effets de survol, les groupes/dossiers automatiques, le défilement aux bords et les indications. L’enregistrement des applications fréquentes est désactivé par défaut.

| Comportement | Lancement par vignette |
| --- | --- |
| ![Options de comportement](docs/images/start-behavior.png) | ![Arguments, répertoire de travail et administrateur](docs/images/launch-options.png) |

**Use custom launch options** peut être désactivé sans effacer les champs. Sinon, les raccourcis conservent leur comportement Windows habituel.

## Vignettes et données personnelles

![Vignettes dynamiques fournies](docs/images/live-tiles.png)

- **Heure et concentration :** Orbit clock, Digital clock, Month dots, Focus cycle, Year in motion et Countdown.
- **PC :** CPU pulse, Memory flow, Network flow, Battery et Storage.
- **Photos :** Photo moments, avec un dossier choisi et passage à l’image suivante au clic.
- **Connexions :** Weather, Feed headlines et JSON monitor.

Clic droit sur l’arrière-plan → **Add live tile**. Un clic agit dans la vignette : pause du minuteur, photo suivante, etc. Pour Weather/Feed/JSON, ouvrez **Configure data sources…**, indiquez une URL ou un fichier et enregistrez. **Refresh data now** demande une nouvelle valeur.

**La météo utilise Berlin comme exemple initial, pas votre position.** ReStart ne déduit pas votre région. Modifiez les coordonnées et le nom du lieu avec le [guide météo](docs/javascript-tiles.md#weather-location). Les identifiants sont chiffrés pour votre utilisateur Windows et exclus des exports. Le cache reste utilisable lors d’une panne ; masquer Démarrer interrompt les lectures.

Une vignette comprend `manifest.json` et `tile.js`. `render(ctx)` lit un instantané limité et renvoie les données d’affichage natif, avec une disposition selon la taille. JavaScript ne dispose pas d’un accès général aux fichiers, au réseau ou au CLR. Placez les paquets sous `%LOCALAPPDATA%\ReStart\TileLibrary` pour leur détection automatique. [Exemples](examples/tiles) · [API JavaScript (anglais)](docs/javascript-tiles.md) · [Producteur local](examples/integrations/write-tile-data.ps1)

## Commandes usuelles

| Action | Résultat |
| --- | --- |
| Touche Windows / `Ctrl+Alt+Space` | Ouvrir ou fermer |
| Saisir / `Ctrl+F` | Rechercher ; Entrée lance le résultat surligné, les flèches changent la sélection |
| `Esc` | Fermer la recherche, le dossier ou la surcouche, puis Démarrer |
| Glisser une vignette / un titre de groupe | Déplacer ; un espace vide crée une épingle indépendante |
| Superposer une vignette indépendante environ 0,3 s | Créer un groupe lorsque l’aperçu est prêt |
| Superposer une vignette d’un groupe environ 0,6 s | Créer un dossier ; les dossiers existants acceptent des ajouts |
| **Edit layout** / `Ctrl+E` | Sélectionner plusieurs vignettes sans lancer d’applications |
| `Ctrl+A` / `Delete` en édition | Sélectionner les vignettes racines / détacher la sélection |
| `Ctrl+Z` / `Ctrl+Y` ou `Ctrl+Shift+Z` | Annuler / rétablir la disposition |
| Crayon / double-clic sur un nom | Renommer directement le groupe ou dossier |
| `Shift` + molette | Défiler horizontalement |

Les raccourcis Windows sont transmis au système. L’historique conserve jusqu’à 100 modifications dans une limite de 32 Mio pendant la session ; les champs de texte gardent leur propre annulation. La réouverture retourne aux vignettes épinglées.

## Paramètres, confidentialité et mises à jour

Paramètres : `%LOCALAPPDATA%\ReStart\layout.json`. L’activité facultative est séparée dans `activity.json` et peut être effacée. Les migrations gardent des sauvegardes ; [détails (anglais)](docs/migration.md). About affiche version, build et licences.

Les mises à jour consultent les GitHub Releases publiques sans clé API. Quittez ReStart et remplacez manuellement toute l’installation extraite. Si son emplacement change, réenregistrez le démarrage et l’intégration Explorer. Aucune publicité ni analyse d’usage ; listes d’applications, dispositions et choix de photos ne sont pas téléversés. Les fournisseurs de données configurés reçoivent leurs requêtes. [Sécurité (anglais)](SECURITY.md)

Pour désinstaller : désactivez le lancement à la connexion et l’intégration Explorer, quittez via la zone de notification et supprimez le dossier d’installation. Supprimez les données locales uniquement pour effacer aussi paramètres et vignettes personnelles.

## Compiler

Utilisez Windows et le [SDK .NET 10 stable](https://dotnet.microsoft.com/download/dotnet/10.0) indiqué dans `global.json`. Fermez l’application avant de remplacer ses fichiers.

```powershell
git clone https://github.com/LastResolution/ReStart.git
cd ReStart
dotnet restore ReStart.slnx --locked-mode
dotnet build ReStart.slnx -c Release --no-restore
dotnet test ReStart.slnx -c Release --no-build
.\src\ReStart.App\bin\Release\net10.0-windows\ReStart.exe
# Aperçu isolé :
dotnet run --project src/ReStart.App -c Release -- --preview
# Paquet portable avec runtime :
./scripts/package.ps1 -Runtime win-x64
```

Pour ARM64, utilisez `win-arm64`. ZIP et sommes de contrôle sont dans `artifacts/packages`. Distribuez toujours le ZIP complet.

[Bugs et idées](https://github.com/LastResolution/ReStart/issues/new/choose) · [Contribuer (anglais)](CONTRIBUTING.md) · [Architecture (anglais)](docs/architecture.md) · [Tests (anglais)](docs/testing.md) · [Versions](https://github.com/LastResolution/ReStart/releases)

ReStart est un projet MIT indépendant, inspiré du menu Démarrer plein écran des dernières versions de Windows 10 et non affilié à Microsoft. Captures et animation montrent la vraie application avec une démonstration anglaise, des mesures synthétiques et des couleurs de photos générées. Noms et icônes appartiennent à leurs propriétaires. [Licence MIT](LICENSE) · [Assistance (anglais)](SUPPORT.md)
