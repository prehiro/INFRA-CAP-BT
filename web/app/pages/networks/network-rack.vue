<script setup lang="ts">
import type { NetworkRackSummary, NetworkRackDetail, NetworkRackInput, NetworkSwitch, NetworkSwitchInput, FloorMapSummary, FloorMapDetail } from '~/types'

const { isAdmin } = useAuth()
const toast = useToast()
const floors = ref<FloorMapSummary[]>([])
const activeFloorId = ref<number | null>(null)
const floor = ref<FloorMapDetail | null>(null)
const imageUrl = ref('')
const racks = ref<NetworkRackSummary[]>([])
const selectedRackId = ref<number | null>(null)
const showRackEditor = ref(false)
const showSwitchEditor = ref(false)
const showRackDetails = ref(false)
const loading = ref(true)
const busy = ref(false)
const error = ref('')
const rackError = ref('')
const switchError = ref('')
const detailError = ref('')
const editingRackId = ref<number | null>(null)
const editingSwitchId = ref<number | null>(null)
const rackDraft = reactive<NetworkRackInput>({ name: '', description: '', x: 0.5, y: 0.5 })
const switchDraft = reactive<NetworkSwitchInput>({ ipAddress: '', hostname: '', position: '' })
const mapEl = ref<HTMLElement | null>(null)
const mapStageEl = ref<HTMLElement | null>(null)
const mapNatural = ref<{ w: number; h: number } | null>(null)
const mapFit = ref<{ w: number; h: number } | null>(null)
let mapResizeObserver: ResizeObserver | null = null
let disposed = false
// Only keep a few images in this page's memory; never persist protected images to disk.
const floorImages = new Map<number, Blob>()
const pendingFloorImages = new Map<number, Promise<Blob>>()

const uploadName = ref('')
const uploadFile = ref<File | null>(null)
const uploadInput = ref<HTMLInputElement | null>(null)
const uploadPreviewUrl = ref('')
const uploadError = ref('')
const showUpload = ref(false)

function getFloorImage(id: number): Promise<Blob> {
  const cached = floorImages.get(id)
  if (cached) {
    floorImages.delete(id)
    floorImages.set(id, cached)
    return Promise.resolve(cached)
  }
  const pending = pendingFloorImages.get(id)
  if (pending) return pending
  const request = apiGetNetworkFloorImage(id).then(blob => {
    if (!disposed) {
      floorImages.set(id, blob)
      if (floorImages.size > 3) floorImages.delete(floorImages.keys().next().value!)
    }
    return blob
  }).finally(() => pendingFloorImages.delete(id))
  pendingFloorImages.set(id, request)
  return request
}

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

const selectedRack = computed(() => racks.value.find(r => r.id === selectedRackId.value) ?? null)
const selectedRackDetail = ref<NetworkRackDetail | null>(null)
let loadVersion = 0
let suppressRackClickId: number | null = null
let suppressRackClickUntil = 0
let dragRack: { id: number; floorId: number; pointer: number; startX: number; startY: number; clientX: number; clientY: number; moved: boolean } | null = null

function message(e: any): string { return e?.data?.message || e?.message || 'The request could not be completed.' }
function setImageUrl(url: string) { if (imageUrl.value) URL.revokeObjectURL(imageUrl.value); imageUrl.value = url }

