# À propos menu — design

**Date:** 2026-05-27
**Status:** Approved

## Goal

Add an "À propos" dropdown menu to the app header, containing a link to the
project's GitHub repository and a display of the current app version.

## Scope

- New header dropdown labelled **À propos** (top-right of the header).
- Entries:
  - **Code source (GitHub)** → `https://github.com/mickaelfrancois/StoryPoints`
    (opens in a new tab).
  - A divider, then muted text **StoryPoints v\<version\>**.
- Out of scope: the RoK Music Player link (removed during brainstorming);
  any "about" modal; authentication; i18n changes (UI stays French).

## Approach

Blazor-controlled dropdown (no new JS dependency). The app is fully
`InteractiveServer`, so a `bool _open` toggled by `@onclick` drives Bootstrap's
`dropdown-menu show` classes. A transparent full-screen backdrop closes the menu
on an outside click.

Rejected alternatives:
- Native `<details>`/`<summary>`: simplest but no auto-close on outside click.
- Bootstrap JS bundle (`bootstrap.bundle.min.js`, already vendored): loads ~60 KB
  of JS for a single menu.

## Components

- **`Components/Layout/AboutMenu.razor`** (new) — the dropdown: toggle button
  (`dropdown-toggle`, Bootstrap draws the caret), GitHub item with an inline
  GitHub SVG icon, divider, version text. Holds `_open` state + `Toggle`/`Close`.
- **`Components/Layout/AboutMenu.razor.css`** (new) — right alignment, the
  outside-click backdrop, icon sizing. Backdrop `z-index` below the menu so menu
  clicks still register.
- **`Components/Layout/MainLayout.razor`** (edit) — header becomes
  `d-flex justify-content-between align-items-center`; logo stays left,
  `<AboutMenu />` added on the right.
- **`StoryPoints.csproj`** (edit) — add `<Version>1.0.0</Version>` as the single
  source of truth.

## Version source

`AboutMenu` reads `AssemblyInformationalVersionAttribute.InformationalVersion`
via reflection, trims any `+build` metadata, and falls back to `1.0.0`.
Bumping the version = editing `<Version>` in the `.csproj`.

## Icons

Bootstrap Icons is not loaded; the GitHub icon is an inline SVG
(`fill="currentColor"`). The dropdown caret comes from Bootstrap's
`.dropdown-toggle::after`.

## Verification

`dotnet build` succeeds; manual check that the menu opens/closes, the GitHub
link opens in a new tab, and the version renders.
