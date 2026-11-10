<script setup lang="ts">
import type { AccessPoint, AccessPointInput, FloorMapDetail, FloorMapSummary } from '~/types'

const { isAdmin } = useAuth()
const toast = useToast()
const floors = ref<FloorMapSummary[]>([])
const activeId = ref<number | null>(null)
const floor = ref<FloorMapDetail | null>(null)
const imageUrl = ref('')
const selectedId = ref<number | null>(null)
const loading = ref(true)
const busy = ref(false)
const error = ref('')
const uploadName = ref('')
const uploadFile = ref<File | null>(null)
const uploadInput = ref<HTMLInputElement | null>(null)
const uploadPreviewUrl = ref('')
const uploadError = ref('')
const showUpload = ref(false)
const showEditor = ref(false)
const showDetails = ref(false)
const detailError = ref('')
const editingId = ref<number | null>(null)
const editorError = ref('')
const draft = reactive<AccessPointInput>({ hostname: '', ipAddress: '', position: '', x: 0.5, y: 0.5 })
const mapEl = ref<HTMLElement | null>(null)
const mapStageEl = ref<HTMLElement | null>(null)
const mapNatural = ref<{ w: number; h: number } | null>(null)
const mapFit = ref<{ w: number; h: number } | null>(null)
let mapResizeObserver: ResizeObserver | null = null

function fitMap() {
  const stage = mapStageEl.value
  const nat = mapNatural.value
  if (!stage || !nat || nat.w <= 0 || nat.h <= 0) return
  const cw = stage.clientWidth, ch = stage.clientHeight
  if (cw <= 0 || ch <= 0) return
  const scale = Math.min(cw / nat.w, ch / nat.h)
  mapFit.value = { w: Math.max(1, Math.floor(nat.w * scale)), h: Math.max(1, Math.floor(nat.h * scale)) }
}
function onMapImageLoad(event: Event) {
  const img = event.target as HTMLImageElement
  if (img.naturalWidth > 0) mapNatural.value = { w: img.naturalWidth, h: img.naturalHeight }
  fitMap()
}
const mapFitStyle = computed(() => mapFit.value ? { width: `${mapFit.value.w}px`, height: `${mapFit.value.h}px` } : undefined)

watch(mapStageEl, el => {
  mapResizeObserver?.disconnect()
  mapResizeObserver = null
  if (el) {
    mapResizeObserver = new ResizeObserver(() => fitMap())
    mapResizeObserver.observe(el)
  }
  fitMap()
})
const selectedPoint = computed(() => floor.value?.accessPoints.find(p => p.id === selectedId.value) ?? null)
let loadVersion = 0
let suppressMarkerClickId: number | null = null
let suppressMarkerClickUntil = 0
let drag: { id: number; mapId: number; pointer: number; startX: number; startY: number; clientX: number; clientY: number; moved: boolean } | null = null

watch(showUpload, open => {
  if (open) return
  uploadName.value = ''; uploadFile.value = null; uploadError.value = ''
  if (uploadInput.value) uploadInput.value.value = ''
  setUploadPreviewUrl('')
})
watch(showDetails, open => { if (!open) selectedId.value = null })

function message(e: any): string { return e?.data?.message || e?.message || 'The request could not be completed.' }
function setImageUrl(url: string) {
  if (imageUrl.value) URL.revokeObjectURL(imageUrl.value)
  imageUrl.value = url
}
function setUploadPreviewUrl(url: string) {
  if (uploadPreviewUrl.value) URL.revokeObjectURL(uploadPreviewUrl.value)
  uploadPreviewUrl.value = url
}
async function loadFloor(id: number) {
  const version = ++loadVersion
  showDetails.value = false
  showEditor.value = false
  activeId.value = id
  floor.value = null
  selectedId.value = null
  setImageUrl('')
  mapNatural.value = null
  mapFit.value = null
  error.value = ''
  try {
    const [detail, blob] = await Promise.all([apiGetNetworkFloor(id), apiGetNetworkFloorImage(id)])
    if (version !== loadVersion) return
    floor.value = detail
    setImageUrl(URL.createObjectURL(blob))
  } catch (e) { if (version === loadVersion) error.value = message(e) }
}
async function refresh(preferId?: number) {
  loading.value = true
  error.value = ''
  try {
    floors.value = await apiListNetworkFloors()
    const next = preferId && floors.value.some(f => f.id === preferId) ? preferId
      : floors.value.some(f => f.id === activeId.value) ? activeId.value! : floors.value[0]?.id
    if (next) await loadFloor(next)
    else { ++loadVersion; activeId.value = null; floor.value = null; setImageUrl('') }
  } catch (e) { error.value = message(e) }
  finally { loading.value = false }
}
onMounted(() => { void refresh() })
onBeforeUnmount(() => { ++loadVersion; mapResizeObserver?.disconnect(); setImageUrl(''); setUploadPreviewUrl('') })

