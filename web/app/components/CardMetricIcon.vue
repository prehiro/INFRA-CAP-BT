<script setup lang="ts">
/**
 * The top-right badge of a dashboard card.
 *
 * It used to be the text "467 unit", repeated three times across one row of cards. The number is not
 * gone - it lives in the tooltip and the aria-label - but the corner now carries a small drawing of what
 * the card is about, so three cards in a row read as fleet / domain / model instead of three identical
 * grey numbers.
 *
 * One component with three drawings (`kind`), because the motion is the point: each has its own loop -
 * the notebook sliding in, the check drawing itself, the tiles breathing - and every one of them stops
 * under prefers-reduced-motion.
 *
 * Inline SVG rather than <UIcon name>: Nuxt Icon here runs with provider 'none' and no API fallback, so
 * an icon that is not in the build-time bundle renders as an EMPTY span with no error at all. A drawing
 * that lives in this file cannot go missing.
 */
defineProps<{
  kind: 'fleet' | 'domain' | 'treemap'
  /** Shown in the tooltip and to screen readers; the number itself is no longer painted. */
  total: number
}>()
</script>

<template>
  <UTooltip :text="`${total} unit`" :delay-duration="200">
    <!-- role=img + aria-label: the badge is the only carrier of the count now, so it has to announce it. -->
    <span class="metric-ico" role="img" :aria-label="`${total} unit`">
      <!-- fleet: a desktop and a notebook - the two form factors this card counts. -->
      <svg
        v-if="kind === 'fleet'"
        class="ico-glyph"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="1.6"
        stroke-linecap="round"
        stroke-linejoin="round"
        aria-hidden="true"
      >
        <rect x="2.6" y="4.2" width="10.6" height="7.4" rx="1.5" />
        <path d="M7.9 15.2v2M5.9 17.2h4" />
        <g class="ico-notebook">
          <rect x="14.4" y="8.2" width="6.8" height="5.2" rx="1.2" />
          <path d="M13.2 15.6h9.2" />
        </g>
      </svg>

      <!-- domain: a machine with a check - assigned to a user. -->
      <svg
        v-else-if="kind === 'domain'"
        class="ico-glyph"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="1.6"
        stroke-linecap="round"
        stroke-linejoin="round"
        aria-hidden="true"
      >
        <rect x="2.8" y="4.2" width="11.4" height="8.2" rx="1.5" />
        <path d="M7.4 15.8v2M5.2 17.8h4.4" />
        <circle cx="17.2" cy="15.4" r="4.2" />
        <path class="ico-tick" d="M15.3 15.5l1.4 1.5 2.5-2.9" />
      </svg>

      <!-- treemap: four tiles of different sizes, echoing the chart itself. -->
      <svg
        v-else
        class="ico-glyph"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="1.5"
        stroke-linejoin="round"
        aria-hidden="true"
      >
        <rect class="ico-tile" x="3" y="4.2" width="8.4" height="7.2" rx="1.3" />
        <rect class="ico-tile" x="12.6" y="4.2" width="8.4" height="4.4" rx="1.2" />
        <rect class="ico-tile" x="12.6" y="9.8" width="8.4" height="8.4" rx="1.3" />
        <rect class="ico-tile" x="3" y="12.6" width="8.4" height="5.6" rx="1.2" />
      </svg>
    </span>
  </UTooltip>
</template>

<style scoped>
/* Bare glyph, no plate: the size is almost all hover padding, so the drawing sits on the card like the
   title's own text instead of inside a coloured square. */
.metric-ico {
  display: grid;
  place-items: center;
  width: 1.9rem;
  height: 1.9rem;
  border-radius: 0.5rem;
  color: var(--ui-primary);
  transition: transform 0.25s ease;
}

.metric-ico:hover {
  transform: translateY(-1px) scale(1.08);
}

.ico-glyph {
  width: 1.3rem;
  height: 1.3rem;
}

.ico-notebook {
  animation: ico-nudge 3.8s ease-in-out infinite;
}

.ico-tick {
  stroke-dasharray: 7;
  stroke-dashoffset: 7;
  animation: ico-draw 3.8s ease-in-out infinite;
}

.ico-tile {
  transform-box: fill-box;
  transform-origin: center;
  animation: ico-breathe 3.4s ease-in-out infinite;
}

.ico-tile:nth-child(2) { animation-delay: 0.18s; }
.ico-tile:nth-child(3) { animation-delay: 0.36s; }
.ico-tile:nth-child(4) { animation-delay: 0.54s; }

@keyframes ico-nudge {
  0%, 100% { transform: translateX(0); opacity: 0.65; }
  45% { transform: translateX(1.1px); opacity: 1; }
}

@keyframes ico-draw {
  0%, 12% { stroke-dashoffset: 7; }
  45%, 82% { stroke-dashoffset: 0; }
  100% { stroke-dashoffset: 7; }
}

@keyframes ico-breathe {
  0%, 100% { transform: scale(1); }
  50% { transform: scale(0.84); }
}

/* The badge is decoration; users who ask for less motion get a still icon, not a frozen half-state. */
@media (prefers-reduced-motion: reduce) {
  .ico-notebook,
  .ico-tick,
  .ico-tile {
    animation: none;
  }

  .ico-tick {
    stroke-dashoffset: 0;
  }
}
</style>