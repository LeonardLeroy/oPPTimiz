# oPPTimiz — moteur .NET Framework 4.8

Version C# du moteur d'optimisation, ciblant **.NET Framework 4.8** (installé par
défaut sous Windows 10/11, donc déployable sans prérequis supplémentaire) et
**sans aucune dépendance NuGet** : images via `System.Drawing`, archive via
`System.IO.Compression`.

Comme le moteur Python, il optimise les documents Office en manipulant
directement l'archive OOXML : recompression/redimensionnement des images sous
`*/media/`, purge des médias non référencés, repack du ZIP en DEFLATE maximal.
Il couvre `.pptx`, `.docx` et `.xlsx`.

> Cette partie est **Windows-only** (`System.Drawing`/GDI+ et .NET Framework le
> sont par nature). Pour Linux/macOS, utiliser le moteur Python (`engine-python/`).

## Projets

| Projet            | Cible | Rôle |
|-------------------|-------|------|
| `oPPTimiz.Core`   | net48 | Bibliothèque : `Optimizer`, recompression d'images |
| `oPPTimiz.Cli`    | net48 | Exécutable ligne de commande |
| `oPPTimiz.Tests`  | net48 | Tests xUnit |

## Build & tests

Nécessite le SDK .NET (avec le targeting pack .NET Framework 4.8) ou Visual Studio.

```bash
dotnet test oPPTimiz.Tests/oPPTimiz.Tests.csproj
dotnet build oPPTimiz.Cli/oPPTimiz.Cli.csproj -c Release
```

## Utilisation (CLI, mêmes options que l'existant)

```
oPPTimiz.exe -pptFile source [-compressionLevel [Maximal | Intermediate]] [-keepFile [0 | 1]]
```

## Brancher l'add-in VSTO dessus

L'add-in `Src/oPPTimiz/` peut référencer `oPPTimiz.Core` puis appeler
`Optimizer.OptimizeFile(...)` sur la présentation sauvegardée, à la place de
l'automatisation `PicturesCompress`/`SendKeys`. Cela le rend plus robuste et lui
permet de traiter aussi des fichiers Word/Excel. (Étape d'intégration à réaliser
dans Visual Studio.)