async function loadFloor(id: number) {
  const version = ++loadVersion
  showRackDetails.value = false
  showRackEditor.value = false
  showSwitchEditor.value = false
  activeFloorId.value = id
  floor.value = null
  racks.value = []
  selectedRackId.value = null
  setImageUrl('')
  mapNatural.value = null
  mapFit.value = null
  error.value = ''
  try {
    const [detail, blob] = await Promise.all([
      apiGetNetworkFloor(id),
      getFloorImage(id)
    ])
    if (version !== loadVersion) return
    floor.value = detail
    setImageUrl(URL.createObjectURL(blob))
    await loadRacks(id)
  } catch (e) { if (version === loadVersion) error.value = message(e) }
}
async function loadRacks(floorId: number) {
  try {
    racks.value = await apiListNetworkRacks(floorId)
  } catch (e) { error.value = message(e) }
}
async function refresh(preferFloorId?: number) {
  loading.value = true
  error.value = ''
  try {
    floors.value = await apiListNetworkFloors()
    const next = preferFloorId && floors.value.some(f => f.id === preferFloorId) ? preferFloorId
      : floors.value.some(f => f.id === activeFloorId.value) ? activeFloorId.value! : floors.value[0]?.id
    if (next) await loadFloor(next)
    else { ++loadVersion; activeFloorId.value = null; floor.value = null; setImageUrl('') }
  } catch (e) { error.value = message(e) }
  finally { loading.value = false }
}
onMounted(() => { void refresh() })
onBeforeUnmount(() => { disposed = true; ++loadVersion; floorImages.clear(); pendingFloorImages.clear(); mapResizeObserver?.disconnect(); setImageUrl(''); setUploadPreviewUrl('') })

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
  const uploadedFile = uploadFile.value
  busy.value = true; uploadError.value = ''
  try {
    const created = await apiCreateNetworkFloor(uploadName.value.trim(), uploadedFile)
    floorImages.set(created.id, uploadedFile)
    if (floorImages.size > 3) floorImages.delete(floorImages.keys().next().value!)
    showUpload.value = false
    await refresh(created.id)
    toast.add({ title: 'Floor plan added', color: 'primary' })
  } catch (e) { uploadError.value = message(e) }
  finally { busy.value = false }
}
async function deleteFloor(item: FloorMapSummary) {
  if (!isAdmin.value || busy.value) return
  if (!window.confirm(`Delete "${item.name}" and all its racks?`)) return
  busy.value = true; error.value = ''
  try {
    await apiDeleteNetworkFloor(item.id)
    floorImages.delete(item.id)
    if (activeFloorId.value === item.id) activeFloorId.value = null
    await refresh()
    toast.add({ title: 'Floor plan deleted', color: 'primary' })
  } catch (e) { error.value = message(e) }
  finally { busy.value = false }
}
function setUploadPreviewUrl(url: string) { if (uploadPreviewUrl.value) URL.revokeObjectURL(uploadPreviewUrl.value); uploadPreviewUrl.value = url }
function openAddRack(x = 0.5, y = 0.5) {
  if (!isAdmin.value || !floor.value || busy.value) return
  showRackDetails.value = false
  editingRackId.value = null
  Object.assign(rackDraft, { name: '', description: '', x, y })
  rackError.value = ''; showRackEditor.value = true
}
function openEditRack(rack: NetworkRackSummary) {
  if (!isAdmin.value || busy.value) return
  showRackDetails.value = false
  editingRackId.value = rack.id
  Object.assign(rackDraft, { name: rack.name, description: rack.description, x: rack.x, y: rack.y })
  rackError.value = ''; showRackEditor.value = true
}
async function saveRack() {
  if (!isAdmin.value || !floor.value || busy.value) return
  if (!rackDraft.name.trim()) { rackError.value = 'Rack name is required.'; return }
  const floorId = floor.value.id
  busy.value = true; rackError.value = ''
  try {
    const payload = { ...rackDraft, name: rackDraft.name.trim(), description: rackDraft.description.trim() }
    const saved = editingRackId.value === null
      ? await apiCreateNetworkRack(floorId, payload)
      : await apiUpdateNetworkRack(floorId, editingRackId.value, payload)
    if (editingRackId.value === null) racks.value.push(saved)
    else {
      const idx = racks.value.findIndex(r => r.id === editingRackId.value)
      if (idx >= 0) racks.value[idx] = saved
    }
    selectedRackId.value = saved.id
    showRackEditor.value = false
    toast.add({ title: editingRackId.value === null ? 'Rack added' : 'Rack updated', color: 'primary' })
  } catch (e) { rackError.value = message(e) }
  finally { busy.value = false }
}
async function deleteRack(rack: NetworkRackSummary) {
  if (!isAdmin.value || busy.value) return
  if (!window.confirm(`Delete rack “${rack.name}” and all its switches?`)) return
  busy.value = true; error.value = ''
  try {
    await apiDeleteNetworkRack(floor.value!.id, rack.id)
    racks.value = racks.value.filter(r => r.id !== rack.id)
    if (selectedRackId.value === rack.id) selectedRackId.value = null
    toast.add({ title: 'Rack deleted', color: 'primary' })
  } catch (e) { error.value = message(e) }
  finally { busy.value = false }
}
async function moveRack(rackId: number, x: number, y: number) {
  if (!isAdmin.value || !floor.value || busy.value) return
  try {
    const saved = await apiMoveNetworkRack(floor.value.id, rackId, x, y)
    const idx = racks.value.findIndex(r => r.id === rackId)
    if (idx >= 0) { racks.value[idx].x = saved.x; racks.value[idx].y = saved.y }
  } catch (e) { error.value = message(e) }
}

function openRackDetails(rack: NetworkRackSummary) {
  if (rack.id === suppressRackClickId && performance.now() < suppressRackClickUntil) return
  selectedRackId.value = rack.id
  showRackDetails.value = true
  detailError.value = ''
  loadRackDetail(rack.id)
}
async function loadRackDetail(rackId: number) {
  try {
    selectedRackDetail.value = await apiGetNetworkRack(floor.value!.id, rackId)
  } catch (e) { detailError.value = message(e) }
}
function closeRackDetails() { showRackDetails.value = false; selectedRackId.value = null; selectedRackDetail.value = null }

