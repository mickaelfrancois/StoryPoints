# StoryPoints

StoryPoints est une application web de **planning poker** (estimation en story points) en temps réel, construite avec **Blazor Server / Interactive Server** sur **.NET 9**. Créez un salon, partagez son URL, et votez ensemble sur l'échelle de votre choix.

## Prérequis

- [.NET 9 SDK](https://dotnet.microsoft.com/download) pour le développement local
- [Docker](https://www.docker.com/) (optionnel) pour l'exécution conteneurisée

## Lancer en local

```powershell
dotnet run                          # profil http  -> http://localhost:5134
dotnet run --launch-profile https   # profil https -> https://localhost:7014
dotnet watch                        # hot reload
```

La base SQLite (`StoryPoints.db`) est créée automatiquement au démarrage dans le répertoire de l'application.

## Lancer avec Docker

```powershell
docker build -t storypoints .
docker run -p 8080:8080 storypoints
```

L'application est alors accessible sur http://localhost:8080.

## Lancer avec Docker Compose

Créez un fichier `docker-compose.yml` à la racine du dépôt :

```yaml
services:
  storypoints:
    build: .
    image: storypoints:latest
    container_name: storypoints
    ports:
      - "8080:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      # Surcharges de configuration (cf. appsettings.json) — le double underscore
      # correspond à l'imbrication des sections :
      Cleanup__InactiveDays: "60"
      Cleanup__RunIntervalHours: "12"
      RoomLimits__MaxTotalRooms: "1000"
      RoomLimits__MaxCreationsPerHourPerIp: "10"
      RoomLimits__MaxMembersPerRoom: "10"
    restart: unless-stopped
```

Puis :

```powershell
docker compose up -d --build   # build + démarrage en arrière-plan
docker compose logs -f         # suivre les logs
docker compose down            # arrêt
```

Dans le conteneur, l'application écoute sur le port **8080** (HTTP).

### Persistance des données (optionnel)

Par conception, tout l'état *vivant* d'une session (membres connectés, votes du tour en cours, AFK, compte à rebours) est conservé **en mémoire** et perdu au redémarrage. La base SQLite ne stocke que les **métadonnées** des salons (échelle choisie, durée de vote, horodatages).

Pour conserver ces métadonnées entre deux redémarrages, montez le fichier de base via un bind mount en ajoutant au service :

```yaml
    volumes:
      - ./data/StoryPoints.db:/app/StoryPoints.db
```

Avant le premier démarrage, créez le fichier côté hôte (Docker le créerait sinon comme un répertoire) :

```powershell
New-Item -ItemType Directory -Force -Path .\data | Out-Null
if (-not (Test-Path .\data\StoryPoints.db)) { New-Item -ItemType File -Path .\data\StoryPoints.db | Out-Null }
```

> Le conteneur s'exécute en utilisateur non-root (`$APP_UID`). Sur un hôte Linux, assurez-vous que le fichier monté est accessible en écriture à cet utilisateur ; avec Docker Desktop (Windows/macOS), le partage de fichiers gère cela automatiquement.

## Configuration

Les clés de `appsettings.json` (liées via `IOptionsMonitor`, donc rechargeables à chaud) :

| Clé | Description |
| --- | --- |
| `Cleanup:InactiveDays` | Nombre de jours d'inactivité avant suppression d'un salon |
| `Cleanup:RunIntervalHours` | Intervalle d'exécution du service de nettoyage |
| `RoomLimits:MaxTotalRooms` | Nombre maximal de salons simultanés |
| `RoomLimits:MaxCreationsPerHourPerIp` | Créations de salons par heure et par IP |
| `RoomLimits:MaxMembersPerRoom` | Nombre maximal de membres par salon |

En conteneur, surchargez ces valeurs via des variables d'environnement en remplaçant `:` par `__` (voir l'exemple Docker Compose ci-dessus).
