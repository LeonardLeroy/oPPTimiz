# oPPTimiz — Comparatif des moteurs d'optimisation

Ce document compare les approches mises en place pour optimiser la taille des
documents Office, les résultats obtenus, les dépendances/technologies utilisées
et les raisons de ces choix.

---

## 1. Contexte : ce qui existait

Le projet d'origine est un **add-in VSTO PowerPoint** (`Src/oPPTimiz/`) :

- **Techno** : .NET Framework 4.8 + COM `Microsoft.Office.Interop.PowerPoint`.
- **Méthode** : il **pilote l'interface de PowerPoint** — il déclenche la commande
  native `PicturesCompress` via des frappes clavier simulées (`SendKeys`) et
  supprime les masques/dispositions inutilisés via l'API COM.
- **Limites** :
  - Windows + Office obligatoires (VSTO est Windows-only par nature).
  - Fragile : dépend de la version d'Office (M365 = 16.0) et de la **langue** de
    l'UI (les raccourcis clavier changent).
  - PowerPoint uniquement.
  - N'optimise que ce que sait faire PowerPoint (baisse de DPI).

Un script PowerShell (`Src/Menu Contextuel/oPPTimiz.ps1`) reprend la même logique
pour le menu contextuel / la ligne de commande.

### Correctif apporté (issue « méthode sur expression Null »)

Le script lisait la variable `$processName` (inexistante) au lieu de
`$pptProcessName`. `$process` valait donc toujours `$null`, et `$process.Kill()`
levait *« Impossible d'appeler une méthode dans une expression Null »*. Corrigé,
bug reproduit puis vérifié en PowerShell.

---

## 2. Ce qu'on a ajouté : deux moteurs autonomes

Constat clé : un `.pptx`, `.docx` ou `.xlsx` est une **archive ZIP** de XML et de
médias. On peut donc l'optimiser **sans Office du tout**, en manipulant
directement l'archive. Les deux moteurs font la même chose :

1. redimensionner + recompresser les images sous `*/media/` ;
2. supprimer les médias non référencés par aucune partie ;
3. repacker le ZIP en compression DEFLATE maximale.

Cela couvre **PowerPoint, Word et Excel** d'un coup (même format OOXML).

| | `engine-python/` | `engine-dotnet/` |
|---|---|---|
| Langage | Python ≥ 3.10 | C# / .NET Framework 4.8 |
| Plateformes | **Windows, Linux, macOS** | **Windows uniquement** |
| Formats | pptx, docx, xlsx | pptx, docx, xlsx |
| Lib images | **Pillow** | **System.Drawing** (GDI+) |
| Dépendances | Pillow (PyPI) | **aucune** (intégré au framework) |
| ZIP | `zipfile` (deflate 9) | `System.IO.Compression` (Optimal) |
| Tests | 10 (pytest) | 8 (xUnit) |
| Rôle | Optimiseur **maximum**, cross-platform | Option **légère**, sans dépendance, intégrable à l'add-in VSTO |

---

## 3. Résultats mesurés (mêmes fichiers réels)

| Fichier | Taille | Python | .NET |
|---|---|---|---|
| Présentation `.pptx` (riche en images) | 3.4 Mo | **−27.85 %** (43 images) | −3.09 % (24 images) |
| Document `.docx` (sans image) | 11.7 Ko | **−19.21 %** (0 image) | −16.82 % (0 image) |
| Classeur `.xlsx` (1 image) | 265.5 Ko | −14.86 % (0 image) | **−18.51 %** (1 image) |

**Lecture importante** : l'écart entre les deux moteurs dépend du **contenu**.

- Sur un fichier **riche en images PNG** (la présentation), Python obtient −28 %
  contre seulement −3 % pour .NET. Le fichier n'est pas « trop léger » (il est très
  compressible) : l'écart vient des **codecs**. Pillow recompresse efficacement le
  JPEG **et** le PNG (`optimize=True`), alors que **System.Drawing** (GDI+) ne sait
  quasiment pas compresser un PNG — les captures/PNG ne rétrécissent pas et sont
  ignorées.
- Sur des fichiers **pauvres en images** (Word, Excel), les deux moteurs sont
  **comparables**, et le .NET fait même légèrement mieux sur le classeur Excel. Le
  gain vient alors surtout du **repack du ZIP** : Office compresse mal ses propres
  archives, et toutes les parties sont conservées à l'octet près — seul l'emballage
  change.

### Autres exemples (fichiers réels)