function pickImage(event: Event) {
  uploadFile.value = (event.target as HTMLInputElement).files?.[0] ?? null
  uploadError.value = ''
  setUploadPreviewUrl(uploadFile.value && ['image/png', 'image/jpeg', 'image/webp'].includes(uploadFile.value.type)
    ? URL.createObjectURL(uploadFile.value) : '')
}
async function createFloor() {
  if (!isAdmin.value || busy.value) return
  if (!uploadName.value.trim() || !uploadFile.value) { uploadError.value = 'Enter a floor name and choose a plan image.'; return }
  if (uploadFile.value.size > 8 * 1024 * 1024) { uploadError.value = 'The image must be smaller than 8 MB.'; return }
  busy.value = true; uploadError.value = ''
  try {
    const created = await apiCreateNetworkFloor(uploadName.value.trim(), uploadFile.value)
    showUpload.value = false
    await refresh(created.id)
    toast.add({ title: 'Floor plan added', color: 'primary' })
  } catch (e) { uploadError.value = message(e) }
  finally { busy.value = false }
}
async function deleteFloor(item: FloorMapSummary) {
  if (!isAdmin.value || busy.value) return
  if (!window.confirm(`Delete “${item.name}” and all its access points?`)) return
  busy.value = true; error.value = ''
  try {
    await apiDeleteNetworkFloor(item.id)
    if (activeId.value === item.id) activeId.value = null
    await refresh()
    toast.add({ title: 'Floor plan deleted', color: 'primary' })
  } catch (e) { error.value = message(e) }
  finally { busy.value = false }
}
function openAdd(x = 0.5, y = 0.5) {
  if (!isAdmin.value || !floor.value || busy.value) return
  showDetails.value = false
  editingId.value = null
  Object.assign(draft, { hostname: '', ipAddress: '', position: '', x, y })
  editorError.value = ''; showEditor.value = true
}
function openDetails(point: AccessPoint) {
  if (point.id === suppressMarkerClickId && performance.now() < suppressMarkerClickUntil) return
  selectedId.value = point.id
  detailError.value = ''
  showDetails.value = true
}
function openEdit(point: AccessPoint) {
  if (!isAdmin.value || busy.value) return
  showDetails.value = false
  editingId.value = point.id
  Object.assign(draft, { hostname: point.hostname, ipAddress: point.ipAddress, position: point.position, x: point.x, y: point.y })
  editorError.value = ''; showEditor.value = true
}
async function savePoint() {
  if (!isAdmin.value || !floor.value || busy.value) return
  if (!draft.hostname.trim() || !draft.ipAddress.trim()) { editorError.value = 'Hostname and IP address are required.'; return }
  const id = floor.value.id
  busy.value = true; editorError.value = ''
  try {
    const payload = { ...draft, hostname: draft.hostname.trim(), ipAddress: draft.ipAddress.trim(), position: draft.position.trim() }
    const saved = editingId.value === null ? await apiCreateAccessPoint(id, payload)
      : await apiUpdateAccessPoint(id, editingId.value, payload)
    if (floor.value?.id === id) {
      const index = floor.value.accessPoints.findIndex(p => p.id === saved.id)
      if (index < 0) floor.value.accessPoints.push(saved)
      else floor.value.accessPoints[index] = saved
      selectedId.value = saved.id
    }
    floors.value = floors.value.map(f => f.id === id ? { ...f, accessPointCount: floor.value?.accessPoints.length ?? 0 } : f)
    showEditor.value = false
    toast.add({ title: editingId.value === null ? 'Access point added' : 'Access point updated', color: 'primary' })
  } catch (e) { editorError.value = message(e) }
  finally { busy.value = false }
}
async function deletePoint() {
  if (!isAdmin.value || !floor.value || !selectedPoint.value || busy.value) return
  const id = floor.value.id, point = selectedPoint.value
  if (!window.confirm(`Delete access point “${point.hostname}”?`)) return
  busy.value = true; error.value = ''
  try {
    await apiDeleteAccessPoint(id, point.id)
    if (floor.value?.id === id) floor.value.accessPoints = floor.value.accessPoints.filter(p => p.id !== point.id)
    floors.value = floors.value.map(f => f.id === id ? { ...f, accessPointCount: floor.value?.accessPoints.length ?? 0 } : f)
    selectedId.value = null; showEditor.value = false; showDetails.value = false
    toast.add({ title: 'Access point deleted', color: 'primary' })
  } catch (e) { detailError.value = message(e) }
  finally { busy.value = false }
}
function placeOnMap(event: MouseEvent) {
  if (!isAdmin.value || !mapEl.value || event.target !== mapEl.value && event.target !== mapEl.value.querySelector('img')) return
  const rect = mapEl.value.getBoundingClientRect()
  openAdd(Math.max(0, Math.min(1, (event.clientX - rect.left) / rect.width)),
    Math.max(0, Math.min(1, (event.clientY - rect.top) / rect.height)))
}
function pointerDown(event: PointerEvent, point: AccessPoint) {
  selectedId.value = point.id
  if (!isAdmin.value || !floor.value || busy.value) return
  event.preventDefault()
  ;(event.currentTarget as HTMLElement).setPointerCapture(event.pointerId)
  drag = { id: point.id, mapId: floor.value.id, pointer: event.pointerId, startX: point.x, startY: point.y,
    clientX: event.clientX, clientY: event.clientY, moved: false }
}
function pointerMove(event: PointerEvent, point: AccessPoint) {
  if (!drag || drag.pointer !== event.pointerId || drag.id !== point.id || drag.mapId !== floor.value?.id || !mapEl.value) return
  const dx = event.clientX - drag.clientX, dy = event.clientY - drag.clientY
  if (!drag.moved && Math.hypot(dx, dy) < 4) return
  drag.moved = true
  const rect = mapEl.value.getBoundingClientRect()
  point.x = Math.max(0, Math.min(1, Math.round((drag.startX + dx / rect.width) * 10000) / 10000))
  point.y = Math.max(0, Math.min(1, Math.round((drag.startY + dy / rect.height) * 10000) / 10000))
}
async function pointerUp(event: PointerEvent, point: AccessPoint, cancelled = false) {
  if (!drag || drag.pointer !== event.pointerId || drag.id !== point.id) return
  const current = drag; drag = null
  if (current.moved) { suppressMarkerClickId = point.id; suppressMarkerClickUntil = performance.now() + 250 }
  if (cancelled) { point.x = current.startX; point.y = current.startY; return }
  if (!current.moved || current.mapId !== floor.value?.id) return
  try {
    const saved = await apiMoveAccessPoint(current.mapId, point.id, point.x, point.y)
    if (floor.value?.id === current.mapId) { point.x = saved.x; point.y = saved.y }
  } catch (e) {
    if (floor.value?.id === current.mapId) { point.x = current.startX; point.y = current.startY }
    error.value = message(e)
  }
}
</script>

