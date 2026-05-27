# Sélecteur de thème clair/sombre/système — Plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ajouter un bouton-icône dans le header qui bascule l'UI entre thème clair, sombre et système, avec mémorisation par navigateur et sans flash au chargement.

**Architecture:** On s'appuie sur le mode sombre natif de Bootstrap 5.3.3 (`data-bs-theme` sur `<html>`). Un script inline dans le `<head>` applique le thème persisté (`localStorage`) avant le premier paint et expose une mini-API JS. Un composant Blazor `ThemeToggle` cycle la préférence et appelle cette API. Quelques classes Bootstrap à couleur figée sont rendues adaptatives pour que le mode sombre soit lisible.

**Tech Stack:** Blazor Server / .NET 9, Bootstrap 5.3.3, JS interop (`IJSRuntime`), `localStorage`.

**Note sur les tests :** Ce dépôt n'a pas de projet de test (cf. `CLAUDE.md` : schéma créé via `EnsureCreated`, pas de migrations ni de tests). La fonctionnalité est purement UI. Chaque tâche se vérifie par `dotnet build` (compilation) puis une **vérification manuelle au navigateur** décrite explicitement. Lancer l'app : `dotnet run` depuis la racine du worktree → http://localhost:5134.

---

### Task 1: Script de thème inline (anti-flash) dans `App.razor`

**Files:**
- Modify: `Components/App.razor` (dans le `<head>`, après les `<link>` CSS, avant `<ImportMap />`)

- [ ] **Step 1: Ajouter le script inline dans le `<head>`**

Insérer ce bloc dans `Components/App.razor` immédiatement après la ligne
`<link rel="stylesheet" href="@Assets["StoryPoints.styles.css"]" />` et avant `<ImportMap />` :

```html
    <script>
        (function () {
            const KEY = 'sp-theme';
            const mq = window.matchMedia('(prefers-color-scheme: dark)');
            const resolve = p => (p === 'dark' || p === 'light') ? p : (mq.matches ? 'dark' : 'light');
            const apply = p => document.documentElement.setAttribute('data-bs-theme', resolve(p));
            window.storyPointsTheme = {
                get: () => localStorage.getItem(KEY) || 'system',
                set: p => { localStorage.setItem(KEY, p); apply(p); }
            };
            apply(window.storyPointsTheme.get());
            mq.addEventListener('change', () => {
                if (window.storyPointsTheme.get() === 'system') apply('system');
            });
        })();
    </script>
```

- [ ] **Step 2: Compiler**

Run: `dotnet build`
Expected: `Build succeeded`, 0 erreur.

- [ ] **Step 3: Vérification manuelle (anti-flash + résolution système)**

Lancer `dotnet run`, ouvrir http://localhost:5134, puis dans la console du navigateur (F12) :
- `document.documentElement.getAttribute('data-bs-theme')` → `"light"` ou `"dark"` selon le thème de l'OS (pas `null`, pas `"system"`).
- `window.storyPointsTheme.get()` → `"system"` (aucune préférence encore stockée).
- `window.storyPointsTheme.set('dark')` → la page passe immédiatement en sombre ; recharger (F5) → reste sombre, **sans** flash clair au chargement.
- `localStorage.removeItem('sp-theme')` puis F5 → revient au thème de l'OS.

- [ ] **Step 4: Commit**

```bash
git add Components/App.razor
git commit -m "feat: applique le thème persisté avant le premier rendu (anti-flash)"
```

---

### Task 2: Composant `ThemeToggle`

**Files:**
- Create: `Components/Layout/ThemeToggle.razor`

- [ ] **Step 1: Créer le composant**

Créer `Components/Layout/ThemeToggle.razor` avec ce contenu exact :

