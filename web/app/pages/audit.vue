<script setup lang="ts">
import type { AuditDto } from '~/types'

/**
 * Log Audit - every action a user performed, read-only.
 *
 * WHY A DEDICATED PAGE rather than a column on the logbooks: the trail spans BOTH logbooks
 * plus authentication and user management, so it has no single parent register to live under.
 * It sits below User Management because both are admin-only surfaces, not because the events
 * are related - the nav group is "administration", not "logbook".
 *
 * The page owns no writes. There is no delete-audit-row affordance anywhere here, and the API
 * has no matching endpoint: an audit trail an admin can silently edit is not a trail.
 */

const toast = useToast()
const { isAdmin } = useAuth()

const rows = ref<AuditDto[]>([])
const stats = ref<{ total: number, logins: number, failed: number, deletes: number, actors: { username: string, count: number }[] } | null>(null)
const loading = ref(false)
const page = ref(1)
const pageSize = ref(25)
const total = ref(0)

const search = ref('')
const actionFilter = ref<string | undefined>(undefined)
const actorFilter = ref<string | undefined>(undefined)
const rangeFilter = ref('all')

/**
 * Action catalogue. Rendered as chips rather than a select because the whole point of the
 * filter row is to show at a glance which categories exist; the counts come from the server's
 * aggregate so a chip showing 0 is visibly a dead end rather than a silent empty result.
 *
 * `tone` drives the icon colour only. Colour alone must not carry the meaning - each chip
 * keeps its text label, and each row keeps its action word in the table.
 */
const ACTIONS = [
  { value: 'login', label: 'Sign in', icon: 'i-lucide-log-in', tone: 'text-primary' },
  { value: 'login_failed', label: 'Failed sign-in', icon: 'i-lucide-shield-alert', tone: 'text-error' },
  { value: 'record_create', label: 'Record created', icon: 'i-lucide-file-plus-2', tone: 'text-success' },
  { value: 'record_update', label: 'Record updated', icon: 'i-lucide-file-pen-line', tone: 'text-info' },
  { value: 'record_delete', label: 'Record deleted', icon: 'i-lucide-file-minus-2', tone: 'text-error' },
  { value: 'user_create', label: 'User created', icon: 'i-lucide-user-plus', tone: 'text-success' },
  { value: 'user_update', label: 'User updated', icon: 'i-lucide-user-cog', tone: 'text-info' },
  { value: 'user_delete', label: 'User deleted', icon: 'i-lucide-user-minus', tone: 'text-error' }
] as const

// The summary card for deletions reuses the ACTION colour of a delete row, not a separate
// amber of its own. Two hues for one event class reads as a mistake: the card said "warning"
// while the row it counts said "error". One colour per meaning, everywhere.
const DELETE_TONE = 'text-error'

function actionMeta(value: string) {
  return ACTIONS.find(a => a.value === value) ?? { value, label: value.replace(/_/g, ' '), icon: 'i-lucide-dot', tone: 'text-muted' }
}

/**
 * Relative time for the recency column, absolute date on hover.
 *
 * Audit reading is overwhelmingly "what happened just now / just before lunch", so the
 * primary label is relative and the exact timestamp stays available rather than being the
 * only thing shown. `title` carries the full local date-time.
 */
function relTime(iso: string): string {
  const then = new Date(iso).getTime()
  if (Number.isNaN(then)) return String(iso)
  const secs = Math.round((Date.now() - then) / 1000)
  if (secs < 10) return 'just now'
  if (secs < 60) return `${secs}s ago`
  const mins = Math.round(secs / 60)
  if (mins < 60) return `${mins}m ago`
  const hours = Math.round(mins / 60)
  if (hours < 24) return `${hours}h ago`
  const days = Math.round(hours / 24)
  return days < 7 ? `${days}d ago` : new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
}