<template>
  <UDashboardPanel id="network-access-point" :ui="{ body: 'flex min-h-0 flex-col p-4 sm:p-6' }">
    <template #header><PageHeader title="Access Point" /></template>
    <template #body>
      <div class="mx-auto flex min-h-0 w-full max-w-[1500px] flex-1 flex-col gap-5">
        <div class="ap-header-row flex flex-wrap items-center gap-x-4 gap-y-3 rounded-lg border border-default bg-elevated p-4">
          <div class="flex w-full min-w-0 items-center gap-3 xl:max-w-[320px]">
            <span class="grid size-12 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary ring-1 ring-inset ring-primary/20" aria-hidden="true">
              <RecordAddIcon size="lg" random-halo>
                <svg class="pc-add-tilt size-7" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" focusable="false">
                  <path class="ap-wifi-outer" d="M2 8.8a15 15 0 0 1 20 0" />
                  <path class="ap-wifi-mid" d="M5 12a10 10 0 0 1 14 0" />
                  <path d="M8.5 15.2a5 5 0 0 1 7 0" />
                  <circle cx="12" cy="19" r="1" fill="currentColor" stroke="none" />
                </svg>
              </RecordAddIcon>
            </span>
            <div class="min-w-0">
              <h2 class="text-lg font-bold tracking-wide text-highlighted">Access Point</h2>
              <p class="text-xs text-muted">{{ isAdmin ? 'Click the plan to add an AP, drag markers to move, or select one for details.' : 'Select a marker to see its network details.' }}</p>
            </div>
          </div>
          <div v-if="floors.length" class="ap-floor-list flex w-full min-w-0 gap-2 overflow-x-auto" aria-label="Floor plans">
            <div v-for="item in floors" :key="item.id" class="ap-floor-tab relative w-[156px] shrink-0 rounded-lg border sm:w-[176px]"
              :class="activeId === item.id ? 'border-primary/50 bg-primary/10 text-primary' : 'border-default bg-default/60 text-muted hover:border-primary/40 hover:text-highlighted'">
              <button type="button" class="flex h-12 w-full flex-col justify-center gap-0.5 rounded-lg py-1 pl-3 text-left focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
                :class="isAdmin ? 'pr-10' : 'pr-3'" :aria-pressed="activeId === item.id" :disabled="busy" @click="loadFloor(item.id)">
                <span class="w-full truncate text-sm font-semibold leading-tight" :title="item.name">{{ item.name }}</span>
                <span class="text-[11px] leading-tight opacity-70">{{ item.accessPointCount }} AP</span>
              </button>
              <UButton v-if="isAdmin" type="button" icon="i-lucide-trash-2" size="xs" color="neutral" variant="ghost"
                class="ap-plan-delete absolute right-1 top-1" :aria-label="`Delete floor plan ${item.name}`" :title="`Delete ${item.name}`"
                :disabled="busy" @click.stop="deleteFloor(item)" />
            </div>
          </div>
          <div v-if="isAdmin" class="ap-header-actions flex w-full shrink-0 items-center justify-end gap-2">
            <UButton v-if="floors.length" type="button" size="sm" icon="bi:router" class="ap-ap-add-action record-add-hover stable-hover-hitbox" :disabled="busy || !floor" @click="openAdd()">Add AP</UButton>
            <UButton type="button" size="sm" icon="iconoven:floor-plan" variant="soft" class="ap-floor-add-action record-add-hover stable-hover-hitbox" :disabled="busy" @click="showUpload = true">Add floor plan</UButton>
          </div>
        </div>

        <p v-if="error" role="alert" class="rounded-lg border border-error/30 bg-error/10 px-3 py-2 text-sm text-error">{{ error }}</p>
        <div v-if="loading" class="py-10 text-center text-sm text-muted">Loading floor plans…</div>
        <UCard v-else-if="!floors.length" :ui="{ body: 'p-10 text-center' }">
          <div class="mx-auto grid size-14 place-items-center rounded-2xl bg-primary/10 text-primary"><UIcon name="i-lucide-wifi" class="size-7" /></div>
          <h3 class="mt-4 font-semibold text-highlighted">No floor plans yet</h3>
          <p class="mt-1 text-sm text-muted">{{ isAdmin ? 'Upload a plan to start mapping access points.' : 'An administrator can add the first floor plan.' }}</p>
        </UCard>
        <template v-else>
          <UCard :ui="{ body: 'flex min-h-0 flex-1 flex-col p-2 sm:p-3' }" class="flex min-h-0 min-w-0 flex-1 flex-col">
            <div v-if="floor && imageUrl" ref="mapStageEl" class="flex min-h-0 flex-1 items-center justify-center overflow-hidden">
                <div ref="mapEl" :style="mapFitStyle" class="relative shrink-0 overflow-hidden rounded-lg border border-default bg-elevated" :class="isAdmin ? 'cursor-crosshair' : ''" @click="placeOnMap">
                <img :src="imageUrl" :alt="`Floor plan: ${floor.name}`" draggable="false" class="block h-full w-full select-none" @load="onMapImageLoad" />
                <button v-for="point in floor.accessPoints" :key="point.id" type="button"
                  class="ap-marker absolute grid size-8 -translate-x-1/2 -translate-y-1/2 place-items-center rounded-full border-2 border-white bg-primary text-white shadow-md outline-primary transition-[box-shadow] focus-visible:outline-2 sm:size-9"
                  :class="[selectedId === point.id ? 'ring-4 ring-primary/40' : 'hover:ring-4 hover:ring-primary/30', isAdmin ? 'cursor-grab active:cursor-grabbing touch-none' : 'cursor-pointer']"
                  :style="{ left: `${point.x * 100}%`, top: `${point.y * 100}%` }" :aria-label="`${point.hostname}, ${point.ipAddress}${point.position ? `, ${point.position}` : ''}`"
                  :title="point.hostname" @click.stop="openDetails(point)" @pointerdown.stop="pointerDown($event, point)" @pointermove="pointerMove($event, point)" @pointerup="pointerUp($event, point)" @pointercancel="pointerUp($event, point, true)">
                  <span class="ap-marker-ping absolute -inset-1 rounded-full border-2" aria-hidden="true" />
                  <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" class="relative z-10 size-4 pointer-events-none">
                    <path class="ap-wifi-outer" d="M2 8.8a15 15 0 0 1 20 0" />
                    <path class="ap-wifi-mid" d="M5 12a10 10 0 0 1 14 0" />
                    <path class="ap-wifi-inner" d="M8.5 15.2a5 5 0 0 1 7 0" />
                    <circle cx="12" cy="19" r="1" fill="currentColor" stroke="none" />
                  </svg>
                </button>
                </div>
              </div>
              <p v-else class="flex flex-1 items-center justify-center py-20 text-center text-sm text-muted">Loading floor-plan image…</p>
          </UCard>
        </template>
      </div>

      <UModal v-if="isAdmin" v-model:open="showUpload" title="Add floor plan" description="Upload an image and give this map a clear name." :dismissible="!busy" :close="!busy" :ui="{ content: 'max-w-lg ap-upload-modal', header: 'relative z-10', body: 'relative z-10 p-4 sm:p-5' }">
        <template #body>
          <form class="space-y-4" @submit.prevent="createFloor">
            <div class="flex items-start gap-3 rounded-xl border border-primary/20 bg-primary/5 p-3 text-sm text-muted">
              <span class="ap-modal-icon grid size-9 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary" :class="{ 'ap-modal-icon-active': showUpload }"><UIcon name="i-lucide-upload" class="ap-upload-glyph size-5" /></span>
              <p>Upload a floor image, then place access points directly on the plan.</p>
            </div>
            <div><label for="floor-name" class="mb-1.5 block text-sm font-medium text-highlighted">Floor name</label><UInput id="floor-name" v-model="uploadName" placeholder="e.g. Building A · Level 2" class="w-full" :disabled="busy" maxlength="120" /></div>
            <div>
              <label for="floor-image" class="mb-1.5 block text-sm font-medium text-highlighted">Floor-plan image</label>
              <div class="flex min-w-0 items-center gap-3 rounded-xl border border-dashed border-default bg-elevated/40 p-3">
                <input id="floor-image" ref="uploadInput" type="file" accept="image/png,image/jpeg,image/webp" class="ap-file-input sr-only" :disabled="busy" @change="pickImage" />
                <label for="floor-image" class="ap-file-trigger inline-flex shrink-0 items-center gap-2 rounded-lg border border-primary/25 bg-primary/10 px-3 py-2 text-sm font-semibold text-primary" :class="{ 'ap-file-disabled': busy }">
                  <UIcon name="i-lucide-upload" class="ap-choose-icon size-4" aria-hidden="true" />Choose file
                </label>
                <span class="min-w-0 truncate text-sm" :class="uploadFile ? 'text-highlighted' : 'text-muted'" :title="uploadFile?.name || 'No file selected'">{{ uploadFile?.name || 'No file selected' }}</span>
              </div>
              <p class="mt-1.5 text-xs text-muted">PNG, JPG or WebP · up to 8 MB</p>
              <div v-if="uploadPreviewUrl" class="mt-3 overflow-hidden rounded-lg border border-default bg-elevated p-2">
                <img :src="uploadPreviewUrl" alt="Preview of selected floor plan" class="h-36 w-full object-contain" />
                <p class="mt-1.5 truncate text-xs text-muted">{{ uploadFile?.name }}</p>
              </div>
            </div>
            <p role="alert" class="min-h-5 text-sm text-error">{{ uploadError }}</p>
            <div class="flex justify-end gap-2 border-t border-default pt-4">
              <UButton type="button" color="red" variant="ghost" icon="basil:cancel-outline" class="cancel-hover" :disabled="busy" @click="showUpload = false">Cancel</UButton>
              <UButton type="submit" icon="i-lucide-upload" class="record-save-hover stable-hover-hitbox" :loading="busy" :disabled="busy">Upload plan</UButton>
            </div>
          </form>
        </template>
      </UModal>

      <UModal v-if="isAdmin" v-model:open="showEditor" :title="editingId === null ? 'Add access point' : 'Edit access point'" :description="editingId === null ? 'Place a network device on this floor plan.' : 'Update this device’s network details.'" :dismissible="!busy" :close="!busy" :ui="{ content: 'max-w-lg', body: 'p-4 sm:p-5' }">
        <template #body>
          <form class="space-y-4" @submit.prevent="savePoint">
            <div class="flex items-center gap-3 rounded-xl border border-primary/20 bg-primary/5 p-3">
              <span class="ap-modal-icon grid size-10 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary" :class="{ 'ap-modal-icon-active': showEditor }"><UIcon name="i-lucide-wifi" class="size-5" /></span>
              <div class="min-w-0"><p class="text-sm font-medium text-highlighted">{{ floor?.name }}</p><p class="text-xs text-muted">Position: {{ Math.round(draft.x * 100) }}% across · {{ Math.round(draft.y * 100) }}% down</p></div>
            </div>
            <div class="grid gap-4 sm:grid-cols-2">
              <div><label for="ap-hostname" class="mb-1.5 block text-sm font-medium text-highlighted">Hostname</label><UInput id="ap-hostname" v-model="draft.hostname" class="w-full" maxlength="160" placeholder="e.g. AP-L2-01" :disabled="busy" /></div>
              <div><label for="ap-ip" class="mb-1.5 block text-sm font-medium text-highlighted">IP address</label><UInput id="ap-ip" v-model="draft.ipAddress" class="w-full" maxlength="45" placeholder="e.g. 192.168.1.10" :disabled="busy" /></div>
            </div>
            <div><label for="ap-position" class="mb-1.5 block text-sm font-medium text-highlighted">Position / location</label><UInput id="ap-position" v-model="draft.position" class="w-full" maxlength="200" placeholder="e.g. North corridor ceiling" :disabled="busy" /></div>
            <p class="text-xs text-muted">You can drag the marker on the plan to adjust its position later.</p>
            <p role="alert" class="min-h-5 text-sm text-error">{{ editorError }}</p>
            <div class="flex justify-end gap-2 border-t border-default pt-4">
              <UButton type="button" color="red" variant="ghost" icon="basil:cancel-outline" class="cancel-hover" :disabled="busy" @click="showEditor = false">Cancel</UButton>
              <UButton type="submit" :icon="editingId === null ? 'i-lucide-plus' : 'i-lucide-pencil'" :class="editingId === null ? 'record-add-hover stable-hover-hitbox' : 'record-save-hover stable-hover-hitbox'" :loading="busy" :disabled="busy">{{ editingId === null ? 'Add AP' : 'Save changes' }}</UButton>
            </div>
          </form>
        </template>
      </UModal>

      <UModal v-model:open="showDetails" title="Access point details" description="Selected marker on this floor plan." :dismissible="!busy" :close="!busy" :ui="{ content: 'max-w-md', body: 'p-4 sm:p-5' }">
        <template #body>
          <div v-if="selectedPoint" class="space-y-4">
            <div class="flex items-center gap-3">
              <span class="ap-modal-icon grid size-12 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary ring-1 ring-inset ring-primary/20" :class="{ 'ap-modal-icon-active': showDetails }"><UIcon name="i-lucide-wifi" class="size-6" /></span>
              <div class="min-w-0"><h3 class="truncate text-base font-semibold text-highlighted">{{ selectedPoint.hostname }}</h3><p class="truncate text-xs text-muted">{{ floor?.name }}</p></div>
            </div>
            <dl class="divide-y divide-default overflow-hidden rounded-xl border border-default bg-elevated/40 text-sm">
              <div class="grid grid-cols-[105px_minmax(0,1fr)] gap-3 px-3 py-3"><dt class="text-muted">IP address</dt><dd class="break-all font-mono text-highlighted">{{ selectedPoint.ipAddress }}</dd></div>
              <div class="grid grid-cols-[105px_minmax(0,1fr)] gap-3 px-3 py-3"><dt class="text-muted">Location</dt><dd class="break-words text-highlighted">{{ selectedPoint.position || 'Not specified' }}</dd></div>
            </dl>
            <p v-if="isAdmin" role="alert" class="min-h-5 text-sm text-error">{{ detailError }}</p>
            <div class="flex justify-end gap-2 border-t border-default pt-4">
              <UButton type="button" color="neutral" variant="ghost" icon="i-lucide-x" class="ap-close-action" :disabled="busy" @click="showDetails = false">Close</UButton>
              <template v-if="isAdmin">
                <UButton type="button" icon="i-lucide-pencil" variant="soft" class="record-add-hover stable-hover-hitbox" :disabled="busy" @click="openEdit(selectedPoint)">Edit</UButton>
                <UButton type="button" icon="i-lucide-trash-2" color="error" variant="soft" class="record-delete-hover ap-stable-action" :disabled="busy" @click="deletePoint">Delete</UButton>
              </template>
            </div>
          </div>
        </template>
      </UModal>
    </template>
  </UDashboardPanel>