function openAddSwitch() {
  if (!isAdmin.value || !selectedRackDetail.value || busy.value) return
  showRackDetails.value = false
  editingSwitchId.value = null
  Object.assign(switchDraft, { ipAddress: '', hostname: '', position: '' })
  switchError.value = ''; showSwitchEditor.value = true
}
function openEditSwitch(sw: NetworkSwitch) {
  if (!isAdmin.value || busy.value) return
  showRackDetails.value = false
  editingSwitchId.value = sw.id
  Object.assign(switchDraft, { ipAddress: sw.ipAddress, hostname: sw.hostname, position: sw.position })
  switchError.value = ''; showSwitchEditor.value = true
}
async function saveSwitch() {
  if (!isAdmin.value || !selectedRackDetail.value || busy.value) return
  if (!switchDraft.ipAddress.trim()) { switchError.value = 'IP address is required.'; return }
  const floorId = floor.value!.id
  const rackId = selectedRackDetail.value.id
  busy.value = true; switchError.value = ''
  try {
    const payload = { ...switchDraft, ipAddress: switchDraft.ipAddress.trim(), hostname: switchDraft.hostname.trim(), position: switchDraft.position.trim() }
    const saved = editingSwitchId.value === null
      ? await apiAddNetworkSwitch(floorId, rackId, payload)
      : await apiUpdateNetworkSwitch(floorId, rackId, editingSwitchId.value, payload)
    if (selectedRackDetail.value) {
      const idx = selectedRackDetail.value.switches.findIndex(s => s.id === (editingSwitchId.value ?? -1))
      if (idx >= 0) selectedRackDetail.value.switches[idx] = saved
      else selectedRackDetail.value.switches.push(saved)
      selectedRackDetail.value.switchCount = selectedRackDetail.value.switches.length
    }
    const rackIdx = racks.value.findIndex(r => r.id === rackId)
    if (rackIdx >= 0) racks.value[rackIdx].switchCount = selectedRackDetail.value?.switchCount ?? 0
    showSwitchEditor.value = false
    toast.add({ title: editingSwitchId.value === null ? 'Switch added' : 'Switch updated', color: 'primary' })
  } catch (e) { switchError.value = message(e) }
  finally { busy.value = false }
}
async function deleteSwitch(sw: NetworkSwitch) {
  if (!isAdmin.value || !selectedRackDetail.value || busy.value) return
  if (!window.confirm(`Delete switch ${sw.ipAddress}?`)) return
  busy.value = true; detailError.value = ''
  try {
    await apiDeleteNetworkSwitch(floor.value!.id, selectedRackDetail.value.id, sw.id)
    if (selectedRackDetail.value) {
      selectedRackDetail.value.switches = selectedRackDetail.value.switches.filter(s => s.id !== sw.id)
      selectedRackDetail.value.switchCount = selectedRackDetail.value.switches.length
    }
    const rackIdx = racks.value.findIndex(r => r.id === selectedRackDetail.value?.id)
    if (rackIdx >= 0) racks.value[rackIdx].switchCount = selectedRackDetail.value?.switchCount ?? 0
    toast.add({ title: 'Switch deleted', color: 'primary' })
  } catch (e) { detailError.value = message(e) }
  finally { busy.value = false }
}

function placeOnMap(event: MouseEvent) {
  if (!isAdmin.value || !mapEl.value || event.target !== mapEl.value && event.target !== mapEl.value.querySelector('img')) return
  const rect = mapEl.value.getBoundingClientRect()
  openAddRack(Math.max(0, Math.min(1, (event.clientX - rect.left) / rect.width)), Math.max(0, Math.min(1, (event.clientY - rect.top) / rect.height)))
}
function pointerDown(event: PointerEvent, rack: NetworkRackSummary) {
  selectedRackId.value = rack.id
  if (!isAdmin.value || !floor.value || busy.value) return
  event.preventDefault()
  ;(event.currentTarget as HTMLElement).setPointerCapture(event.pointerId)
  dragRack = { id: rack.id, floorId: floor.value.id, pointer: event.pointerId, startX: rack.x, startY: rack.y, clientX: event.clientX, clientY: event.clientY, moved: false }
}
function pointerMove(event: PointerEvent, rack: NetworkRackSummary) {
  if (!dragRack || dragRack.pointer !== event.pointerId || dragRack.id !== rack.id || dragRack.floorId !== floor.value?.id || !mapEl.value) return
  const dx = event.clientX - dragRack.clientX, dy = event.clientY - dragRack.clientY
  if (!dragRack.moved && Math.hypot(dx, dy) < 4) return
  dragRack.moved = true
  const rect = mapEl.value.getBoundingClientRect()
  const nx = Math.max(0, Math.min(1, Math.round((dragRack.startX + dx / rect.width) * 10000) / 10000))
  const ny = Math.max(0, Math.min(1, Math.round((dragRack.startY + dy / rect.height) * 10000) / 10000))
  const idx = racks.value.findIndex(r => r.id === rack.id)
  if (idx >= 0) { racks.value[idx].x = nx; racks.value[idx].y = ny }
}
async function pointerUp(event: PointerEvent, rack: NetworkRackSummary, cancelled = false) {
  if (!dragRack || dragRack.pointer !== event.pointerId || dragRack.id !== rack.id) return
  const current = dragRack; dragRack = null
  if (current.moved) { suppressRackClickId = rack.id; suppressRackClickUntil = performance.now() + 250 }
  if (cancelled) {
    const idx = racks.value.findIndex(r => r.id === rack.id)
    if (idx >= 0) { racks.value[idx].x = current.startX; racks.value[idx].y = current.startY }
    return
  }
  if (!current.moved || current.floorId !== floor.value?.id) return
  await moveRack(current.id, rack.x, rack.y)
}
</script>

