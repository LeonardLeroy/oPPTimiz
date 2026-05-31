# oPPTimiz — moteur Python (cross-platform)

Optimiseur de documents Office **sans dépendance à Office**. Fonctionne sous
Windows, Linux et macOS, sur les fichiers `.pptx`, `.docx` et `.xlsx`.

Un fichier Office est une archive ZIP de parties XML et de médias. Ce moteur :

1. **redimensionne et recompresse** les images raster stockées sous `*/media/`;
2. **supprime les médias non référencés** par aucune partie du document;
3. **repacke l'archive** avec une compression DEFLATE maximale.

Contrairement à l'add-in PowerPoint historique (qui pilote l'interface de
PowerPoint via `SendKeys`), ce moteur manipule directement le format OOXML : il
est portable, plus efficace, et couvre Word/Excel gratuitement.

## Installation

```bash
pip install -e .
```

## Utilisation

```bash
opptimiz presentation.pptx
opptimiz presentation.pptx --keep
opptimiz rapport.docx --level intermediate
opptimiz classeur.xlsx --output /tmp/petit.xlsx
opptimiz deck.pptx --no-prune
```

Niveaux de compression :

| Niveau         | Dimension max | Qualité JPEG |
|----------------|---------------|--------------|
| `maximal`      | 1920 px       | 72           |
| `intermediate` | 2560 px       | 85           |

## Tests

```bash
pip install -e ".[test]"
pytest -v
```