</template>

<style scoped>
.ap-modal-icon-active :deep(.ap-upload-glyph) { animation: ap-upload-rise 2.2s ease-in-out .42s infinite; }
@keyframes ap-upload-rise {
  0%, 100% { transform: translate3d(0, 0, 0); }
  40% { transform: translate3d(0, -3px, 0); }
  72% { transform: translate3d(0, 1px, 0); }
}
.ap-file-trigger {
  cursor: pointer;
  transition: background-color 180ms ease, border-color 180ms ease, box-shadow 180ms ease;
}
.ap-file-trigger :deep(.ap-choose-icon) { transition: transform 180ms ease; }
.ap-file-trigger.ap-file-disabled { cursor: not-allowed; opacity: .5; }
.ap-file-input:focus-visible + .ap-file-trigger { outline: 2px solid var(--ui-primary); outline-offset: 2px; }
@media (hover: hover) and (pointer: fine) {
  .ap-file-trigger:not(.ap-file-disabled):hover {
    background-color: color-mix(in srgb, var(--ui-primary) 18%, transparent);
    border-color: var(--ui-primary);
    box-shadow: 0 3px 10px color-mix(in srgb, var(--ui-primary) 12%, transparent);
  }
  .ap-file-trigger:not(.ap-file-disabled):hover :deep(.ap-choose-icon) { transform: translate3d(0, -2px, 0); }
}
@media (prefers-reduced-motion: reduce) {
  .ap-modal-icon-active :deep(.ap-upload-glyph) { animation: none; }
  .ap-file-trigger, .ap-file-trigger :deep(.ap-choose-icon) { transition: none; transform: none !important; }
}

