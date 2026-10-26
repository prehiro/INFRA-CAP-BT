<script setup lang="ts">
// Matches the bundled Lucide columns-3, list-filter and file-spreadsheet paths.
// Inline strokes (rather than Iconify's masked span) allow a line-only hover trace.
defineProps<{ name: 'columns' | 'filter' | 'excel' | 'loading' }>()
</script>

<template>
  <svg v-if="name === 'loading'" class="size-5 shrink-0 animate-spin motion-reduce:animate-none" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true">
    <circle cx="12" cy="12" r="9" opacity="0.3" />
    <path d="M21 12a9 9 0 0 0-9-9" />
  </svg>
  <svg v-else class="size-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
    <template v-if="name === 'columns'">
      <rect width="18" height="18" x="3" y="3" rx="2" />
      <path class="toolbar-columns-line toolbar-columns-first" d="M9 3v18" />
      <path class="toolbar-columns-line toolbar-columns-second" d="M15 3v18" />
    </template>
    <template v-else-if="name === 'filter'">
      <path class="toolbar-filter-line toolbar-filter-first" d="M2 5h20" />
      <path class="toolbar-filter-line toolbar-filter-second" d="M6 12h12" />
      <path class="toolbar-filter-line toolbar-filter-third" d="M9 19h6" />
    </template>
    <template v-else>
      <path d="M6 22a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h8a2.4 2.4 0 0 1 1.704.706l3.588 3.588A2.4 2.4 0 0 1 20 8v12a2 2 0 0 1-2 2z" />
      <path d="M14 2v5a1 1 0 0 0 1 1h5" />
      <path class="toolbar-excel-line toolbar-excel-first" d="M8 13h2" />
      <path class="toolbar-excel-line toolbar-excel-second" d="M14 13h2" />
      <path class="toolbar-excel-line toolbar-excel-third" d="M8 17h2" />
      <path class="toolbar-excel-line toolbar-excel-fourth" d="M14 17h2" />
    </template>
  </svg>
</template>

<!-- Opt-in only: a button must carry toolbar-line-hover (and --excel for green).
     Shared by exactly the four requested registers, with no block, shadow or transform. -->
<style>
.toolbar-line-hover { transition: color 180ms ease; }
.toolbar-line-hover:is(:hover, :focus-visible) { color: var(--ui-primary); background-color: transparent; }
.toolbar-line-hover--excel:is(:hover, :focus-visible) { color: #4ade80; }
:root:not(.dark) .toolbar-line-hover--excel:is(:hover, :focus-visible) { color: #15803d; }
.toolbar-columns-line { stroke-dasharray: 18; }
.toolbar-filter-first { stroke-dasharray: 20; }
.toolbar-filter-second { stroke-dasharray: 12; }
.toolbar-filter-third { stroke-dasharray: 6; }
.toolbar-excel-line { stroke-dasharray: 2; }
.toolbar-line-hover:is(:hover, :focus-visible) .toolbar-columns-line { animation: toolbar-columns-trace 440ms ease-out both; }
.toolbar-line-hover:is(:hover, :focus-visible) .toolbar-columns-second { animation-delay: 75ms; }
.toolbar-line-hover:is(:hover, :focus-visible) .toolbar-filter-first { animation: toolbar-filter-top 440ms ease-out both; }
.toolbar-line-hover:is(:hover, :focus-visible) .toolbar-filter-second { animation: toolbar-filter-middle 440ms 60ms ease-out both; }
.toolbar-line-hover:is(:hover, :focus-visible) .toolbar-filter-third { animation: toolbar-filter-bottom 440ms 120ms ease-out both; }
.toolbar-line-hover:is(:hover, :focus-visible) .toolbar-excel-line { animation: toolbar-excel-trace 350ms ease-out both; }
.toolbar-line-hover:is(:hover, :focus-visible) .toolbar-excel-second { animation-delay: 45ms; }
.toolbar-line-hover:is(:hover, :focus-visible) .toolbar-excel-third { animation-delay: 90ms; }
.toolbar-line-hover:is(:hover, :focus-visible) .toolbar-excel-fourth { animation-delay: 135ms; }
@keyframes toolbar-columns-trace { from { stroke-dashoffset: 18; } to { stroke-dashoffset: 0; } }
@keyframes toolbar-filter-top { from { stroke-dashoffset: 20; } to { stroke-dashoffset: 0; } }
@keyframes toolbar-filter-middle { from { stroke-dashoffset: 12; } to { stroke-dashoffset: 0; } }
@keyframes toolbar-filter-bottom { from { stroke-dashoffset: 6; } to { stroke-dashoffset: 0; } }
@keyframes toolbar-excel-trace { from { stroke-dashoffset: 2; } to { stroke-dashoffset: 0; } }
@media (prefers-reduced-motion: reduce) {
  .toolbar-line-hover { transition: none; }
  .toolbar-columns-line, .toolbar-filter-line, .toolbar-excel-line { animation: none !important; stroke-dashoffset: 0 !important; }
}
</style>