```razor
@inject IJSRuntime JS

<button type="button"
        class="btn btn-outline-secondary btn-sm d-inline-flex align-items-center"
        title="@Title" aria-label="@Title" @onclick="Cycle">
    @switch (_pref)
    {
        case "light":
            <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
                <path d="M8 11a3 3 0 1 1 0-6 3 3 0 0 1 0 6m0 1a4 4 0 1 0 0-8 4 4 0 0 0 0 8M8 0a.5.5 0 0 1 .5.5v2a.5.5 0 0 1-1 0v-2A.5.5 0 0 1 8 0m0 13a.5.5 0 0 1 .5.5v2a.5.5 0 0 1-1 0v-2A.5.5 0 0 1 8 13m8-5a.5.5 0 0 1-.5.5h-2a.5.5 0 0 1 0-1h2a.5.5 0 0 1 .5.5M3 8a.5.5 0 0 1-.5.5h-2a.5.5 0 0 1 0-1h2A.5.5 0 0 1 3 8m10.657-5.657a.5.5 0 0 1 0 .707l-1.414 1.415a.5.5 0 1 1-.707-.708l1.414-1.414a.5.5 0 0 1 .707 0m-9.193 9.193a.5.5 0 0 1 0 .707L3.05 13.657a.5.5 0 0 1-.707-.707l1.414-1.414a.5.5 0 0 1 .707 0m9.193 2.121a.5.5 0 0 1-.707 0l-1.414-1.414a.5.5 0 0 1 .707-.707l1.414 1.414a.5.5 0 0 1 0 .707M4.464 4.465a.5.5 0 0 1-.707 0L2.343 3.05a.5.5 0 1 1 .707-.707l1.414 1.414a.5.5 0 0 1 0 .708"/>
            </svg>
            break;
        case "dark":
            <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
                <path d="M6 .278a.77.77 0 0 1 .08.858 7.2 7.2 0 0 0-.878 3.46c0 4.021 3.278 7.277 7.318 7.277q.792-.001 1.533-.16a.79.79 0 0 1 .81.316.73.73 0 0 1-.031.893A8.35 8.35 0 0 1 8.344 16C3.734 16 0 12.286 0 7.71 0 4.266 2.114 1.312 5.124.06A.75.75 0 0 1 6 .278"/>
            </svg>
            break;
        default:
            <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
                <path d="M0 4s0-2 2-2h12s2 0 2 2v6s0 2-2 2h-4q0 .25.25.5H11a.5.5 0 0 1 0 1H5a.5.5 0 0 1 0-1h.75q.25-.25.25-.5H2s-2 0-2-2zm1.398-.855a.76.76 0 0 0-.254.302A1.5 1.5 0 0 0 1 4.01V10c0 .325.078.502.145.602q.105.156.302.254a1.5 1.5 0 0 0 .538.143L2.01 11H14c.325 0 .502-.078.602-.145a.76.76 0 0 0 .254-.302 1.5 1.5 0 0 0 .143-.538L15 9.99V4c0-.325-.078-.502-.145-.602a.76.76 0 0 0-.302-.254A1.5 1.5 0 0 0 14.01 3H2c-.325 0-.502.078-.602.145"/>
            </svg>
            break;
    }
</button>

@code {
    private string _pref = "system";

    private string Title => _pref switch
    {
        "light" => "Thème : clair",
        "dark" => "Thème : sombre",
        _ => "Thème : système"
    };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            var stored = await JS.InvokeAsync<string>("storyPointsTheme.get");
            if (stored is "light" or "dark" or "system" && stored != _pref)
            {
                _pref = stored;
                StateHasChanged();
            }
        }
    }

    private async Task Cycle()
    {
        _pref = _pref switch
        {
            "light" => "dark",
            "dark" => "system",
            _ => "light"
        };
        await JS.InvokeVoidAsync("storyPointsTheme.set", _pref);
    }
}
```

Notes : l'ordre du cycle est Clair → Sombre → Système → Clair (depuis le défaut Système, le premier clic donne Clair). `OnAfterRenderAsync(firstRender)` resynchronise l'icône avec la préférence réellement persistée une fois le circuit connecté (au prérendu, l'icône « système » s'affiche par défaut puis se corrige).

- [ ] **Step 2: Compiler**

Run: `dotnet build`
Expected: `Build succeeded`, 0 erreur. (Le composant n'est pas encore référencé ; on vérifie seulement qu'il compile.)

- [ ] **Step 3: Commit**

```bash
git add Components/Layout/ThemeToggle.razor
git commit -m "feat: ajoute le composant ThemeToggle (bouton qui cycle clair/sombre/système)"
```

---

### Task 3: Brancher le bouton dans le header + classes adaptatives

**Files:**
- Modify: `Components/Layout/MainLayout.razor`

- [ ] **Step 1: Rendre le header adaptatif et insérer le bouton**

Dans `Components/Layout/MainLayout.razor`, remplacer le bloc `<header>…</header>` (lignes 3-9) par :