/* Distinct glyph motions for the two map actions; the buttons keep their fixed hitboxes. */
@keyframes ap-floor-lift {
  0%, 100% { transform: translate3d(0px, 0px, 0px) rotate(0deg); }
  45% { transform: translate3d(0px, -3px, 0px) rotate(-8deg); }
  75% { transform: translate3d(0px, -1px, 0px) rotate(3deg); }
}
@keyframes ap-router-wake {
  0%, 100% { transform: scale(1) rotate(0deg); }
  40% { transform: scale(1.15) rotate(-6deg); }
  70% { transform: scale(1.08) rotate(4deg); }
}
.ap-floor-add-action.record-add-hover:not(:disabled):focus-visible :deep([data-slot='leadingIcon']) { animation: ap-floor-lift 650ms ease-in-out; }
.ap-ap-add-action.record-add-hover:not(:disabled):focus-visible :deep([data-slot='leadingIcon']) { animation: ap-router-wake 650ms ease-in-out; }
@media (hover: hover) and (pointer: fine) {
  .ap-floor-add-action.record-add-hover:not(:disabled):hover :deep([data-slot='leadingIcon']) { animation: ap-floor-lift 650ms ease-in-out; }
  .ap-ap-add-action.record-add-hover:not(:disabled):hover :deep([data-slot='leadingIcon']) { animation: ap-router-wake 650ms ease-in-out; }
}
@media (prefers-reduced-motion: reduce) {
  .ap-floor-add-action :deep([data-slot='leadingIcon']), .ap-ap-add-action :deep([data-slot='leadingIcon']) { animation: none !important; }
}

