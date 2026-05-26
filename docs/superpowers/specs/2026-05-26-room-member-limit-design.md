# Design — Limite de membres par salon

**Date :** 2026-05-26
**Statut :** Approuvé

## Contexte

Chaque membre d'un salon correspond à un circuit Blazor Server (connexion SignalR persistante). Sans limite, un leak d'URL de salon permet à n'importe qui d'inscrire un grand nombre d'utilisateurs et d'exploser les connexions serveur. La limite est configurable via appsettings et s'applique à tous les membres présents, AFK compris.

## Configuration

Nouvelle propriété `MaxMembersPerRoom` dans `RoomLimitOptions` (défaut : `10`), sous la section `RoomLimits` existante.

```json
"RoomLimits": {
  "MaxTotalRooms": 1000,
  "MaxCreationsPerHourPerIp": 10,
  "MaxMembersPerRoom": 10
}
```

## Changements

### `Services/RoomLimitOptions.cs`

Ajout de `public int MaxMembersPerRoom { get; set; } = 10;`.

### `Services/RoomCoordinator.cs`

- Injecte `IOptions<RoomLimitOptions>` dans le constructeur.
- `GetOrCreate()` passe `options.Value.MaxMembersPerRoom` au constructeur de `RoomState`.

### `Services/RoomCoordinator.cs` — `RoomState`

- Nouveau paramètre `int maxMembers` dans le constructeur, stocké en champ `_maxMembers`.
- `Join()` passe de `void` à `bool`.
  - Sous le lock : si `_members.Count >= _maxMembers`, retourner `false` sans mutation ni `RaiseChanged()`.
  - Sinon : comportement identique à aujourd'hui, retourner `true`.

### `Components/Pages/RoomPage.razor`

- Ajoute un champ `private bool roomFull;`.
- `JoinAsync()` vérifie le `bool` retourné par `state.Join()`.
  - Si `false` : `roomFull = true`, ne pas passer `joined = true`.
  - Si `true` : comportement actuel inchangé, `roomFull = false`.
- Affiche un message d'erreur sous le formulaire quand `roomFull` :
  *"Ce salon est complet (X membres maximum)."*
- Le nombre maximum affiché dans le message est lu depuis `IOptions<RoomLimitOptions>`.

## Ce qui ne change pas

- Aucune modification de la base de données ni du `CleanupService`.
- `Home.razor` et la création de salon sont inchangés.
- Un abaissement de `MaxMembersPerRoom` via hot-reload n'éjecte pas les membres déjà présents dans les rooms actives.

## Comportement aux limites

- Un membre qui quitte le salon (`Leave`) libère son slot immédiatement.
- Un membre AFK occupe toujours un slot.
- `CircuitMemberTracker.Track` n'est appelé que si `Join()` retourne `true`.
