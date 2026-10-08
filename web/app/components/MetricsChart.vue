<template>
  <UCard
    :ui="{ header: 'p-0 sm:p-0', body: 'p-0 sm:p-0', footer: 'p-0 sm:p-0' }"
    class="anim-fade-up overflow-hidden"
  >
    <template #header>
      <div class="flex flex-wrap items-center justify-between gap-3 px-5 py-4">
        <div class="flex items-center gap-3">
          <span class="grid size-9 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary ring-1 ring-inset ring-primary/20">
            <UIcon name="i-lucide-activity" class="size-5" />
          </span>
          <div class="min-w-0">
            <h2 class="text-sm font-semibold leading-tight">System Metrics</h2>
            <p class="text-xs text-muted">Live host load, sampled every {{ INTERVAL_S }}s</p>
          </div>
        </div>

        <div class="flex items-center gap-2">
          <span
            class="inline-flex items-center gap-1.5 rounded-full border border-default px-2.5 py-1 text-[11px] font-medium"
            :title="statusHint"
          >
            <span class="live-dot" :class="`live-dot--${status}`" />
            {{ statusLabel }}
          </span>
          <span class="hidden text-[11px] text-muted tabular-nums sm:inline">
            {{ updatedLabel }}
          </span>
        </div>
      </div>
    </template>

    <div class="grid grid-cols-1 divide-y divide-default sm:grid-cols-3 sm:divide-x sm:divide-y-0">
      <div v-for="tile in tiles" :key="tile.key" class="px-5 py-4">
        <div class="flex items-start justify-between gap-2">
          <div class="flex min-w-0 items-center gap-2">
            <UIcon :name="tile.icon" class="size-4 shrink-0" :style="{ color: tile.color }" />
            <span class="truncate text-[11px] font-semibold uppercase tracking-wider text-muted">
              {{ tile.label }}
            </span>
          </div>
          <div class="flex items-baseline gap-0.5 tabular-nums">
            <span
              class="text-2xl font-semibold leading-none"
              :style="{ color: tile.value >= HOT_THRESHOLD ? 'var(--ui-error)' : tile.color }"
              :title="tile.value >= HOT_THRESHOLD ? 'High load' : undefined"
            >{{ tile.value.toFixed(1) }}</span>
            <span class="text-xs text-muted">%</span>
          </div>
        </div>

        <div class="mt-3">
          <MetricSparkline
            :values="tile.history"
            :times="tile.times"
            :color="tile.color"
            :label="tile.label"
            :height="70"
          />
        </div>

        <div class="mt-3 flex items-center justify-between gap-2 text-[11px] text-muted tabular-nums">
          <span class="truncate" :title="tile.detail">{{ tile.detail }}</span>
          <span class="shrink-0">peak {{ tile.peak.toFixed(1) }}%</span>
        </div>
      </div>
    </div>

    <template #footer>
      <div class="flex flex-wrap items-center justify-between gap-x-4 gap-y-1 border-t border-default px-5 py-3 text-[11px] text-muted">
        <span class="truncate">{{ hostLabel }}</span>
        <span v-if="lastError" class="inline-flex items-center gap-1.5" :style="{ color: 'var(--ui-error)' }">
          <UIcon name="i-lucide-triangle-alert" class="size-3.5 shrink-0" />
          {{ lastError }}
        </span>
        <span v-else class="tabular-nums">{{ WINDOW }} samples ({{ windowLabel }})</span>
      </div>
    </template>
  </UCard>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import type { MetricsSnapshot } from '~/types'

/**
 * System Metrics panel: CPU / memory / disk of the host running the API, one column each.
 *
 * WHY ONE COLUMN PER METRIC: three small charts are read at a glance ("which of the three is
 * stressed?"), while three overlapping lines in one plot force you to consult a legend on every
 * look. All three sample on the same tick, so the columns stay comparable.
 *
 * WHY THE POLLING IS A CHAINED TIMEOUT AND NOT setInterval: an interval fires again whether or
 * not the previous request came back, so a slow API builds a queue of overlapping requests. The
 * chain below waits for each response, skips work while the tab is hidden, and backs off when
 * the API is down instead of hammering a dead endpoint every five seconds.
 */
const INTERVAL_S = 5
const WINDOW = 60
const INTERVAL_MS = INTERVAL_S * 1000
const BACKOFF_MS = 20_000
const FAILURES_BEFORE_BACKOFF = 3
const HOT_THRESHOLD = 90

/**
 * Fixed per-metric hues rather than theme tokens: the three lines must stay distinguishable
 * whichever accent colour the user picked in the colour picker, and a canvas needs a literal
 * colour anyway. All three read on both the dark and the light background.
 */
const HUES = { cpu: '#00C16A', ram: '#38BDF8', disk: '#F59E0B' } as const

const snapshot = ref<MetricsSnapshot | null>(null)
const status = ref<'connecting' | 'live' | 'offline'>('connecting')
const updatedAt = ref<Date | null>(null)
const lastError = ref('')
const cpuHistory = reactive<number[]>([])
const ramHistory = reactive<number[]>([])
const diskHistory = reactive<number[]>([])
const cpuTimes = reactive<number[]>([])
const ramTimes = reactive<number[]>([])
const diskTimes = reactive<number[]>([])

let timer: ReturnType<typeof setTimeout> | null = null
let failures = 0
let inFlight = false

const statusLabel = computed(() => ({
  connecting: 'Connecting',
  live: 'Live',
  offline: 'API offline'
}[status.value]))

