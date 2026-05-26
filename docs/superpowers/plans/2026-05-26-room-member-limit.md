# Room Member Limit — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Limiter le nombre de membres par salon via un paramètre appsettings, défaut 10, afin d'éviter l'explosion des connexions SignalR Blazor Server.

**Architecture:** `MaxMembersPerRoom` est ajouté à `RoomLimitOptions` et passé au constructeur de `RoomState` via `RoomCoordinator`. `RoomState.Join()` retourne `bool` — `false` si le salon est plein — et `RoomPage.razor` affiche un message d'erreur en cas de refus.

**Tech Stack:** .NET 9, Blazor Server Interactive, C# 13

---

## Fichiers modifiés

| Fichier | Rôle de la modification |
|---|---|
| `Services/RoomLimitOptions.cs` | Nouvelle propriété `MaxMembersPerRoom` |
| `appsettings.json` | Valeur runtime `MaxMembersPerRoom: 10` |
| `Services/RoomCoordinator.cs` | Injection `IOptionsMonitor`, passage `maxMembers` à `RoomState`; `RoomState.Join()` → `bool`, champ `_maxMembers`, propriété `MaxMembers` |
| `Components/Pages/RoomPage.razor` | Gestion du retour `bool` de `Join()`, champ `roomFull`, message d'erreur |

---

## Task 1 : Ajouter `MaxMembersPerRoom` à la configuration

**Files:**
- Modify: `Services/RoomLimitOptions.cs`
- Modify: `appsettings.json`

- [ ] **Step 1 : Ajouter la propriété dans `RoomLimitOptions`**

Remplacer le contenu de `Services/RoomLimitOptions.cs` :

```csharp
namespace StoryPoints.Services;

public class RoomLimitOptions
{
    public const string SectionName = "RoomLimits";

    public int MaxTotalRooms { get; set; } = 10_000;
    public int MaxCreationsPerHourPerIp { get; set; } = 20;
    public int MaxMembersPerRoom { get; set; } = 10;
}
```

- [ ] **Step 2 : Ajouter la valeur dans `appsettings.json`**

Remplacer la section `RoomLimits` dans `appsettings.json` :

```json
"RoomLimits": {
  "MaxTotalRooms": 1000,
  "MaxCreationsPerHourPerIp": 10,
  "MaxMembersPerRoom": 10
}
```

- [ ] **Step 3 : Vérifier que le build passe**

```powershell
dotnet build
```

Résultat attendu : `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 4 : Commit**

```bash
git add Services/RoomLimitOptions.cs appsettings.json
git commit -m "feat: add MaxMembersPerRoom config (default 10)"
```

---

## Task 2 : Enforcer la limite dans `RoomState` et `RoomCoordinator`

**Files:**
- Modify: `Services/RoomCoordinator.cs`

- [ ] **Step 1 : Ajouter le champ et le paramètre constructeur dans `RoomState`**

Dans `RoomCoordinator.cs`, localiser la classe `RoomState`. Ajouter `_maxMembers` parmi les champs privés (après `_maxVoteDuration`) :

```csharp
private readonly TimeSpan? _maxVoteDuration;
private readonly int _maxMembers;
```

Puis mettre à jour le constructeur de `RoomState` pour recevoir `maxMembers` :

```csharp
public RoomState(Guid id, Scale scale, TimeSpan? maxVoteDuration, int countdownSeconds, double revealThreshold, int maxMembers)
{
    Id = id;
    Scale = scale;
    _maxVoteDuration = maxVoteDuration;
    _countdownSeconds = countdownSeconds;
    _revealThreshold = revealThreshold;
    _maxMembers = maxMembers;
}
```

Ajouter la propriété publique `MaxMembers` (après `VoteDeadlineUtc`) :

```csharp
public int MaxMembers => _maxMembers;
```

- [ ] **Step 2 : Modifier `RoomState.Join()` pour retourner `bool`**

Remplacer la méthode `Join` :

```csharp
public bool Join(Guid memberId, string name)
{
    lock (_lock)
    {
        if (!_members.ContainsKey(memberId) && _members.Count >= _maxMembers)
            return false;
        _members[memberId] = name;
    }
    Evaluate();
    RaiseChanged();
    return true;
}
```

> Note : les membres déjà présents (reconnexion circuit) passent toujours, seuls les nouveaux membres sont bloqués.

- [ ] **Step 3 : Injecter `IOptionsMonitor<RoomLimitOptions>` dans `RoomCoordinator` et passer `maxMembers` à `RoomState`**

Remplacer la classe `RoomCoordinator` (sans toucher `RoomState`) :

```csharp
public sealed class RoomCoordinator
{
    private const int CountdownSeconds = 10;
    private const double RevealThreshold = 2.0 / 3.0;

