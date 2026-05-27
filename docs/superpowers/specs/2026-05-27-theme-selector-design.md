# Sélecteur de thème clair / sombre / système

**Date** : 2026-05-27
**Statut** : Design validé

## Objectif

Permettre à l'utilisateur de basculer l'interface entre thème **clair**, **sombre** et
**système** (suit la préférence de l'OS), via un bouton-icône dans le header. Le choix est
mémorisé par navigateur et appliqué sans « flash » de thème incorrect au chargement.

## Décisions

- **Forme du contrôle** : un seul bouton-icône qui *cycle* Clair → Sombre → Système → Clair…
- **Thème par défaut** (premier chargement, avant tout clic) : **Système**.
- **Persistance** : `localStorage` côté navigateur (pas de stockage serveur — cohérent avec
  l'identité actuelle sans authentification et le `memberName` déjà en `localStorage`).
- **Mécanisme** : support natif de Bootstrap 5.3.3 via l'attribut `data-bs-theme` sur `<html>`.

## Architecture

### 1. Mécanisme de fond — Bootstrap natif

Bootstrap 5.3.3 bascule en mode sombre dès que l'élément `<html>` porte
`data-bs-theme="dark"` (ou `"light"`). On s'appuie entièrement là-dessus : aucune feuille de
style « sombre » manuscrite.

Trois préférences possibles, stockées telles quelles : `light`, `dark`, `system`.
La préférence `system` est **résolue** en `light`/`dark` à l'exécution via
`window.matchMedia('(prefers-color-scheme: dark)')`.

### 2. Persistance + zéro flash — script inline dans `App.razor`

En Blazor Server, pendant le prérendu aucun JS n'a encore tourné : il faut poser le thème
**avant** que le `<body>` ne s'affiche pour éviter un flash clair.

Un petit script **inline dans le `<head>`** de `Components/App.razor`, placé **avant** le
`<HeadOutlet>` / le chargement de `blazor.web.js` :

- lit la préférence dans `localStorage` (clé **`sp-theme`**, défaut `system`) ;
- résout la préférence et pose `data-bs-theme` sur `document.documentElement` immédiatement ;
- expose une petite API globale pour le composant Blazor :
  - `window.storyPointsTheme.get()` → renvoie la préférence courante (`light|dark|system`) ;
  - `window.storyPointsTheme.set(pref)` → persiste **et** applique la préférence ;
- écoute `matchMedia('(prefers-color-scheme: dark)')` `change` et **réapplique uniquement si**
  la préférence courante est `system` (pour suivre un changement d'OS en direct).

Esquisse :

```html
<script>
  (function () {
    const KEY = 'sp-theme';
    const mq = window.matchMedia('(prefers-color-scheme: dark)');
    const resolve = p => (p === 'dark' || p === 'light')
        ? p
        : (mq.matches ? 'dark' : 'light');
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

### 3. Le bouton — `Components/Layout/ThemeToggle.razor`

Composant nouveau, inséré dans le header de `MainLayout.razor` **juste avant** `<AboutMenu />`.

- État local : préférence courante (`light` / `dark` / `system`), initialisée à `system`.
- `OnAfterRenderAsync(firstRender)` : lit `storyPointsTheme.get()` via `IJSRuntime` pour
  **synchroniser l'icône** avec la valeur réellement persistée, puis `StateHasChanged()`.
- Clic : avance la préférence dans l'ordre Clair → Sombre → Système → Clair, appelle
  `storyPointsTheme.set(...)`, met à jour l'icône.
- Icône reflétant la préférence : ☀ clair, ☾ sombre, 🖵 système. **SVG inline** pour rester
  cohérent avec le menu « À propos » (qui utilise déjà des SVG).
- Accessibilité : `aria-label` / `title` décrivant l'état courant (ex. « Thème : système »).
- Prérendu (sans JS) : l'icône part par défaut sur « système » puis se corrige à la connexion
  du circuit (décalage d'une frame, acceptable).

### 4. Lisibilité réelle en mode sombre — audit des couleurs figées

Le bouton n'a de valeur que si l'UI est correcte en sombre. Audit ciblé des classes à couleur
**fixe** (qui ne s'inversent pas automatiquement en Bootstrap 5.3) :

- **`Components/Layout/MainLayout.razor`**
  - header : `bg-white` → `bg-body-tertiary`
  - lien titre : `text-dark` → `text-body`
- **`Components/Pages/RoomPage.razor`**
  - badge « en attente » : `bg-light text-muted` → équivalent adaptatif (`text-bg-secondary`
    ou `border` + `text-body-secondary`)
  - badges de résultat **neutres** `bg-dark` (fonction `VoteBadgeClass`) → adaptatif
    (`text-bg-secondary`). Les badges sémantiques `bg-danger` (min) / `bg-success` (max) /
    `bg-secondary` (AFK) et les `text-muted` restent identiques (déjà lisibles dans les deux
    thèmes).
- **`#blazor-error-ui`** (bandeau d'erreur jaune, `MainLayout.razor.css`) : **inchangé** —
  couleur volontairement fixe (`color-scheme: light only` + `lightyellow`).

## Hors périmètre (YAGNI)

- Pas de persistance serveur du thème.
- Pas de transition / animation de bascule.
- Pas de thèmes personnalisés au-delà des trois demandés.

## Fichiers touchés

| Fichier | Changement |
| --- | --- |
| `Components/App.razor` | Ajout du script inline de thème dans le `<head>` |
| `Components/Layout/ThemeToggle.razor` | **Nouveau** composant bouton-cycle |
| `Components/Layout/MainLayout.razor` | Insertion de `<ThemeToggle />` + 2 classes adaptatives |
| `Components/Pages/RoomPage.razor` | Badges « en attente » et résultats neutres → adaptatifs |

## Critères de réussite

1. Au premier chargement, l'app suit le thème de l'OS (clair ou sombre) sans flash.
2. Le bouton cycle Clair → Sombre → Système et l'icône reflète l'état courant.
3. Le choix survit à un rechargement de page (même navigateur).
4. En mode `system`, changer le thème de l'OS met l'app à jour en direct.
5. En mode sombre, header, cartes, listes de membres et badges restent lisibles (contraste correct).