function fullTime(iso: string): string {
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return String(iso)
  return d.toLocaleString('en-GB', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

function clockTime(iso: string): string {
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return ''
  return d.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

/**
 * Calendar date for the DATE column, separate from the clock in the TIME column.
 *
 * HIRO asked for a date column: the previous single Time column showed only a relative
 * label plus HH:MM:SS, so an event from last month read "3d ago" with no date on it and a
 * row could only be placed in time by hovering. Splitting the two makes the date sortable
 * by eye and keeps the relative label as the fast "what just happened" read.
 *
 * Uses LOCAL formatting, because the operator's question is "which day was that", and the
 * server stores UTC. A UTC-formatted date would show the wrong day for any event logged
 * in the evening in WIB.
 */
function dayLabel(iso: string): string {
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return String(iso)
  return d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
}

function initialsOf(username: string): string {
  const parts = (username || '').trim().split(/[\s._-]+/).filter(Boolean)
  if (!parts.length) return '?'
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase()
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
}

/**
 * Date-range bounds for the server query.
 *
 * `to` is EXCLUSIVE and is built from the END of the chosen day, because the server treats
 * it as an upper bound (`CreatedAt < to`). Sending midnight-of-that-day would silently drop
 * every event logged during that day - the filter would look like it works while hiding the
 * rows the user most likely wants to see.
 *
 * Sent as local Date objects; the controller converts to UTC. Building them here rather than
 * in the API keeps "today" meaning the operator's today.
 */
const rangeBounds = computed(() => {
  const now = new Date()
  const startOfDay = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate())
  switch (rangeFilter.value) {
    case 'today':
      return { from: startOfDay(now).toISOString(), to: undefined }
    case 'week': {
      const from = startOfDay(now)
      from.setDate(from.getDate() - 6)
      return { from: from.toISOString(), to: undefined }
    }
    case 'month': {
      const from = new Date(now.getFullYear(), now.getMonth(), 1)
      return { from: from.toISOString(), to: undefined }
    }
    default:
      return { from: undefined, to: undefined }
  }
})

const pageCount = computed(() => Math.max(1, Math.ceil(total.value / pageSize.value)))

const anyFilterActive = computed(() =>
  !!(search.value.trim() || actionFilter.value || actorFilter.value || rangeFilter.value !== 'all'))

/** Server-side filtering: the trail grows without bound, so the client cannot hold it all. */
async function load() {
  if (!isAdmin.value) return
  loading.value = true
  try {
    const b = rangeBounds.value
    const res = await apiListAudit({
      page: page.value,
      pageSize: pageSize.value,
      search: search.value.trim() || undefined,
      action: actionFilter.value,
      username: actorFilter.value,
      from: b.from,
      to: b.to
    })
    rows.value = res.items
    total.value = res.total
    stats.value = res.stats
  } catch (e: any) {
    toast.add({ title: 'Could not load the audit trail', description: e?.data?.message || e?.message, color: 'error' })
  } finally {
    loading.value = false
  }
}

// Any filter change resets to page 1: staying on page 4 of a result set that now has one page
// is the classic way a filter looks like it silently returned nothing.
//
// `search` is NOT in this list on purpose - see onSearchInput below. It is owned by the
// debounced handler, and a watcher on it would fire per keystroke and defeat the debounce.
watch([actionFilter, actorFilter, rangeFilter, pageSize], () => { page.value = 1; load() })
watch(page, load)

/**
 * Search input handler.
 *
 * THE BUG THIS FIXES: the input was bound with `:value="search"` and read through this
 * handler, but the handler never WROTE `search.value` - it only fired the debounced reload.
 * So typing updated nothing: the bound value was re-applied on every re-render and the field
 * either stayed empty or reverted, and the query never carried the term. A one-way binding
 * with no writer is not a search box.
 *
 * `search` is therefore deliberately NOT in the watcher below. It used to be, which also
 * cancelled out the debounce: the watcher fires on every keystroke regardless, so the 280ms
 * timer just queued a second identical request. One path owns the term - this handler - and
 * the watcher owns only the selects.
 */
let searchTimer: ReturnType<typeof setTimeout> | undefined
onBeforeUnmount(() => clearTimeout(searchTimer))
function onSearchInput(e: Event) {
  search.value = (e.target as HTMLInputElement).value
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => { page.value = 1; load() }, 280)
}

/**
 * Clear every filter and reload.
 *
 * The explicit `load()` is required, not redundant. `search` is deliberately absent from the
 * filter watcher (it is debounced, see onSearchInput), so clearing it there fires no watcher
 * at all: measured, the button emptied the input and left the table showing the previous
 * zero-result search with "Showing 0 of 0 events". Resetting a ref is not the same thing as
 * asking for a reload.
 */
function clearFilters() {
  search.value = ''
  actionFilter.value = undefined
  actorFilter.value = undefined
  rangeFilter.value = 'all'
  page.value = 1
  load()
}

onMounted(() => {
  if (isAdmin.value) load()
})
</script>

<template>
  <!-- `flex min-h-0 flex-col` on the panel body and `flex min-h-0 flex-col` on the content
         wrapper are what let the table fill the leftover height. The vendor body is
         `flex flex-col gap-4 sm:gap-6 flex-1 overflow-y-auto`, so without `min-h-0` this
         chain cannot shrink below its content and the BODY keeps its own scrollbar - which
         is what produced the page-level scroll HIRO reported. -->
  <UDashboardPanel :ui="{ body: 'audit-panel-body flex min-h-0 flex-col' }">
    <template #header>
      <!-- PageHeader carries the sidebar collapse control in the navbar's #leading slot,
           exactly as the Nuxt dashboard template does on every page. -->
      <PageHeader title="Log Audit" />
    </template>

    <template #body>
      <div class="flex min-h-0 flex-1 flex-col gap-4">
        <UAlert
          v-if="!isAdmin"
          color="warning"
          variant="soft"
          icon="i-lucide-lock"
          title="Restricted access"
          description="Only an Admin can view the activity trail."
        />

        <template v-else>
          <!-- Summary cards. Values come from the server's aggregate over the FILTERED set,
               so they describe what the filter is currently showing rather than the whole
               table. This is stated in the sub-line because a reader who assumes otherwise
               will misread the numbers after every filter change. -->
          <div class="grid grid-cols-2 gap-3 lg:grid-cols-4">
            <div
              v-for="card in [
                { key: 'total', label: 'Events shown', value: stats?.total ?? 0, icon: 'i-lucide-activity', tone: 'text-primary', ring: 'ring-primary/20', glow: 'bg-primary/10' },
                { key: 'logins', label: 'Sign-in attempts', value: stats?.logins ?? 0, icon: 'i-lucide-log-in', tone: 'text-info', ring: 'ring-info/20', glow: 'bg-info/10' },
                { key: 'deletes', label: 'Deletions', value: stats?.deletes ?? 0, icon: 'i-lucide-trash-2', tone: DELETE_TONE, ring: 'ring-error/20', glow: 'bg-error/10' },
                { key: 'failed', label: 'Failed attempts', value: stats?.failed ?? 0, icon: 'i-lucide-shield-alert', tone: 'text-error', ring: 'ring-error/20', glow: 'bg-error/10' }
              ]"
              :key="card.key"
              class="audit-stat relative overflow-hidden rounded-xl border border-default bg-elevated p-4 ring-1 ring-inset"
              :class="card.ring"
            >
              <!-- Soft accent wash bleeding in from the top-right, clipped by this card's own
                   overflow-hidden. Same trick as the delete dialog and the welcome banner, and
                   keyed to the theme so it follows an accent change. -->
              <div :class="['pointer-events-none absolute -right-8 -top-10 size-24 rounded-full blur-2xl', card.glow]" aria-hidden="true" />
              <div class="relative flex items-start justify-between gap-2">
                <div class="min-w-0">
                  <p class="text-xs font-medium uppercase tracking-wide text-dimmed">
                    {{ card.label }}
                  </p>
                  <!-- tabular-nums so the digits do not jitter as counts change on refresh -->
                  <p class="mt-1.5 text-2xl font-bold tabular-nums text-default">
                    {{ card.value.toLocaleString() }}
                  </p>
                </div>
                <span :class="['grid size-9 shrink-0 place-items-center rounded-lg', card.glow, card.tone]">
                  <UIcon :name="card.icon" class="size-5" />
                </span>
              </div>
            </div>
          </div>

          <!-- Register card. Same block shape as the other two logbooks: rounded-lg border
               bg-elevated p-4, so the three pages read as one application.
               `flex min-h-0 flex-1 flex-col` is the middle link of the height chain: the
               panel body flexes, this card takes the leftover, and the table inside takes
               what is left after the header and pager. Without `min-h-0` on each link the
               card refuses to shrink and pushes the whole page past the viewport. -->
          <div class="flex min-h-0 flex-1 flex-col rounded-lg border border-default bg-elevated p-4">
            <div class="flex flex-wrap items-center justify-between gap-3">
              <div>
                <h2 class="text-lg font-bold tracking-wide">Activity Trail</h2>
                <p class="text-xs text-muted">
                  Every create, update, delete and sign-in, newest first.
                  <span class="text-dimmed">Read-only.</span>
                </p>
              </div>

              <div class="flex items-center gap-2">
                <UButton
                  v-if="anyFilterActive"
                  label="Clear filters"
                  icon="i-lucide-x"
                  color="neutral"
                  variant="ghost"
                  size="sm"
                  @click="clearFilters"
                />
                <UButton
                  label="Refresh"
                  icon="i-lucide-refresh-cw"
                  color="neutral"
                  variant="outline"
                  size="sm"
                  :loading="loading"
                  @click="load"
                />
              </div>
            </div>

            <!-- Filter bar. Action chips double as the legend for the icons in the table, so
                 the mapping from a colour to a meaning is stated before the reader meets it.
                 `shrink-0` because this row is fixed height; without it the filter bar is
                 itself a flex candidate and steals space the table needs when the viewport
                 is short. -->
            <div class="mt-4 shrink-0 space-y-3">
              <div class="flex flex-wrap items-center gap-2">
                <div class="relative">
                  <UIcon name="i-lucide-search" class="pointer-events-none absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-dimmed" />
                  <input
                    :value="search"
                    type="search"
                    placeholder="Search summary, target or user…"
                    class="h-9 w-full rounded-lg border border-default bg-default pl-8 pr-3 text-sm text-default outline-none placeholder:text-dimmed focus:ring-2 focus:ring-primary sm:w-72"
                    @input="onSearchInput"
                  >
                </div>

                <select
                  v-model="rangeFilter"
                  class="h-9 rounded-lg border border-default bg-default px-2.5 text-sm text-default outline-none focus:ring-2 focus:ring-primary"
                >
                  <option value="all">All time</option>
                  <option value="today">Today</option>
                  <option value="week">Last 7 days</option>
                  <option value="month">This month</option>
                </select>

                <select
                  :value="actorFilter"
                  class="h-9 rounded-lg border border-default bg-default px-2.5 text-sm text-default outline-none focus:ring-2 focus:ring-primary"
                  @change="actorFilter = ($event.target as HTMLSelectElement).value || undefined"
                >
                  <option value="">All users</option>
                  <option v-for="a in stats?.actors ?? []" :key="a.username" :value="a.username">
                    {{ a.username }} ({{ a.count }})
                  </option>
                </select>
              </div>

              <div class="flex flex-wrap items-center gap-1.5">
                <button
                  type="button"
                  class="audit-chip"
                  :class="actionFilter ? 'border-default bg-default text-muted' : 'border-primary/40 bg-primary/10 text-primary'"
                  @click="actionFilter = undefined"
                >
                  All activity
                </button>
                <button
                  v-for="a in ACTIONS"
                  :key="a.value"
                  type="button"
                  class="audit-chip"
                  :class="actionFilter === a.value ? 'border-primary/40 bg-primary/10 text-primary' : 'border-default bg-default text-muted hover:text-default'"
                  @click="actionFilter = actionFilter === a.value ? undefined : a.value"
                >
                  <UIcon :name="a.icon" class="size-3.5" />
                  {{ a.label }}
                </button>
              </div>
            </div>

            <!-- Table. `table-fixed` + <colgroup> rather than percentage widths on <th>: percentage
                 widths are only hints, so at the office's 1920px the columns drift apart from
                 what the declaration intended.

                 Column widths were REBALANCED after a screenshot review: TARGET was fixed at
                 150px and ellipsised to "Handover Log Bo..." while the DETAIL column held 991px
                 of mostly empty space. A truncated column sitting next to a void reads as a bug.
                 The last column now flexes and TARGET is wide enough for the longest real
                 target name ("Handover Log Book", "CCTV Access Request Log").

                 HEIGHT: the table fills the viewport instead of being capped at 62vh, which
                 left the page scrolling twice - the panel body AND this table. Measured at
                 1920x1080: content 1002px inside a 1016px panel body, i.e. 34px over, so
                 the body grew a scrollbar of its own. `flex-1 min-h-0` on this wrapper plus
                 the same pair on the register card and its body wrapper makes the table take
                 exactly the leftover height, so only the table scrolls. `min-h-0` is required:
                 a flex item defaults to min-height:auto and would refuse to shrink below its
                 content instead of yielding the space.

                 Per-instance `:ui` on this page's own UDashboardPanel, NOT a theme override:
                 the same trick was rejected earlier for putting a scrollbar on three pages
                 that never had the bug. -->
            <div class="logbook-scroll audit-table mt-4 min-h-0 flex-1 overflow-auto rounded-lg border border-default">
              <table class="w-full min-w-[1180px] table-fixed">
                <colgroup>
                  <col class="w-[118px]">
                  <col class="w-[104px]">
                  <col class="w-[164px]">
                  <col class="w-[182px]">
                  <col class="w-[236px]">
                  <col class="w-[84px]">
                  <col>
                </colgroup>
                <thead class="sticky top-0 z-10 bg-elevated/95 backdrop-blur">
                  <tr class="border-b border-default">
                    <th class="px-3 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-dimmed">Date</th>
                    <th class="px-3 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-dimmed">Time</th>
                    <th class="px-3 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-dimmed">User</th>
                    <th class="px-3 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-dimmed">Action</th>
                    <th class="px-3 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-dimmed">Target</th>
                    <th class="px-3 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-dimmed">Source</th>
                    <th class="px-3 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-dimmed">Detail</th>
                  </tr>
                </thead>

                <tbody>
                  <!-- `v-for` over a wrapper <tr> so the per-row enter animation lives on a
                       real element. Keyed by id: the trail is append-only and ordered by time,
                       so Vue can reuse rows across a refresh and the animation would otherwise
                       replay on rows that did not change. -->
                  <tr v-for="row in rows" :key="row.id" class="audit-row border-b border-default/60 last:border-0 hover:bg-elevated/50">
                    <td class="px-3 py-2.5">
                      <!-- Local calendar date, independent of the clock column beside it. -->
                      <span class="block truncate text-sm tabular-nums text-default" :title="fullTime(row.createdAt)">
                        {{ dayLabel(row.createdAt) }}
                      </span>
                    </td>

                    <td class="px-3 py-2.5">
                      <span class="block text-sm font-medium tabular-nums text-default" :title="fullTime(row.createdAt)">
                        {{ relTime(row.createdAt) }}
                      </span>
                      <span class="block text-xs tabular-nums text-dimmed">{{ clockTime(row.createdAt) }}</span>
                    </td>

                    <td class="px-3 py-2.5">
                      <div class="flex items-center gap-2">
                        <span class="grid size-7 shrink-0 place-items-center rounded-full bg-elevated text-xs font-semibold text-muted ring-1 ring-inset ring-default">
                          {{ initialsOf(row.username) }}
                        </span>
                        <span class="truncate text-sm text-default" :title="row.username">{{ row.username }}</span>
                      </div>
                    </td>

                    <td class="px-3 py-2.5">
                      <!-- icon + word, never the icon alone: colour must not be the only
                           carrier of the meaning. A failed row keeps its OWN action's icon
                           and names the action, with the red state carried by the small
                           "Rejected" tag — previously it swapped the icon for a generic cross,
                           which lost WHICH action had been rejected. -->
                      <span :class="['inline-flex items-center gap-1.5 text-sm font-medium', actionMeta(row.action).tone, !row.success && 'text-error']">
                        <UIcon :name="actionMeta(row.action).icon" class="size-4 shrink-0" />
                        <span class="truncate">{{ actionMeta(row.action).label }}</span>
                        <span v-if="!row.success" class="shrink-0 rounded bg-error/15 px-1 py-px text-[10px] font-semibold uppercase tracking-wide text-error">
                          Rejected
                        </span>
                      </span>
                    </td>

                    <td class="px-3 py-2.5">
                      <span class="block truncate text-sm text-default" :title="row.target">{{ row.target }}</span>
                      <span v-if="row.targetId" class="block truncate text-xs tabular-nums text-dimmed">#{{ row.targetId }}</span>
                    </td>

                    <!-- Source IP. Informative for an audit trail and, on this deployment,
                         always "::1"/"127.0.0.1" in dev because the API and the browser are on
                         the same machine - the column earns its place on the office server,
                         where it shows the requesting workstation. -->
                    <td class="px-3 py-2.5">
                      <span class="block truncate text-xs tabular-nums text-muted" :title="row.ipAddress || 'not recorded'">
                        {{ row.ipAddress || '—' }}
                      </span>
                    </td>

                    <td class="px-3 py-2.5">
                      <span class="block truncate text-sm text-muted" :title="row.summary">{{ row.summary }}</span>
                    </td>
                  </tr>

                  <tr v-if="!loading && !rows.length">
                    <td colspan="7" class="px-3 py-14 text-center">
                      <UIcon name="i-lucide-inbox" class="mx-auto size-8 text-dimmed" />
                      <p class="mt-3 text-sm font-medium text-default">
                        {{ anyFilterActive ? 'No activity matches these filters' : 'No activity recorded yet' }}
                      </p>
                      <p class="mt-1 text-sm text-muted">
                        {{ anyFilterActive ? 'Try widening the date range or clearing the filters.' : 'Actions appear here as soon as users start working.' }}
                      </p>
                    </td>
                  </tr>
                </tbody>
              </table>

              <!-- Skeleton rows, so a slow query does not collapse the table to a single
                   "no rows" line and then pop back into shape. -->
              <div v-if="loading && !rows.length" class="space-y-2 p-3">
                <div v-for="i in 6" :key="i" class="h-9 animate-pulse rounded-md bg-elevated" />
              </div>
            </div>

            <!-- Pager. Count text is explicit about what it counts, because the header card
                 above it shows a DIFFERENT number (events in the filtered set, not on this
                 page) and conflating the two is an easy misread. `shrink-0` so it stays
                 pinned under the table instead of being squeezed by it. -->
            <div class="mt-3 flex shrink-0 flex-wrap items-center justify-between gap-3">
              <p class="text-xs text-muted">
                Showing <span class="tabular-nums">{{ rows.length }}</span> of
                <span class="tabular-nums">{{ total.toLocaleString() }}</span> events
                <template v-if="anyFilterActive">
                  · <button type="button" class="underline underline-offset-2 hover:text-default" @click="clearFilters">clear filters</button>
                </template>
              </p>

              <div class="flex items-center gap-2">
                <select
                  v-model.number="pageSize"
                  class="h-8 rounded-lg border border-default bg-default px-2 text-xs text-default outline-none focus:ring-2 focus:ring-primary"
                >
                  <option :value="25">25 / page</option>
                  <option :value="50">50 / page</option>
                  <option :value="100">100 / page</option>
                </select>

                <div class="flex items-center gap-1">
                  <UButton
                    icon="i-lucide-chevron-left"
                    color="neutral"
                    variant="ghost"
                    size="sm"
                    :disabled="page <= 1 || loading"
                    aria-label="Previous page"
                    @click="page--"
                  />
                  <span class="px-2 text-xs tabular-nums text-muted">{{ page }} / {{ pageCount }}</span>
                  <UButton
                    icon="i-lucide-chevron-right"
                    color="neutral"
                    variant="ghost"
                    size="sm"
                    :disabled="page >= pageCount || loading"
                    aria-label="Next page"
                    @click="page++"
                  />
                </div>
              </div>
            </div>
          </div>
        </template>
      </div>
    </template>
  </UDashboardPanel>