/* Only actions on this map opt in; keep button hitboxes fixed during hover. */
/* Equal side tracks align the floor-card strip with the centred map image, not the space between controls. */
@media (min-width: 1280px) {
  .ap-header-row { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 405px) minmax(0, 1fr); }
  .ap-header-actions { grid-column: 3; }
}
/* Centre the floor cards when they fit; overflow falls back to start so every card stays reachable. */
.ap-floor-list { justify-content: safe center; scrollbar-width: none; }
.ap-floor-list::-webkit-scrollbar { display: none; }
.ap-floor-tab { transition: border-color 180ms ease, background-color 180ms ease, box-shadow 180ms ease; }
.ap-plan-delete { color: var(--ui-text-muted); transition: color 180ms ease, background-color 180ms ease; }
.ap-plan-delete:focus-visible { color: var(--ui-error); outline: 2px solid var(--ui-error); outline-offset: 1px; }
.ap-close-action :deep([data-slot='leadingIcon']) { transition: transform 220ms cubic-bezier(.2,.8,.2,1); }
.ap-marker > svg { transition: transform 200ms ease-out; }
.ap-modal-icon-active { animation: ap-icon-arrive 420ms cubic-bezier(.2,.8,.2,1) both; }
@keyframes ap-icon-arrive {
  from { transform: scale(.88) rotate(-9deg); opacity: .55; }
  to { transform: scale(1) rotate(0); opacity: 1; }
}
@media (hover: hover) and (pointer: fine) {
  .ap-floor-tab:hover { box-shadow: 0 3px 10px color-mix(in srgb, var(--ui-primary) 14%, transparent); }
  .ap-plan-delete:not(:disabled):hover { color: var(--ui-error); background-color: color-mix(in srgb, var(--ui-error) 12%, transparent); }
  .ap-close-action:not(:disabled):hover :deep([data-slot='leadingIcon']) { transform: scale(1.14) rotate(12deg); }
  .ap-marker:hover:not(:active) > svg { transform: scale(1.12); }
}
.ap-marker:focus-visible > svg { transform: scale(1.12); }
.ap-stable-action.record-delete-hover:not(:disabled):not([aria-disabled='true']):is(:hover, :focus-visible) { transform: none; }
@media (prefers-reduced-motion: reduce) {
  .ap-floor-tab, .ap-plan-delete, .ap-close-action :deep([data-slot='leadingIcon']), .ap-marker > svg { transition: none; transform: none !important; }
  .ap-modal-icon-active { animation: none; }
}