<template>
  <UDashboardPanel id="network-rack" :ui="{ body: 'flex min-h-0 flex-col p-4 sm:p-6' }">
    <template #header><PageHeader title="Network Rack" /></template>
    <template #body>
      <div class="ap-page-enter mx-auto flex min-h-0 w-full max-w-[1500px] flex-1 flex-col gap-5">
        <div class="ap-header-row flex flex-wrap items-center gap-x-4 gap-y-3 rounded-lg border border-default bg-elevated p-4">
          <div class="flex w-full min-w-0 items-center gap-3 xl:max-w-[320px]">
            <span class="grid size-12 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary ring-1 ring-inset ring-primary/20" aria-hidden="true">
              <RecordAddIcon size="lg" random-halo>
                <svg class="pc-add-tilt size-7" viewBox="0 0 512 512" fill="currentColor" focusable="false" aria-hidden="true">
                  <path d="M 25 457 L 25 487 L 155.2 487 L 175.2 457 Z M 196.8 457 L 176.8 487 L 335.2 487 L 315.2 457 Z M 336.8 457 L 356.8 487 L 487 487 L 487 457 Z" />
                  <path class="nr-rack-line nr-rack-line-1" d="M 41 25 L 41 103 L 471 103 L 471 25 Z M 295 48 L 313 48 L 313 80 L 295 80 Z M 416 48 A 16 16 0 0 1 432 64 A 16 16 0 0 1 416 80 A 16 16 0 0 1 400 64 A 16 16 0 0 1 416 48 Z M 64 55 L 112 55 L 112 73 L 64 73 Z M 144 55 L 192 55 L 192 73 L 144 73 Z M 224 55 L 272 55 L 272 73 L 224 73 Z M 105 121 L 105 151 L 407 151 L 407 121 Z" />
                  <path class="nr-rack-line nr-rack-line-2" d="M 41 169 L 41 247 L 471 247 L 471 169 Z M 295 192 L 313 192 L 313 224 L 295 224 Z M 416 192 A 16 16 0 0 1 432 208 A 16 16 0 0 1 416 224 A 16 16 0 0 1 400 208 A 16 16 0 0 1 416 192 Z M 64 199 L 112 199 L 112 217 L 64 217 Z M 144 199 L 192 199 L 192 217 L 144 217 Z M 224 199 L 272 199 L 272 217 L 224 217 Z M 105 265 L 105 295 L 407 295 L 407 265 Z" />
                  <path class="nr-rack-line nr-rack-line-3" d="M 41 313 L 41 391 L 471 391 L 471 313 Z M 295 336 L 313 336 L 313 368 L 295 368 Z M 416 336 A 16 16 0 0 1 432 352 A 16 16 0 0 1 416 368 A 16 16 0 0 1 400 352 A 16 16 0 0 1 416 336 Z M 64 343 L 112 343 L 112 361 L 64 361 Z M 144 343 L 192 343 L 192 361 L 144 361 Z M 224 343 L 272 343 L 272 361 L 224 361 Z M 237 409 L 237 439 L 275 439 L 275 409 Z" />
                </svg>
              </RecordAddIcon>
            </span>
            <div class="min-w-0">
              <h2 class="text-lg font-bold tracking-wide text-highlighted">Network Rack Map</h2>
              <p class="text-xs text-muted">{{ isAdmin ? 'Click the plan to add rack, Click rack for details.' : 'Select a rack to see its switches.' }}</p>
            </div>
          </div>
          <div v-if="floors.length" class="ap-floor-list flex w-full min-w-0 gap-2 overflow-x-auto" aria-label="Floor plans">
            <div v-for="item in floors" :key="item.id" class="ap-floor-tab relative w-[156px] shrink-0 rounded-lg border sm:w-[176px]"
              :class="activeFloorId === item.id ? 'border-primary/50 bg-primary/10 text-primary' : 'border-default bg-default/60 text-muted hover:border-primary/40 hover:text-highlighted'">
              <button type="button" class="flex h-12 w-full flex-col justify-center gap-0.5 rounded-lg py-1 pl-3 text-left focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
                :class="isAdmin ? 'pr-10' : 'pr-3'" :aria-pressed="activeFloorId === item.id" :disabled="busy" @click="loadFloor(item.id)">
                <span class="w-full truncate text-sm font-semibold leading-tight" :title="item.name">{{ item.name }}</span>
                <span class="text-[11px] leading-tight opacity-70">{{ item.rackCount }} rack{{ item.rackCount === 1 ? '' : 's' }}</span>
              </button>
              <UButton v-if="isAdmin" type="button" icon="i-lucide-trash-2" size="xs" color="neutral" variant="ghost"
                class="ap-plan-delete absolute right-1 top-1" :aria-label="`Delete floor plan ${item.name}`" :title="`Delete ${item.name}`"
                :disabled="busy" @click.stop="deleteFloor(item)" />
            </div>
          </div>
          <div v-if="isAdmin" class="ap-header-actions flex w-full shrink-0 items-center justify-end gap-2">
            <UButton v-if="floors.length" type="button" size="sm" icon="game-icons:server-rack" class="ap-rack-add-action record-add-hover stable-hover-hitbox" :disabled="busy || !floor" @click="openAddRack()">Add Rack</UButton>
            <UButton type="button" size="sm" icon="iconoven:floor-plan" variant="soft" class="ap-floor-add-action record-add-hover stable-hover-hitbox" :disabled="busy" @click="showUpload = true">Add floor plan</UButton>
          </div>
        </div>

        <p v-if="error" role="alert" class="rounded-lg border border-error/30 bg-error/10 px-3 py-2 text-sm text-error">{{ error }}</p>
        <div v-if="loading" class="flex items-center justify-center py-10 text-sm text-muted">
          <RegisterLoading label="Loading floor plans..." />
        </div>
        <UCard v-else-if="!floors.length" :ui="{ body: 'p-10 text-center' }">
          <div class="mx-auto grid size-14 place-items-center rounded-2xl bg-primary/10 text-primary"><UIcon name="i-lucide-server" class="size-7" /></div>
          <h3 class="mt-4 font-semibold text-highlighted">No floor plans yet</h3>
          <p class="mt-1 text-sm text-muted">{{ isAdmin ? 'Upload a plan to start mapping racks.' : 'An administrator can add the first floor plan.' }}</p>
        </UCard>
        <template v-else>
          <UCard :ui="{ body: 'flex min-h-0 flex-1 flex-col p-2 sm:p-3' }" class="flex min-h-0 min-w-0 flex-1 flex-col">
            <div v-if="floor && imageUrl" ref="mapStageEl" class="flex min-h-0 flex-1 items-center justify-center overflow-hidden">
              <div ref="mapEl" :style="mapFitStyle" class="relative shrink-0 overflow-hidden rounded-lg border border-default bg-elevated" :class="isAdmin ? 'cursor-crosshair' : ''" @click="placeOnMap">
                <img :src="imageUrl" :alt="`Floor plan: ${floor.name}`" draggable="false" class="block h-full w-full select-none" @load="onMapImageLoad" />
                <button v-for="rack in racks" :key="rack.id" type="button"
                  class="ap-rack-marker absolute -translate-x-1/2 -translate-y-1/2 transition-[box-shadow] focus-visible:outline-2"
                  :class="[selectedRackId === rack.id ? 'ring-4 ring-primary/40' : 'hover:ring-4 hover:ring-primary/30', isAdmin ? 'cursor-grab active:cursor-grabbing touch-none' : 'cursor-pointer']"
                  :style="{ left: `${rack.x * 100}%`, top: `${rack.y * 100}%` }"
                  :aria-label="`Rack ${rack.name}${rack.description ? `, ${rack.description}` : ''}, ${rack.switchCount} switches`"
                  :title="rack.name" @click.stop="openRackDetails(rack)" @pointerdown.stop="pointerDown($event, rack)" @pointermove="pointerMove($event, rack)" @pointerup="pointerUp($event, rack)" @pointercancel="pointerUp($event, rack, true)">
                  <span class="ap-rack-body relative grid size-12 place-items-center rounded-xl border-2 border-primary bg-elevated shadow-md">
                    <UIcon name="game-icons:server-rack" class="size-7 text-primary" />
                  </span>
                </button>
              </div>
            </div>
            <div v-else class="flex min-h-0 flex-1 items-center justify-center p-3 text-sm text-muted">
              <RegisterLoading label="Loading floor-plan image..." />
            </div>
          </UCard>
        </template>
      </div>

      <!-- Upload floor plan modal (same as Access Point) -->
      <UModal v-if="isAdmin" v-model:open="showUpload" title="Add floor plan" description="Upload an image and give this map a clear name." :dismissible="!busy" :close="!busy" :ui="{ content: 'max-w-lg ap-upload-modal', header: 'relative z-10', body: 'relative z-10 p-4 sm:p-5' }">
        <template #body>
          <form class="space-y-4" @submit.prevent="createFloor">
            <div class="flex items-start gap-3 rounded-xl border border-primary/20 bg-primary/5 p-3 text-sm text-muted">
              <span class="ap-modal-icon grid size-9 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary"><UIcon name="i-lucide-upload" class="ap-upload-glyph size-5" /></span>
              <p>Set floor name and floor layout, then place racks directly on the plan.</p>
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

      <!-- Rack editor modal -->
      <UModal v-if="isAdmin" v-model:open="showRackEditor" :title="editingRackId === null ? 'Add rack' : 'Edit rack'" :description="editingRackId === null ? 'Place a network rack on this floor plan.' : 'Update this rack’s details.'" :dismissible="!busy" :close="!busy" :ui="{ content: 'max-w-lg', body: 'p-4 sm:p-5' }">
        <template #body>
          <form class="space-y-4" @submit.prevent="saveRack">
            <div class="flex items-center gap-3 rounded-xl border border-primary/20 bg-primary/5 p-3">
              <span class="ap-modal-icon grid size-10 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary"><UIcon name="game-icons:server-rack" class="size-5" /></span>
              <div class="min-w-0"><p class="text-sm font-medium text-highlighted">{{ floor?.name }}</p><p class="text-xs text-muted">Position: {{ Math.round(rackDraft.x * 100) }}% across · {{ Math.round(rackDraft.y * 100) }}% down</p></div>
            </div>
            <fieldset :disabled="busy">
              <legend class="mb-1.5 text-sm font-medium text-highlighted">Rack name</legend>
              <UInput v-model="rackDraft.name" class="w-full" maxlength="60" placeholder="e.g. Rack-A1" :disabled="busy" />
            </fieldset>
            <fieldset :disabled="busy">
              <legend class="mb-1.5 text-sm font-medium text-highlighted">Description (optional)</legend>
              <UInput v-model="rackDraft.description" class="w-full" maxlength="300" placeholder="e.g. Core switch rack, 42U" :disabled="busy" />
            </fieldset>
            <p class="text-xs text-muted">You can drag the rack on the plan to adjust its position later.</p>
            <p role="alert" class="min-h-5 text-sm text-error">{{ rackError }}</p>
            <div class="flex justify-end gap-2 border-t border-default pt-4">
              <UButton type="button" color="red" variant="ghost" icon="basil:cancel-outline" class="cancel-hover" :disabled="busy" @click="showRackEditor = false">Cancel</UButton>
              <UButton type="submit" :icon="editingRackId === null ? 'i-lucide-plus' : 'i-lucide-pencil'" :class="editingRackId === null ? 'record-add-hover stable-hover-hitbox' : 'record-save-hover stable-hover-hitbox'" :loading="busy" :disabled="busy">{{ editingRackId === null ? 'Add Rack' : 'Save changes' }}</UButton>
            </div>
          </form>
        </template>
      </UModal>

      <!-- Switch editor modal -->
      <UModal v-if="isAdmin" v-model:open="showSwitchEditor" :title="editingSwitchId === null ? 'Add switch' : 'Edit switch'" :description="editingSwitchId === null ? 'Add a switch to this rack.' : 'Update this switch’s details.'" :dismissible="!busy" :close="!busy" :ui="{ content: 'max-w-lg', body: 'p-4 sm:p-5' }">
        <template #body>
          <form class="space-y-4" @submit.prevent="saveSwitch">
            <div class="flex items-center gap-3 rounded-xl border border-primary/20 bg-primary/5 p-3">
              <span class="ap-modal-icon grid size-10 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary"><UIcon name="i-lucide-cpu" class="size-5" /></span>
              <div class="min-w-0"><p class="text-sm font-medium text-highlighted">{{ selectedRackDetail?.name }}</p><p class="text-xs text-muted">Rack on {{ floor?.name }}</p></div>
            </div>
            <fieldset :disabled="busy">
              <legend class="mb-1.5 text-sm font-medium text-highlighted">IP address</legend>
              <UInput v-model="switchDraft.ipAddress" class="w-full" maxlength="45" placeholder="e.g. 192.168.1.1" :disabled="busy" />
            </fieldset>
            <fieldset :disabled="busy">
              <legend class="mb-1.5 text-sm font-medium text-highlighted">Hostname (optional)</legend>
              <UInput v-model="switchDraft.hostname" class="w-full" maxlength="160" placeholder="e.g. SW-CORE-01" :disabled="busy" />
            </fieldset>
            <fieldset :disabled="busy">
              <legend class="mb-1.5 text-sm font-medium text-highlighted">Position in rack (optional)</legend>
              <UInput v-model="switchDraft.position" class="w-full" maxlength="100" placeholder="e.g. U1-U4" :disabled="busy" />
            </fieldset>
            <p role="alert" class="min-h-5 text-sm text-error">{{ switchError }}</p>
            <div class="flex justify-end gap-2 border-t border-default pt-4">
              <UButton type="button" color="red" variant="ghost" icon="basil:cancel-outline" class="cancel-hover" :disabled="busy" @click="showSwitchEditor = false">Cancel</UButton>
              <UButton type="submit" :icon="editingSwitchId === null ? 'i-lucide-plus' : 'i-lucide-pencil'" :class="editingSwitchId === null ? 'record-add-hover stable-hover-hitbox' : 'record-save-hover stable-hover-hitbox'" :loading="busy" :disabled="busy">{{ editingSwitchId === null ? 'Add Switch' : 'Save changes' }}</UButton>
            </div>
          </form>
        </template>
      </UModal>

      <!-- Rack details modal -->
      <UModal v-model:open="showRackDetails" title="Rack details" description="Selected rack on this floor plan." :dismissible="!busy" :close="!busy" :ui="{ content: 'max-w-md', body: 'p-4 sm:p-5' }">
        <template #body>
          <div v-if="selectedRackDetail" class="space-y-4">
            <div class="flex items-center gap-3">
              <span class="ap-modal-icon grid size-12 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary ring-1 ring-inset ring-primary/20"><UIcon name="game-icons:server-rack" class="size-6" /></span>
              <div class="min-w-0"><h3 class="truncate text-base font-semibold text-highlighted">{{ selectedRackDetail.name }}</h3><p class="truncate text-xs text-muted">{{ floor?.name }}</p></div>
            </div>
            <div v-if="selectedRackDetail.description" class="text-sm text-highlighted">{{ selectedRackDetail.description }}</div>
            <dl class="divide-y divide-default overflow-hidden rounded-xl border border-default bg-elevated/40 text-sm">
              <div class="grid grid-cols-[105px_minmax(0,1fr)] gap-3 px-3 py-3"><dt class="text-muted">Switches</dt><dd class="font-semibold text-primary">{{ selectedRackDetail.switchCount }}</dd></div>
              <div class="grid grid-cols-[105px_minmax(0,1fr)] gap-3 px-3 py-3"><dt class="text-muted">Position</dt><dd class="text-highlighted">{{ Math.round(selectedRackDetail.x * 100) }}% across · {{ Math.round(selectedRackDetail.y * 100) }}% down</dd></div>
            </dl>
            <h4 class="text-sm font-semibold text-highlighted">Switches in this rack</h4>
            <div v-if="selectedRackDetail.switches.length === 0" class="text-sm text-muted text-center py-4">No switches yet</div>
            <ul v-else class="space-y-2">
              <li v-for="sw in selectedRackDetail.switches" :key="sw.id" class="flex items-center gap-3 rounded-lg border border-default bg-elevated/40 p-3 text-sm">
                <UIcon name="i-lucide-cpu" class="size-4 text-primary shrink-0" />
                <div class="min-w-0 flex-1">
                  <p class="font-mono text-highlighted truncate">{{ sw.ipAddress }}</p>
                  <p v-if="sw.hostname" class="truncate text-xs text-muted">{{ sw.hostname }}</p>
                  <p v-if="sw.position" class="truncate text-xs text-muted">U-position: {{ sw.position }}</p>
                </div>
                <template v-if="isAdmin">
                  <UButton type="button" size="xs" icon="i-lucide-pencil" variant="ghost" class="shrink-0" :disabled="busy" @click="openEditSwitch(sw)" />
                  <UButton type="button" size="xs" icon="i-lucide-trash-2" color="error" variant="ghost" class="shrink-0" :disabled="busy" @click="deleteSwitch(sw)" />
                </template>
              </li>
            </ul>
            <p v-if="isAdmin" role="alert" class="min-h-5 text-sm text-error">{{ detailError }}</p>
            <div class="flex justify-end gap-2 border-t border-default pt-4">
              <UButton type="button" color="neutral" variant="ghost" icon="i-lucide-x" class="ap-close-action" :disabled="busy" @click="closeRackDetails">Close</UButton>
              <template v-if="isAdmin">
                <UButton type="button" icon="i-lucide-plus" variant="soft" class="record-add-hover stable-hover-hitbox" :disabled="busy" @click="openAddSwitch">Add Switch</UButton>
                <UButton type="button" icon="i-lucide-pencil" variant="soft" class="record-add-hover stable-hover-hitbox" :disabled="busy" @click="openEditRack(selectedRackDetail)">Edit Rack</UButton>
                <UButton type="button" icon="i-lucide-trash-2" color="error" variant="soft" class="record-delete-hover ap-stable-action" :disabled="busy" @click="deleteRack(selectedRackDetail)">Delete Rack</UButton>
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
.ap-file-trigger { cursor: pointer; transition: background-color 180ms ease, border-color 180ms ease, box-shadow 180ms ease; }
.ap-file-trigger :deep(.ap-choose-icon) { transition: transform 180ms ease; }
.ap-file-trigger.ap-file-disabled { cursor: not-allowed; opacity: .5; }
.ap-file-input:focus-visible + .ap-file-trigger { outline: 2px solid var(--ui-primary); outline-offset: 2px; }
@media (hover: hover) and (pointer: fine) {
  .ap-file-trigger:not(.ap-file-disabled):hover { background-color: color-mix(in srgb, var(--ui-primary) 18%, transparent); border-color: var(--ui-primary); box-shadow: 0 3px 10px color-mix(in srgb, var(--ui-primary) 12%, transparent); }
  .ap-file-trigger:not(.ap-file-disabled):hover :deep(.ap-choose-icon) { transform: translate3d(0, -2px, 0); }
}
@media (prefers-reduced-motion: reduce) {
  .ap-modal-icon-active :deep(.ap-upload-glyph) { animation: none; }
  .ap-file-trigger, .ap-file-trigger :deep(.ap-choose-icon) { transition: none; transform: none !important; }
}

