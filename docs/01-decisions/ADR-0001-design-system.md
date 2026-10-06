# ADR-0001 — Adopt the "GRID" design system (Theme Pack 164)

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-09-29 |
| Decision owner | User (selected in Design Studio) |
| Supersedes | The placeholder "visual language TBD" section of `docs/00-analysis/03-uiux-design-proposal.md` |
| Affects | Frontend Phase 3 onward; all 16 canonical screens |

## Context

The SSS specifies the frontend stack (§28.1: Angular 22 + Angular Material + Tailwind CSS) and the screens (§15), but deliberately specifies no visual language. The user was offered a design-system choice and selected **GRID** from the Design Studio theme pack.

## Decision

Adopt **GRID** as the single visual language for the application, applied exclusively through its `--tp-*` custom properties.

- Root element: `<html data-theme="grid" style="--tp-h: 254">`
- Brand hue is declared once, on the same element as `data-theme`. Changing `--tp-h` re-skins the app; no accent colour is ever hand-picked in component code.
- Every component uses `var(--tp-*)` only — no raw hex, font name, px radius, duration or shadow appears in component code (Design Studio hard rule 1).

## Why GRID fits this system

| System characteristic | GRID property | Fit |
|---|---|---|
| Internal enterprise back-office, not consumer SaaS | Neutral, legible, "safe default" identity; no spectacle, no decorative FX (`--tp-fx: none`, `--tp-3d: none`) | The product must not look like a marketing site |
| Table- and status-dense screens (task/request lists, audit, approvals) | 4px radii, 1px `--tp-line` hairlines, very low elevation (`0 1px 3px`) | Density reads as precision, not clutter |
| Large volumes of identifiers, dates, counts, SLA durations | `--tp-font-mono: JetBrains Mono` | Tabular numerals for exact column alignment |
| Users stare at these screens for hours | Restrained motion budget (`--tp-dur: 180ms`, one idle animation max) | No distraction; `prefers-reduced-motion` already handled by the pack |
| 11–13 lifecycle states must be distinguishable | Neutral base + a single accent leaves the semantic colour set unambiguous | Status colour never competes with brand colour |
| Multi-tenant product needs per-tenant identity later | Single `--tp-h` rebrand variable | A tenant-branded theme is a one-line change, satisfying §2.6 "one platform, many companies" visually |

## Token usage map (semantic → token)

| UI purpose | Token |
|---|---|
| App canvas / page background | `--tp-canvas`, `--tp-bg` |
| Cards, panels, table surfaces | `--tp-surf` |
| Sunken fills, table header, inactive segmented control | `--tp-surf-2` |
| Primary text | `--tp-ink` |
| Secondary/meta text | `--tp-mute` (never on accent, never on imagery) |
| Accent (primary action, active nav, focus ring) | `--tp-acc`; text on it `--tp-acc-ink` |
| Secondary accent / high-contrast ink plate | `--tp-acc-2` |
| Borders, dividers, table rules | `--tp-line` with `--tp-bw` |
| Radius | `--tp-r-btn` (buttons), `--tp-r-sm` (chips), `--tp-r` (cards), `--tp-r-lg` (sheets, dialogs) |
| Elevation | `--tp-sh-sm` (rows, chips), `--tp-sh` (cards, popovers, dialogs) |
| Motion | `--tp-dur` (hover/press), `--tp-dur-slow` (dialogs, sheets), `--tp-ease`, `--tp-press` for `:active` |
| Numerals, IDs, dates, durations, token counts | `--tp-font-mono` |
| Gradients | `--tp-grad` used **only** as the image overlay specified by the pack (`--tp-img-overlay-op: 0.1`), never as decoration |

**Explicit note on the accent gradient.** GRID's `--tp-grad` is derived from the accent hue. Per the frontend hard rule that Design Studio tokens are the exception, the token is used exactly where the pack directs — image overlays — and nowhere else. It is never a page or card background.

## Typography