| Fichier | Taille | Python | .NET |
|---|---|---|---|
| Présentation A (.pptx, images) | 43.4 Mo | **−12.65 %** | −0.44 % |
| Présentation B (.pptx, images) | 13.3 Mo | **−52.06 %** ||
| Document A (.docx, images) | 19.6 Mo | −41.49 % | **−41.99 %** |
| Document B (.docx, photos) | 16.6 Mo | **−62.30 %** | −58.68 % |

Ces mesures **précisent** le constat — l'efficacité du .NET dépend du **type
d'images**, pas du poids du fichier :

- **Documents riches en photos (JPEG)** : le .NET **rivalise** avec Python (−42 %
  vs −41 %, −59 % vs −62 %) — `System.Drawing` recompresse correctement le JPEG.
- **Présentations riches en PNG** : le .NET reste faible (−0.4 %) là où Python
  exploite la recompression PNG (jusqu'à −52 %).
- Le .NET a **échoué** sur un `.pptx` stocké en ligne sur OneDrive (non synchronisé
  localement) ; le moteur Python (accès via WSL) l'a traité (voir §6).

---

## 4. Choix techniques et justifications

### Manipulation directe de l'OOXML (et non piloter Office)
Plutôt que de télécommander PowerPoint comme l'add-in d'origine, on ouvre
l'archive. **Pourquoi** : robuste (indépendant de la version/langue d'Office),
portable (aucun Office requis), plus puissant (on peut purger des médias, etc.)
et applicable à Word/Excel sans effort.

### Python + Pillow
**Pourquoi Pillow** : bibliothèque image standard de l'écosystème Python, mature,
multiplateforme, excellente sur JPEG et PNG. **Pourquoi Python** : portable
nativement (Windows/Linux/macOS), idéal pour une CLI et des tests reproductibles.

### .NET Framework 4.8 + System.Drawing
**Pourquoi net48** : c'est la version installée **par défaut** sous Windows 10/11
et la cible attendue chez EDF — pas besoin d'installer un runtime supplémentaire
(contrairement à .NET 8). C'est aussi la cible de l'add-in VSTO existant, donc la
lib `oPPTimiz.Core` peut y être branchée directement.

**Pourquoi System.Drawing** : intégré au framework → **zéro dépendance NuGet** à
déployer (contrainte EDF), et **déjà référencé** par le projet existant. Conséquence
assumée : compression plus faible que Pillow (voir §3).

**Alternatives écartées (pour l'instant)** :
- **SixLabors.ImageSharp** : 100 % géré, MIT, compatible net48, qualité proche de
  Pillow — mais ajoute des DLL NuGet à déployer.
- **Magick.NET** (ImageMagick) : qualité maximale, mais NuGet avec binaires natifs
  lourds et spécifiques à chaque plateforme — peu adapté à un déploiement EDF.

### Pas de cross-platform côté .NET
VSTO, GDI+ (`System.Drawing`) et .NET Framework 4.8 sont **Windows-only** par
nature. Le besoin cross-platform est donc couvert par le moteur **Python**.

---

## 5. Quel moteur utiliser ?

- **Besoin de la compression maximale, ou Linux/macOS, ou traitement en lot de
  fichiers Word/Excel/PowerPoint** → moteur **Python** (`engine-python/`).
- **Intégration à l'add-in PowerPoint / environnement EDF net48 sans dépendance,
  optimisation légère sur Windows** → moteur **.NET** (`engine-dotnet/`).

---

## 6. Limites connues

- Le moteur .NET (System.Drawing) compresse peu les PNG → faible gain sur les
  documents riches en captures d'écran (voir §3). En revanche il rivalise avec
  Python sur les fichiers riches en photos JPEG.
- Le CLI .NET peut échouer à lire un fichier **OneDrive non synchronisé localement**
  (placeholder « fichiers à la demande »). Hydrater le fichier (l'ouvrir une fois)
  ou désactiver les fichiers à la demande règle le problème.
- Les deux moteurs recompressent les images **dans leur format d'origine** (pas de
  PNG → JPEG) pour ne jamais avoir à réécrire les relations / `[Content_Types].xml`
  — c'est un choix de **sûreté** (le document reste valide). Une conversion de
  format apporterait plus de gain mais au prix d'un risque de corruption.
- L'add-in VSTO reste Windows/PowerPoint ; le brancher sur `oPPTimiz.Core` (pour
  qu'il utilise ce moteur et gagne Word/Excel) est une étape d'intégration à
  réaliser dans Visual Studio.