@keyframes ap-rack-lift {
  0%, 100% { transform: translate3d(0px, 0px, 0px) rotate(0deg); }
  45% { transform: translate3d(0px, -3px, 0px) rotate(-6deg); }
  75% { transform: translate3d(0px, -1px, 0px) rotate(3deg); }
}
@keyframes ap-floor-lift {
  0%, 100% { transform: translate3d(0px, 0px, 0px) rotate(0deg); }
  45% { transform: translate3d(0px, -3px, 0px) rotate(-8deg); }
  75% { transform: translate3d(0px, -1px, 0px) rotate(3deg); }
}
.ap-rack-add-action.record-add-hover:not(:disabled):focus-visible :deep([data-slot='leadingIcon']) { animation: ap-rack-lift 650ms ease-in-out; }
.ap-floor-add-action.record-add-hover:not(:disabled):focus-visible :deep([data-slot='leadingIcon']) { animation: ap-floor-lift 650ms ease-in-out; }
@media (hover: hover) and (pointer: fine) {
  .ap-rack-add-action.record-add-hover:not(:disabled):hover :deep([data-slot='leadingIcon']) { animation: ap-rack-lift 650ms ease-in-out; }
  .ap-floor-add-action.record-add-hover:not(:disabled):hover :deep([data-slot='leadingIcon']) { animation: ap-floor-lift 650ms ease-in-out; }
}
@media (prefers-reduced-motion: reduce) {
  .ap-rack-add-action :deep([data-slot='leadingIcon']), .ap-floor-add-action :deep([data-slot='leadingIcon']) { animation: none !important; }
}