`--tp-font-display` and `--tp-font-body` are both Helvetica Neue / Helvetica / Arial (system-native, zero webfont cost for body text). `--tp-font-mono` loads JetBrains Mono 400/700 from Google Fonts. Display weight 700 with `--tp-track-display: -0.02em`; `--tp-hero: 60px` is reserved for genuinely hero-scale numbers (e.g. an SLA compliance figure), not for page titles.

## Motion budget

- One idle animation per screen, maximum. In practice: the SLA at-risk pulse on the SLA Monitor only.
- Enter animations only on first mount; lists stagger by `--tp-stagger × index`, capped at six.
- **No scroll reveals inside the application.** Design Studio rule 8 confines `reveal.js` to landing pages; this product's only public surface is a company-registration form, and app chrome above the fold must be fully present on open. `reveal.js` is stored with the theme assets for a future public marketing page and is not wired into the app shell.

## Imagery strategy

The SSS defines no photographic content and none is required: every core artifact is structured data (tasks, requests, approvals, SLA, audit, attachments). Therefore:

- **No decorative imagery in the application.** Dashboards, lists, detail views and settings stay typographic, exactly as GRID's `artDirection` prescribes for a no-thesis app.
- Uploaded **evidence attachments** are the only user-authored images; they receive the pack's image treatment (`--tp-img-r/-frame/-sh/-filter`) and are rendered as thumbnails in the timeline, never full-bleed behind text.
- Empty states use `--tp-surf-2` plates and iconography — never a grey box, never stock photography.
- The public registration page may use a `--tp-grad` panel; no photos are planned.

## Documented deviations from the pack

Two deviations, both forced by the SSS, both deliberate.

**D-1 — No Apple/Google sign-in on the auth screens (pack §4b requires them).**
FR-AUTH-001/002 lock authentication to *employee code or company-provided email + password* within a tenant. There is no external identity provider anywhere in the SSS, enterprise SSO is explicitly out of scope (§2.7), and adding consumer social login would be inventing functionality outside the specification. The auth screens therefore keep the pack's *themed* elements — headline, inputs, `--tp-r-btn` controls, primary CTA in `--tp-acc`, FX-free background, legal line — and omit the social block. Tenant selection (decision A-01) appears where the pack would place the social block, at the top of the form, because it is the first piece of information an ambiguous login needs.

**D-2 — Desktop navigation is a left sidebar, not the pack's `top` segmented control.**
The pack's four nav archetypes are mobile patterns (bottom tabs, floating pill, FAB, segmented control). This workspace has nine destinations for a Company Admin and is table-centric; a segmented control cannot carry that, and the pack itself treats desktop as a first-class target with its own templates. One navigation *model* is used throughout and expressed at two breakpoints — sidebar ≥1024px, bottom tab bar (the pack's `tabs` archetype) <1024px. This is one model, not two nav patterns, so pack rule 2b is honoured in substance.

**Adopted from the pack without change:** dashboard archetype **bento** — the Manager Home decision queue is the hero tile and the metric row is the tile grid; empty and skeleton states on every list; bottom sheets for secondary content; 44px tap targets; full-opacity text only.

## Accessibility commitments carried forward

Body text ≥ 13px with primary body at 14–16px · `--tp-ink` on `--tp-surf` and `--tp-acc-ink` on `--tp-acc` are the only guaranteed pairs · `--tp-mute` never on accent or imagery · full-opacity type for anything readable · status always icon + text, never colour alone · visible focus rings using `--tp-acc` · `prefers-reduced-motion` handled by the pack and not re-implemented · WCAG 2.1 AA contrast target.

## Consequences

- Phase 3 implements the token layer once (Tailwind `theme.extend` + Angular Material theme) and every shared component consumes tokens only.
- Per-tenant visual identity becomes a `--tp-h` change, which is cheap and reversible.
- Vendor-neutrality: GRID is neutral by construction, so the product does not date. If a brand emerges, only `--tp-h` changes.

## Assets

Theme assets are captured verbatim in `docs/01-decisions/design-tokens/` (`grid-theme.css`, `tailwind.theme.cjs`, `reveal.js`) and copied into the Angular app during Phase 2/3 scaffolding. `frontend/` is intentionally not created yet, because `ng new` requires an empty target directory.