/* Wifi builds up from the dot alone: dot → 1 bar → 2 bars → 3 bars, then fades back to the dot. */
.ap-wifi-inner { animation: ap-wifi-inner 2.7s ease-in-out infinite; }
.ap-wifi-mid { animation: ap-wifi-middle 2.7s ease-in-out infinite; }
.ap-wifi-outer { animation: ap-wifi-outer 2.7s ease-in-out infinite; }
@keyframes ap-wifi-inner {
  0%, 8% { opacity: .12; }
  20%, 88% { opacity: 1; }
  100% { opacity: .12; }
}
@keyframes ap-wifi-middle {
  0%, 30% { opacity: .12; }
  42%, 80% { opacity: 1; }
  92%, 100% { opacity: .12; }
}
@keyframes ap-wifi-outer {
  0%, 52% { opacity: .12; }
  62%, 72% { opacity: 1; }
  84%, 100% { opacity: .12; }
}
.ap-marker-ping {
  border-color: var(--ui-primary);
  box-shadow: 0 0 8px 2px color-mix(in srgb, var(--ui-primary) 75%, transparent),
              0 0 16px 4px color-mix(in srgb, var(--ui-primary) 30%, transparent);
  pointer-events: none;
  animation: ap-marker-breathe 2.7s ease-in-out infinite;
}
@keyframes ap-marker-breathe {
  0%, 100% { opacity: .2; transform: scale(.92); }
  50% { opacity: .65; transform: scale(1.27); }
}
@media (prefers-reduced-motion: reduce) {
  .ap-wifi-inner, .ap-wifi-mid, .ap-wifi-outer { animation: none; opacity: 1; }
  .ap-marker-ping { animation: none; opacity: 0; }
}
</style>

<style>
/* UModal teleports to body; this unique class limits the wash to the upload popup. */
.ap-upload-modal[data-slot='content']::before {
  content: '';
  position: absolute;
  top: 0;
  right: 0;
  width: min(70%, 300px);
  height: 170px;
  background: radial-gradient(ellipse at top right, color-mix(in srgb, var(--ui-primary) 17%, transparent), transparent 72%);
  pointer-events: none;
  z-index: 0;
}
</style>