</template>

<style>
/* Filter chips and rows carry their own classes rather than long utility strings, so the
   active/inactive pair is one edit rather than four. */
.audit-chip {
  display: inline-flex;
  align-items: center;
  gap: 0.375rem;
  border-width: 1px;
  border-radius: 0.5rem;
  padding: 0.25rem 0.625rem;
  font-size: 0.75rem;
  font-weight: 500;
  line-height: 1rem;
  transition: background-color 150ms ease, color 150ms ease, border-color 150ms ease;
}

/* Row entrance. Deliberately NOT keyed to `loading`: a mount-once reveal belongs on the row
   itself, and binding it to the fetch flag replays the animation under the cursor on every
   refresh, which reads as a flicker. The delay is a fixed 18ms, not a function of page state.
   Only opacity and a small translate: anything bigger reads as slow. */
@keyframes infra-audit-row {
  from { opacity: 0; transform: translate3d(0, 6px, 0); }
  to   { opacity: 1; transform: none; }
}

.audit-row {
  animation: infra-audit-row 280ms cubic-bezier(0.22, 1, 0.36, 1) both;
  animation-delay: 18ms;
}

/* Summary cards fade up once, on the element, never on a data flag. */
@keyframes infra-audit-card {
  from { opacity: 0; transform: translate3d(0, 8px, 0); }
  to   { opacity: 1; transform: none; }
}