```razor
<header class="border-bottom bg-body-tertiary px-3 py-2 d-flex justify-content-between align-items-center">
    <a href="/" class="d-inline-flex align-items-center text-decoration-none text-body">
        <img src="favicon.svg" alt="StoryPoints" width="28" height="28" class="me-2" />
        <strong>StoryPoints</strong>
    </a>
    <div class="d-flex align-items-center gap-2">
        <ThemeToggle />
        <AboutMenu />
    </div>
</header>
```

Changements : `bg-white` → `bg-body-tertiary`, `text-dark` → `text-body`, et `<AboutMenu />` enveloppé avec `<ThemeToggle />` dans un conteneur `d-flex align-items-center gap-2`.

- [ ] **Step 2: Compiler**

Run: `dotnet build`
Expected: `Build succeeded`, 0 erreur.

- [ ] **Step 3: Vérification manuelle (bouton fonctionnel)**

`dotnet run`, ouvrir http://localhost:5134 :
- Le bouton apparaît à gauche du menu « À propos », avec une icône.
- Cliquer plusieurs fois : l'UI cycle clair → sombre → système, l'icône change (☀/☾/écran) et l'info-bulle (`title`) reflète l'état.
- Le header et le titre restent lisibles en sombre comme en clair.
- Recharger la page : le dernier choix est conservé et l'icône correspond.

- [ ] **Step 4: Commit**

```bash
git add Components/Layout/MainLayout.razor
git commit -m "feat: place le sélecteur de thème dans le header et rend l'en-tête adaptatif"
```

---

### Task 4: Badges adaptatifs dans `RoomPage`

**Files:**
- Modify: `Components/Pages/RoomPage.razor` (fonction `VoteBadgeClass` ~lignes 88-95 ; badge « en attente » ~ligne 124)

- [ ] **Step 1: Rendre neutres les badges de vote adaptatifs**

Dans `Components/Pages/RoomPage.razor`, remplacer la fonction `VoteBadgeClass` par (les trois `bg-dark` neutres deviennent `text-bg-secondary` ; min/max sémantiques inchangés) :

```razor
            string VoteBadgeClass(string? vote)
            {
                if (vote is null || revealedResults is null) return "text-bg-secondary";
                if (revealedResults.Min == revealedResults.Max) return "text-bg-secondary";
                if (vote == revealedResults.Min) return "bg-danger";
                if (vote == revealedResults.Max) return "bg-success";
                return "text-bg-secondary";
            }
```

- [ ] **Step 2: Rendre le badge « en attente » adaptatif**

Toujours dans `RoomPage.razor`, remplacer la ligne :

```razor
                                <span class="badge bg-light text-muted">en attente</span>
```

par (badge en contour, lisible dans les deux thèmes) :

```razor
                                <span class="badge border text-body-secondary">en attente</span>
```

- [ ] **Step 3: Compiler**

Run: `dotnet build`
Expected: `Build succeeded`, 0 erreur.

- [ ] **Step 4: Vérification manuelle (contraste des badges en sombre)**

`dotnet run`, ouvrir http://localhost:5134, créer un salon, basculer en thème **sombre** :
- Le badge « en attente » est lisible (texte gris clair sur fond sombre, contour visible), pas un pavé blanc.
- Après révélation d'un vote, les badges neutres (ni min ni max) sont gris lisibles ; le min reste rouge, le max reste vert.

- [ ] **Step 5: Commit**

```bash
git add Components/Pages/RoomPage.razor
git commit -m "feat: rend les badges de RoomPage lisibles en thème sombre"
```

---

### Task 5: Vérification d'ensemble (critères de réussite de la spec)

**Files:** aucun (vérification uniquement)

- [ ] **Step 1: Recette complète au navigateur**

`dotnet run`, http://localhost:5134. Vérifier les 5 critères de la spec :
1. **Défaut système, sans flash** : `localStorage.removeItem('sp-theme')` + F5 → suit l'OS, aucun flash clair.
2. **Cycle + icône** : cliquer le bouton → clair → sombre → système, icône et info-bulle cohérentes.
3. **Persistance** : choisir « sombre », F5 → reste sombre.
4. **Système en direct** : préférence « système », changer le thème clair/sombre de l'OS → l'app suit sans recharger.
5. **Lisibilité sombre** : sur `/` et dans un salon (membres, votes, résultats, badges), tout reste lisible et contrasté.

- [ ] **Step 2: Build final propre**

Run: `dotnet build`
Expected: `Build succeeded`, 0 erreur, 0 warning nouveau.