@media (min-width: 1280px) {
  .ap-header-row { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 405px) minmax(0, 1fr); }
  .ap-header-actions { grid-column: 3; }
}
.ap-floor-list { justify-content: safe center; scrollbar-width: none; }
.ap-floor-list::-webkit-scrollbar { display: none; }
.ap-floor-tab { transition: border-color 180ms ease, background-color 180ms ease, box-shadow 180ms ease; }
.ap-plan-delete { color: var(--ui-text-muted); transition: color 180ms ease, background-color 180ms ease; }
.ap-plan-delete:focus-visible { color: var(--ui-error); outline: 2px solid var(--ui-error); outline-offset: 1px; }
.ap-close-action :deep([data-slot='leadingIcon']) { transition: transform 220ms cubic-bezier(.2,.8,.2,1); }
.ap-rack-marker { transition: box-shadow 200ms ease; }
.ap-modal-icon-active { animation: ap-icon-arrive 420ms cubic-bezier(.2,.8,.2,1) both; }
@keyframes ap-icon-arrive {
  from { transform: scale(.88) rotate(-9deg); opacity: .55; }
  to { transform: scale(1) rotate(0); opacity: 1; }
}
@media (hover: hover) and (pointer: fine) {
  .ap-floor-tab:hover { box-shadow: 0 3px 10px color-mix(in srgb, var(--ui-primary) 14%, transparent); }
  .ap-plan-delete:not(:disabled):hover { color: var(--ui-error); background-color: color-mix(in srgb, var(--ui-error) 12%, transparent); }
  .ap-close-action:not(:disabled):hover :deep([data-slot='leadingIcon']) { transform: scale(1.14) rotate(12deg); }
  .ap-rack-marker:hover:not(:active) { box-shadow: 0 4px 16px color-mix(in srgb, var(--ui-primary) 35%, transparent); }
}
.ap-rack-marker:focus-visible { box-shadow: 0 4px 16px color-mix(in srgb, var(--ui-primary) 35%, transparent); }
.ap-stable-action.record-delete-hover:not(:disabled):not([aria-disabled='true']):is(:hover, :focus-visible) { transform: none; }
@media (prefers-reduced-motion: reduce) {
  .ap-floor-tab, .ap-plan-delete, .ap-close-action :deep([data-slot='leadingIcon']), .ap-rack-marker { transition: none; transform: none !important; }
  .ap-modal-icon-active { animation: none; }
}