.audit-stat {
  animation: infra-audit-card 320ms cubic-bezier(0.22, 1, 0.36, 1) both;
}

@media (prefers-reduced-motion: reduce) {
  .audit-row,
  .audit-stat {
    animation: none !important;
    opacity: 1 !important;
    transform: none !important;
  }
  .audit-chip { transition: none !important; }
}

/* -------------------------------------------------------------------------------------------
   HIDE the panel body's scrollbar - keep the scrolling.

   HIRO asked for no body scrollbar and a table that fits the screen, so the body should not
   be scrolling at all once the flex height chain is in place. `overflow-y: auto` stays
   exactly as the vendor has it: if a viewport is ever too short to fit the cards, the page
   degrades to a working scroll rather than clipping the pager. Only the scrollbar's
   APPEARANCE is suppressed, by giving it zero width.

   Scoped to `.audit-panel-body`, a class that exists only on this page's own
   UDashboardPanel via :ui. Same approach as `.cctv-panel-body` on the CCTV page and for the
   same reason: a theme override in app.config.ts was tried for the CCTV flicker and was
   rejected because it put a permanent scrollbar on three pages that never had the bug.

   The standard properties cover Firefox, ::-webkit covers Chrome/Edge; both are needed
   because a browser supporting neither would otherwise still paint a scrollbar.
   ------------------------------------------------------------------------------------------- */
.audit-panel-body {
  scrollbar-width: none;
  -ms-overflow-style: none;
}

.audit-panel-body::-webkit-scrollbar {
  width: 0;
  height: 0;
}

.audit-panel-body::-webkit-scrollbar-track {
  background: transparent;
}

.audit-panel-body::-webkit-scrollbar-thumb {
  background: transparent;
}
</style>