const statusHint = computed(() => ({
  connecting: 'Fetching the first sample',
  live: `Refreshed automatically every ${INTERVAL_S} seconds`,
  offline: lastError.value || 'Cannot reach the API'
}[status.value]))

const updatedLabel = computed(() => {
  if (!updatedAt.value) return 'no data yet'
  return `Updated ${updatedAt.value.toLocaleTimeString('en-GB', { hour12: false })}`
})

const hostLabel = computed(() => {
  const s = snapshot.value
  if (!s) return 'Waiting for host data...'
  return [s.host, osLabel(s.os), `up ${formatUptime(s.uptimeSeconds)}`].join(' | ')
})

/**
 * .NET reports the raw version, and Windows 11 still identifies as 10.0.<build> — so the host
 * line would read "Windows 10.0.26200" on a Windows 11 machine, which looks like the panel is
 * reporting the wrong OS. Build 22000 is where Windows 11 starts.
 */
function osLabel(os: string): string {
  const build = Number(/10\.0\.(\d+)/.exec(os)?.[1] ?? 0)
  if (build >= 22000) return `Windows 11 (build ${build})`
  const clean = os.replace(/^Microsoft\s+/i, '')
  return clean || 'Windows'
}

const windowLabel = computed(() => {
  const seconds = WINDOW * INTERVAL_S
  return seconds >= 60 ? `${Math.round(seconds / 60)} min` : `${seconds}s`
})

const tiles = computed(() => {
  const s = snapshot.value
  const gb = (b: number) => (b / 1073741824).toFixed(1)
  return [
    {
      key: 'cpu' as const,
      label: 'CPU',
      icon: 'i-lucide-cpu',
      color: HUES.cpu,
      value: s?.cpu ?? 0,
      history: cpuHistory,
      times: cpuTimes,
      peak: peak(cpuHistory),
      detail: s ? `${s.cores} logical cores` : 'waiting for data'
    },
    {
      key: 'ram' as const,
      label: 'Memory',
      icon: 'i-lucide-memory-stick',
      color: HUES.ram,
      value: s?.ram ?? 0,
      history: ramHistory,
      times: ramTimes,
      peak: peak(ramHistory),
      detail: s ? `${gb(s.ramUsedBytes)} / ${gb(s.ramTotalBytes)} GB` : 'waiting for data'
    },
    {
      key: 'disk' as const,
      label: 'Disk',
      icon: 'i-lucide-hard-drive',
      color: HUES.disk,
      value: s?.disk ?? 0,
      history: diskHistory,
      times: diskTimes,
      peak: peak(diskHistory),
      detail: s ? `${gb(s.diskUsedBytes)} / ${gb(s.diskTotalBytes)} GB (${s.diskDrive})` : 'waiting for data'
    }
  ]
})

function peak(values: number[]): number {
  return values.length ? Math.max(...values) : 0
}

function formatUptime(seconds: number): string {
  const d = Math.floor(seconds / 86400)
  const h = Math.floor((seconds % 86400) / 3600)
  const m = Math.floor((seconds % 3600) / 60)
  if (d > 0) return `${d}d ${h}h`
  if (h > 0) return `${h}h ${m}m`
  return `${m}m`
}

/** Appends a sample and trims the window, so the panel's memory is bounded at 60 points. */
function push(series: number[], times: number[], value: number, at: number) {
  series.push(value)
  times.push(at)
  if (series.length > WINDOW) series.shift()
  if (times.length > WINDOW) times.shift()
}

async function poll() {
  if (inFlight) return
  inFlight = true
  try {
    const data = await apiGetMetrics()
    snapshot.value = data
    const now = Date.now()
    push(cpuHistory, cpuTimes, data.cpu, now)
    push(ramHistory, ramTimes, data.ram, now)
    push(diskHistory, diskTimes, data.disk, now)
    status.value = 'live'
    failures = 0
    updatedAt.value = new Date()
    lastError.value = ''
  } catch {
    failures++
    status.value = 'offline'
    lastError.value = 'Metrics unavailable (API not responding).'
  } finally {
    inFlight = false
    schedule()
  }
}

function schedule() {
  if (timer) clearTimeout(timer)
  const delay = failures >= FAILURES_BEFORE_BACKOFF ? BACKOFF_MS : INTERVAL_MS
  timer = setTimeout(() => {
    // A hidden tab draws nothing, so polling it would be pure load on the API host.
    if (typeof document !== 'undefined' && document.hidden) return
    poll()
  }, delay)
}

function onVisibility() {
  if (document.hidden || inFlight) return
  // Coming back to the tab should not wait out the pending delay: the chart is visibly stale.
  if (timer) clearTimeout(timer)
  poll()
}

onMounted(() => {
  poll()
  document.addEventListener('visibilitychange', onVisibility)
})

onBeforeUnmount(() => {
  if (timer) clearTimeout(timer)
  document.removeEventListener('visibilitychange', onVisibility)
})
</script>

<style scoped>
/* A live monitor needs a "still alive" cue that does not shift the layout: an opacity animation
   on a 6px dot is cheap, and it is switched off entirely for reduced motion. */
.live-dot {
  width: 6px;
  height: 6px;
  border-radius: 9999px;
  background: var(--ui-primary);
  animation: metrics-pulse 2s cubic-bezier(0.4, 0, 0.6, 1) infinite;
}

.live-dot--connecting {
  background: var(--ui-text-dimmed);
}

.live-dot--offline {
  background: var(--ui-error);
  animation: none;
}

@keyframes metrics-pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.25; }
}

@media (prefers-reduced-motion: reduce) {
  .live-dot {
    animation: none;
  }
}
</style>
