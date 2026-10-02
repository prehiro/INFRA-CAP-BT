import { defineNuxtConfig } from 'nuxt/config'

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
