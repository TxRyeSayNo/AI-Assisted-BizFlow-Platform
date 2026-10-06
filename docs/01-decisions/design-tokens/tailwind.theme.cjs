/**
 * GRID theme tokens for Tailwind CSS — AI-Assisted BizFlow Platform.
 * Selected design system: GRID (Theme Pack 164). See docs/01-decisions/ADR-0001-design-system.md
 *
 * Usage (Tailwind v3+):
 *   const gridTheme = require('./src/styles/tailwind.theme.cjs');
 *   module.exports = { theme: { extend: { ...gridTheme } }, ... };
 *
 * Every utility resolves to a var(--tp-*) custom property, so changing --tp-h on <html>
 * re-skins the whole application without touching component code.
 *
 * Corrections applied to the Design Studio snippet, because the original would not compile:
 *   - `borderRadius_img`, `mixBlendMode.img`, `opacity.img`, `rotate.img` and `themeIds` are not valid
 *     Tailwind theme keys. Image treatment is delivered as component classes in `image-tokens.css`
 *     instead of by overriding Tailwind's opacity/blend/rotate scales (which would break `opacity-50`,
 *     `mix-blend-multiply` and `rotate-45` app-wide).
 *   - `fontSize.hero` is defined as a tuple so it carries a line-height, matching how the token is used
 *     for the single hero-scale figure on the dashboard.
 */

module.exports = {
  colors: {
    bg: 'var(--tp-bg)',
    canvas: 'var(--tp-canvas)',
    surf: 'var(--tp-surf)',
    'surf-2': 'var(--tp-surf-2)',
    ink: 'var(--tp-ink)',
    mute: 'var(--tp-mute)',
    acc: 'var(--tp-acc)',
    'acc-2': 'var(--tp-acc-2)',
    'acc-ink': 'var(--tp-acc-ink)',
    line: 'var(--tp-line)'
  },

  backgroundImage: {
    grad: 'var(--tp-grad)',
    tex: 'var(--tp-tex)'
  },

  borderRadius: {
    DEFAULT: 'var(--tp-r)',
    sm: 'var(--tp-r-sm)',
    lg: 'var(--tp-r-lg)',
    btn: 'var(--tp-r-btn)'
  },

  borderWidth: {
    theme: 'var(--tp-bw)'
  },

  boxShadow: {
    theme: 'var(--tp-sh)',
    'theme-sm': 'var(--tp-sh-sm)',
    img: 'var(--tp-img-sh)'
  },

  fontFamily: {
    display: 'var(--tp-font-display)',
    body: 'var(--tp-font-body)',
    mono: 'var(--tp-font-mono)'
  },

  fontSize: {
    // Hero figure only (e.g. SLA compliance on the SLA Monitor / Manager dashboard).
    hero: ['var(--tp-hero)', { lineHeight: '1', letterSpacing: 'var(--tp-track-display)' }]
  },

  fontWeight: {
    display: 'var(--tp-w-display)'
  },

  letterSpacing: {
    display: 'var(--tp-track-display)'
  },

  backdropBlur: {
    theme: 'var(--tp-blur)'
  },

  aspectRatio: {
    img: 'var(--tp-img-ar)'
  },

  transitionDuration: {
    theme: 'var(--tp-dur)',
    'theme-slow': 'var(--tp-dur-slow)'
  },

  transitionTimingFunction: {
    theme: 'var(--tp-ease)'
  },

  scale: {
    press: 'var(--tp-press)'
  },

  keyframes: {
    pop: { '0%': { transform: 'scale(.88)', opacity: '0' }, '62%': { transform: 'scale(1.05)' }, '100%': { transform: 'scale(1)', opacity: '1' } },
    up: { from: { transform: 'translateY(var(--tp-reveal-dist,16px))', opacity: '0' }, to: { transform: 'translateY(0)', opacity: '1' } },
    sheet: { from: { transform: 'translateY(102%)' }, to: { transform: 'translateY(0)' } },
    shake: { '10%,90%': { transform: 'translateX(-2px)' }, '30%,70%': { transform: 'translateX(3px)' }, '50%': { transform: 'translateX(-4px)' } },
    breathe: { '0%,100%': { transform: 'scale(1)' }, '50%': { transform: 'scale(1.045)' } },
    float: { '0%,100%': { transform: 'translateY(0)' }, '50%': { transform: 'translateY(-9px)' } },
    shimmer: { from: { backgroundPosition: '-160% 0' }, to: { backgroundPosition: '260% 0' } },
    spin: { to: { transform: 'rotate(360deg)' } },
    // The single permitted idle animation: SLA at-risk indicator (design rule 8 — one per screen).
    pulse: { '0%,100%': { opacity: '1' }, '50%': { opacity: '.42' } }
  },

  animation: {
    pop: 'tp-pop var(--tp-dur) var(--tp-ease) both',
    up: 'tp-up var(--tp-dur-slow) var(--tp-ease) both',
    sheet: 'tp-sheet var(--tp-dur-slow) var(--tp-ease) both',
    shake: 'tp-shake 500ms var(--tp-ease) 1',
    breathe: 'tp-breathe 3.4s var(--tp-ease) infinite',
    float: 'tp-float 4s ease-in-out infinite',
    shimmer: 'tp-shimmer 1.4s linear infinite',
    spin: 'tp-spin 800ms linear infinite',
    pulse: 'tp-pulse 2.4s var(--tp-ease) infinite'
  }
};
