import { defineNuxtConfig } from 'nuxt/config'

// Public Sans is declared HERE, in the document head, and the files are preloaded.
//
// WHY NOT IN THE CSS: in dev the app's CSS is injected by JavaScript, so during the first render
// the browser does not yet know the @font-face. The text painted with the system fallback, the
// real font arrived a moment later, and everything re-rendered - the sidebar labels and the page
// title visibly shifted on EVERY refresh (HIRO: "setiap kali refresh page seluruh teks yang ada di
// sidebar dan title header bergetar ... terjadi di semua halaman"). Measured before the fix: the
// header title's width stepped 115.19px -> 113.70px with its left/top unchanged, which is the
// exact signature of a late font swap.
//
// Declaring the faces in the head makes them known at parse time, and the preload asks for the
// files in parallel with the app's JavaScript, so the font is ready before the first paint and
// there is nothing left to swap. The files are static assets under public/fonts, which is why the
// base URL is prefixed by hand: the production app is served under /INFRA-CAP.
const BASE_URL = process.env.NUXT_APP_BASE_URL || '/'

const PUBLIC_SANS_WEIGHTS = [400, 500, 600, 700]

const PUBLIC_SANS_FACES = PUBLIC_SANS_WEIGHTS.map((w) =>
  `@font-face{font-family:'Public Sans';font-style:normal;font-display:swap;font-weight:${w};` +
  `src:url(${BASE_URL}fonts/public-sans-latin-${w}-normal.woff2) format('woff2')}`
).join('')

export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  devtools: { enabled: false },

  // Dark first: ISD staff watch this on the NOC screens at night. The toggle lives in
  // the sidebar footer (UserMenu), and the preference persists via the color-mode cookie.
  colorMode: {
    preference: 'dark',
    fallback: 'dark',
    classSuffix: ''
  },

  // SPA only: no Node.js runtime required on the IIS target.
  ssr: false,

  modules: ['@nuxt/ui'],

  // OFFLINE-SAFE: the production server (10.89.6.237) has no internet access.
  // Default @nuxt/ui icon resolution calls https://api.iconify.design at RUNTIME,
  // which would make every icon silently vanish on the target machine.
  // 'lucide' is installed from npm (@iconify-json/lucide) and bundled at build time.
  icon: {
    clientBundle: {
      scan: true,
      size: 200000
    },
    provider: 'iconify',
    // No remote fetch. Anything not in the local lucide collection is simply omitted.
    clientBundleFallback: ''
  },

  ui: {
    icons: {
      serverBundle: {
        collections: ['lucide']
      }
    }
  },

  css: ['~/assets/css/main.css'],

  // Served under the IIS application /INFRA-CAP in production.
  // NUXT_APP_BASE_URL is set by deploy/build-release.cmd during `nuxt generate`.
  // In dev it must stay '/' - a leftover /INFRA-CAP base makes the dev router
  // reject every path ("No match found for location with path /infra-cap").
  app: {
    baseURL: process.env.NUXT_APP_BASE_URL || '/',
    head: {
      title: 'INFRA-CAP',
      // See PUBLIC_SANS_FACES above: faces in the head + preload = the font is ready before the
      // first paint, so a refresh never re-renders the chrome text mid-load.
      style: [{ innerHTML: PUBLIC_SANS_FACES }],
      link: PUBLIC_SANS_WEIGHTS.map((w) => ({
        rel: 'preload',
        as: 'font',
        type: 'font/woff2',
        href: `${BASE_URL}fonts/public-sans-latin-${w}-normal.woff2`,
        crossorigin: 'anonymous'
      })),
      meta: [
        { charset: 'utf-8' },
        { name: 'viewport', content: 'width=device-width, initial-scale=1' },
        { name: 'description', content: 'Aplikasi internal INFRA-CAP' }
      ]
    }
  },

  // Dev only: the Vite server proxies /api to the ASP.NET Core API so the browser
  // never makes a cross-origin request during development. In production the API
  // sits at the same origin (/INFRA-CAP-api) and no proxy is involved.
  vite: {
    server: {
      port: 3000,
      proxy: process.env.NUXT_API_PROXY_TARGET
        ? { '/api': { target: process.env.NUXT_API_PROXY_TARGET, changeOrigin: true } }
        : undefined
    }
  },

  runtimeConfig: {
    // Overridable at runtime without a rebuild (NUXT_PUBLIC_* env vars).
    public: {
      apiBase: process.env.NUXT_PUBLIC_API_BASE || '/api'
    }
  },

  nitro: {
    // nuxt generate emits a fully static SPA; preset 'static' documents that intent.
    preset: 'static'
  }
})
