# SyncHTTP

Petit script en C# qui permet de synchroniser un dossier local avec un répertoire distant exposé via HTTP (ex : Apache avec index de fichiers activé).

## Fonctionnement

- Télécharge récursivement tous les fichiers et dossiers d'une URL distante.
- Crée les dossiers manquants localement.
- Télécharge les nouveaux fichiers uniquement.
- Supprime les fichiers locaux qui n'existent plus sur le serveur.

## Paramètres modifiables

string serverUrl = "https://ip-du-serveur/mp3/";
string localPath = "/home/mp3/";