    private readonly IOptionsMonitor<RoomLimitOptions> _options;
    private readonly ConcurrentDictionary<Guid, RoomState> _rooms = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _pendingActivity = new();

    public RoomCoordinator(IOptionsMonitor<RoomLimitOptions> options)
    {
        _options = options;
    }

    public RoomState GetOrCreate(Guid roomId, Scale scale, TimeSpan? maxVoteDuration)
    {
        return _rooms.GetOrAdd(roomId, id =>
        {
            var state = new RoomState(id, scale, maxVoteDuration, CountdownSeconds, RevealThreshold,
                _options.CurrentValue.MaxMembersPerRoom);
            state.Changed += () => _pendingActivity[id] = DateTime.UtcNow;
            _pendingActivity[id] = DateTime.UtcNow;
            return state;
        });
    }

    public RoomState? TryGet(Guid roomId) =>
        _rooms.TryGetValue(roomId, out var state) ? state : null;

    public IReadOnlyDictionary<Guid, DateTime> DrainActivity()
    {
        var snapshot = new Dictionary<Guid, DateTime>();
        foreach (var key in _pendingActivity.Keys.ToList())
        {
            if (_pendingActivity.TryRemove(key, out var ts))
            {
                snapshot[key] = ts;
            }
        }
        return snapshot;
    }

    public void Evict(Guid roomId)
    {
        _rooms.TryRemove(roomId, out _);
        _pendingActivity.TryRemove(roomId, out _);
    }
}
```

Il faut aussi ajouter le `using` manquant en tête du fichier :

```csharp
using Microsoft.Extensions.Options;
```

- [ ] **Step 4 : Vérifier que le build passe**

```powershell
dotnet build
```

Résultat attendu : `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 5 : Commit**

```bash
git add Services/RoomCoordinator.cs
git commit -m "feat: enforce MaxMembersPerRoom in RoomState.Join"
```

---

## Task 3 : Afficher l'erreur de salon complet dans `RoomPage.razor`

**Files:**
- Modify: `Components/Pages/RoomPage.razor`

- [ ] **Step 1 : Ajouter l'injection `IOptionsMonitor` et le using**

Dans le bloc de directives en haut de `RoomPage.razor`, après les `@inject` existants, ajouter :

```razor
@using Microsoft.Extensions.Options
@inject IOptionsMonitor<RoomLimitOptions> RoomLimits
```

- [ ] **Step 2 : Ajouter le champ `roomFull` dans le bloc `@code`**

Dans le bloc `@code`, après `private string memberName = string.Empty;`, ajouter :

```csharp
private bool roomFull;
```

- [ ] **Step 3 : Afficher le message d'erreur dans le formulaire**

Dans le template HTML, dans le formulaire de la section `else if (!joined)`, ajouter le message d'erreur juste avant le bouton "Rejoindre" :

```razor
@if (roomFull)
{
    <div class="alert alert-warning py-2 mt-2">
        Ce salon est complet (@(state?.MaxMembers ?? RoomLimits.CurrentValue.MaxMembersPerRoom) membres maximum).
    </div>
}
<button type="submit" class="btn btn-primary w-100" disabled="@string.IsNullOrWhiteSpace(memberName)">
    Rejoindre
</button>
```

> Remplacer uniquement le `<button type="submit"...>` existant par le bloc ci-dessus (alert + bouton).

- [ ] **Step 4 : Modifier `JoinAsync()` pour vérifier le retour de `Join()`**

Remplacer la méthode `JoinAsync` :

```csharp
private async Task JoinAsync()
{
    if (state is null) return;
    memberName = memberName.Trim();
    if (memberName.Length == 0) return;

    try
    {
        await Storage.SetAsync("memberName", memberName);
    }
    catch { /* ignore */ }

    if (!state.Join(memberId, memberName))
    {
        roomFull = true;
        return;
    }

    roomFull = false;
    Tracker.Track(RoomId, memberId, memberName);
    joined = true;
    RefreshLocal();
}
```

- [ ] **Step 5 : Vérifier que le build passe**

```powershell
dotnet build
```

Résultat attendu : `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 6 : Tester manuellement**

```powershell
dotnet run
```

Scénarios à valider :
1. Ouvrir le salon dans 11 onglets/navigateurs différents et tenter de rejoindre avec le 11ème → message d'erreur "Ce salon est complet (10 membres maximum)".
2. L'un des 10 membres ferme son onglet (déconnexion = `Leave`) → le 11ème peut maintenant rejoindre.
3. Un membre existant qui perd la connexion et se reconnecte (F5) → rejoint sans être bloqué.

- [ ] **Step 7 : Commit**

```bash
git add Components/Pages/RoomPage.razor
git commit -m "feat: block join when room is full, show error message"
```
