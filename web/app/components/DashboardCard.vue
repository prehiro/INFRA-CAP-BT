<script setup lang="ts">
/**
 * One module tile on the dashboard grid.
 *
 * WHY A COMPONENT
 * The grid used to be seven hand-written UCard blocks, which is how two of them ended up as dead
 * placeholders ("0", opacity-50, pointer-events-none) while the pages they pointed at already existed.
 * One card shape also means the hover choreography is written once instead of seven times.
 *
 * MOTION
 * Everything moves on hover of the whole card, never on hover of the number alone: the tile lifts 1px,
 * a tinted glow blooms in the corner the icon sits in, the icon chip cocks back a few degrees, and a
 * single sheen crosses the surface. The chevron is the only element that travels any distance, because
 * it is the one thing that means "go". All of it is behind (hover: hover) and (pointer: fine) so a tap
 * on a touch screen cannot leave a card stuck in its hover state, and all of it is dropped under
 * prefers-reduced-motion.
 */
const props = withDefaults(defineProps<{
  /** Route the whole card links to. */
  to: string
  /** What the module is, e.g. "PC Ledger". */
  label: string
  /** The headline number for the module. */
  value: number | string
  /** Small line under the number, e.g. "3 floors". */
  sub?: string
  /** Iconify name. Must also be listed in nuxt.config's icon.clientBundle.icons: the scanner does
   *  not look inside a script array, so an unlisted name renders as an empty span. */
  icon: string
  /** Colour family. Only the icon chip and the hover glow are tinted; the card chrome stays neutral. */
  tone?: keyof typeof TONES
  /** Optional per-floor breakdown, shown on the wide card. */
  chips?: { label: string, value: number }[]
  /** Spans the full row on lg. Use for the last tile so a 7-card grid still fills whole rows. */
  wide?: boolean
}>(), {
  sub: '',
  tone: 'primary',
  chips: () => [],
  wide: false
})

const TONES = {
  emerald: {
    chip: 'bg-emerald-500/10 text-emerald-600 ring-emerald-500/20 dark:text-emerald-400',
    glow: 'bg-emerald-500/25',
    dot: 'bg-emerald-500'
  },
  sky: {
    chip: 'bg-sky-500/10 text-sky-600 ring-sky-500/20 dark:text-sky-400',
    glow: 'bg-sky-500/25',
    dot: 'bg-sky-500'
  },
  violet: {
    chip: 'bg-violet-500/10 text-violet-600 ring-violet-500/20 dark:text-violet-400',
    glow: 'bg-violet-500/25',
    dot: 'bg-violet-500'
  },
  rose: {
    chip: 'bg-rose-500/10 text-rose-600 ring-rose-500/20 dark:text-rose-400',
    glow: 'bg-rose-500/25',
    dot: 'bg-rose-500'
  },
  amber: {
    chip: 'bg-amber-500/10 text-amber-600 ring-amber-500/20 dark:text-amber-400',
    glow: 'bg-amber-500/25',
    dot: 'bg-amber-500'
  },
  cyan: {
    chip: 'bg-cyan-500/10 text-cyan-600 ring-cyan-500/20 dark:text-cyan-400',
    glow: 'bg-cyan-500/25',
    dot: 'bg-cyan-500'
  },
  primary: {
    chip: 'bg-primary/10 text-primary ring-primary/20',
    glow: 'bg-primary/25',
    dot: 'bg-primary'
  }
} as const

const tone = computed(() => TONES[props.tone] ?? TONES.primary)
</script>

<template>
  <UCard
    :to="to"
    class="dc group relative block overflow-hidden transition-[transform,box-shadow,border-color] duration-300 ease-out hover:-translate-y-1 hover:border-primary/40 hover:shadow-xl"
    :class="wide ? 'lg:col-span-3' : ''"
  >
    <!-- Corner bloom in the icon's colour. Absolutely positioned and clipped by the card, so it can
         never push the content around. -->
    <span
      aria-hidden="true"
      class="dc-glow pointer-events-none absolute -left-10 -top-12 size-32 rounded-full blur-2xl"
      :class="tone.glow"
    />
    <!-- One sheen crossing the surface, on hover only. -->
    <span aria-hidden="true" class="dc-sheen pointer-events-none absolute inset-0" />

    <div class="relative flex items-center justify-between gap-4 p-6">
      <div class="flex min-w-0 items-center gap-4">
        <span
          class="dc-chip grid size-11 shrink-0 place-items-center rounded-xl ring-1 ring-inset"
          :class="tone.chip"
        >
          <UIcon :name="icon" class="size-5" aria-hidden="true" />
        </span>

        <div class="min-w-0">
          <p class="text-3xl font-bold tracking-tight tabular-nums">{{ value }}</p>
          <p class="truncate text-xs text-muted">{{ label }}</p>
          <p v-if="sub" class="truncate text-[11px] text-muted/80">{{ sub }}</p>

          <!-- Per-floor breakdown. Only the wide card carries these, which is what makes it earn its
               full-width slot instead of just stretching one number across the row. -->
          <div v-if="chips.length" class="mt-3 flex flex-wrap items-center gap-1.5">
            <span
              v-for="chip in chips"
              :key="chip.label"
              class="inline-flex items-center gap-1.5 rounded-md bg-default/40 px-2 py-0.5 text-[11px] text-muted"
            >
              <span aria-hidden="true" class="size-1.5 rounded-full" :class="tone.dot" />
              <span class="max-w-[10rem] truncate">{{ chip.label }}</span>
              <span class="font-semibold text-default tabular-nums">{{ chip.value }}</span>
            </span>
          </div>
        </div>
      </div>

      <UIcon
        name="i-lucide-chevron-right"
        class="dc-chev size-5 shrink-0 text-muted opacity-60"
        aria-hidden="true"
      />
    </div>
  </UCard>
</template>

<style scoped>
.dc-glow {
  opacity: 0;
  transition: opacity 420ms ease;
}

.dc-sheen {
  background: linear-gradient(110deg, transparent 30%, rgb(255 255 255 / 0.14) 50%, transparent 70%);
  transform: translateX(-140%);
}

.dc-chip {
  transition: transform 380ms cubic-bezier(0.34, 1.56, 0.64, 1);
}

.dc-chev {
  transition: transform 320ms cubic-bezier(0.22, 1, 0.36, 1), opacity 320ms ease, color 320ms ease;
}

@media (hover: hover) and (pointer: fine) {
  .dc:hover .dc-glow {
    opacity: 1;
  }

  /* The chip cocks back toward the corner it sits in; the glyph inside stays put. */
  .dc:hover .dc-chip {
    transform: rotate(-6deg) scale(1.05);
  }

  .dc:hover .dc-chev {
    transform: translateX(4px);
    opacity: 1;
    color: var(--ui-primary);
  }

  /* One pass only, 620ms, no loop. */
  .dc:hover .dc-sheen {
    animation: dc-sheen 620ms cubic-bezier(0.22, 1, 0.36, 1) both;
  }
}

@keyframes dc-sheen {
  from { transform: translateX(-140%); }
  to   { transform: translateX(140%); }
}

@media (prefers-reduced-motion: reduce) {
  /* The lift/border/shadow transition comes from Tailwind on the card root, so it needs its own reset -
     zeroing only the inner parts would leave the tile itself still animating. */
  .dc {
    transition: none !important;
  }
  .dc-glow,
  .dc-chip,
  .dc-chev {
    transition: none !important;
  }
  .dc:hover .dc-chip,
  .dc:hover .dc-chev {
    transform: none !important;
  }
  .dc:hover .dc-sheen {
    animation: none !important;
    display: none;
  }
}
</style>