/* Rack marker visual */
.ap-rack-body { transition: transform 200ms ease-out; }
@media (hover: hover) and (pointer: fine) {
  .ap-rack-marker:hover:not(:active) .ap-rack-body { transform: scale(1.1); }
}
.ap-rack-marker:focus-visible .ap-rack-body { transform: scale(1.1); }

/* Rack units light up in sequence: band 1 -> 2 -> 3 all lit, then back to band 1.
   Geometry is the exact game-icons:server-rack body (same as the sidebar icon),
   split into its three unit bands so each band can fade on its own. */
.nr-rack-line { animation: nr-rack-line-1 2.7s ease-in-out infinite; }
.nr-rack-line-2 { animation-name: nr-rack-line-2; }
.nr-rack-line-3 { animation-name: nr-rack-line-3; }
@keyframes nr-rack-line-1 {
  0%, 10% { opacity: .12; }
  20%, 85% { opacity: 1; }
  95%, 100% { opacity: .12; }
}
@keyframes nr-rack-line-2 {
  0%, 32% { opacity: .12; }
  42%, 85% { opacity: 1; }
  95%, 100% { opacity: .12; }
}
@keyframes nr-rack-line-3 {
  0%, 54% { opacity: .12; }
  64%, 85% { opacity: 1; }
  95%, 100% { opacity: .12; }
}
@media (prefers-reduced-motion: reduce) {
  .ap-rack-body, .nr-rack-line { transition: none; transform: none !important; animation: none; opacity: 1; }
}

/* Page load: the whole body eases in once (fade + 10px rise) instead of popping. */
.ap-page-enter { animation: ap-page-arrive 420ms cubic-bezier(.2,.8,.2,1) both; }
@keyframes ap-page-arrive {
  from { opacity: 0; transform: translate3d(0, 10px, 0); }
  to { opacity: 1; transform: translate3d(0, 0, 0); }
}
@media (prefers-reduced-motion: reduce) {
  .ap-page-enter { animation: none; }
}
</style>

<style>
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