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
const editingRackId = ref<number | null>(null)
const editingSwitchId = ref<number | null>(null)
// Cabinet heights the add-rack form offers. U_HEIGHT matches the 3D model (1.75 in per U) and
// the plinth + roof add 0.16 m, so the shown metre value is the height the user will actually see.
const RACK_SIZES = [19, 22, 27, 42] as const
const U_HEIGHT_M = 0.04445
const rackSizeHeight = (u: number) => `${(u * U_HEIGHT_M + 0.16).toFixed(2)} m`
const rackDraft = reactive<NetworkRackInput>({ name: '', x: 0.5, y: 0.5, sizeU: 42 })
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
const replacingId = ref<number | null>(null)

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

// Switching floors used to wait for a fresh blob download, and on a cold remote SQL Server that first
// read is the slow one (the API answers warm in a few ms, MISS reads take seconds). Warm the remaining
// plans of this page one per idle slot so switching tabs is instant; each blob lands in floorImages
// (max 3 kept), which getFloorImage already serves from.
function prefetchFloorImages() {
  const pending = floors.value.map(f => f.id).filter(id => !floorImages.has(id) && !pendingFloorImages.has(id))
  const step = () => {
    const id = pending.shift()
    if (id === undefined || disposed) return
    getFloorImage(id)
      .catch(() => {})
      .finally(() => {
        if (typeof requestIdleCallback === 'function') requestIdleCallback(step, { timeout: 4000 })
        else setTimeout(step, 500)
      })
  }
  if (typeof requestIdleCallback === 'function') requestIdleCallback(step, { timeout: 4000 })
  else setTimeout(step, 800)
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
// The detail request is a separate round trip, and the remote SQL host can take a while. Without
// these two the dialog kept painting the PREVIOUS rack (header, LED seed and the whole 3D canvas)
// until the new payload landed, so switching racks flashed the old cabinet first.
const rackDetailLoading = ref(false)
let rackDetailVersion = 0

// Opening a rack used to feel slow for two reasons that had nothing to do with the API (which answers
// in ~15 ms warm): the dialog is a LazyRack3DView, so Vue could not even mount it until the three.js
// chunk had downloaded and parsed (~380 ms measured), and every open re-fetched the detail. Both are
// one-time costs, so pay them off the critical path: warm the chunk once the floor is on screen, and
// keep the last detail per rack so a re-open paints immediately while a silent refresh runs.
const rackDetailCache = new Map<number, NetworkRackDetail>()
// The header only needs name / sizeU / id, all of which the rack SUMMARY already carries. Keeping the
// clicked summary here lets the dialog open fully labelled while the detail request is in flight, so
// the only loading state the user ever sees is the 3D view's own "Initializing ..." screen.
const pendingRack = ref<NetworkRackSummary | null>(null)
const headerRack = computed(() => selectedRackDetail.value ?? pendingRack.value)
let rack3dWarmed = false
function warmRack3D() {
  if (rack3dWarmed) return
  rack3dWarmed = true
  const run = () => { import('~/components/Rack3DView.vue').catch(() => { rack3dWarmed = false }) }
  if (typeof requestIdleCallback === 'function') requestIdleCallback(run, { timeout: 2500 })
  else setTimeout(run, 400)
}
function prefetchRackDetail(rack: { id: number }) {
  if (rackDetailCache.has(rack.id) || !floor.value) return
  apiGetNetworkRack(floor.value.id, rack.id).then(d => rackDetailCache.set(rack.id, d)).catch(() => {})
}
// The dev SQL Server sits on another subnet and goes cold after a quiet spell: the API log shows the
// first queries of a session taking 4.8-5.1 s while warm ones answer in 4-16 ms. Paying that ahead of
// the click instead of inside it is the difference between "Initializing..." for five seconds and an
// instant open, so walk the floor's racks once, one request per idle slot.
function scheduleRackDetailPrefetch() {
  const pending = racks.value.map(r => r.id).filter(id => !rackDetailCache.has(id))
  const step = () => {
    const id = pending.shift()
    if (id === undefined || !floor.value) return
    const floorId = floor.value.id
    apiGetNetworkRack(floorId, id)
      .then(d => rackDetailCache.set(id, d))
      .catch(() => {})
      .finally(() => {
        if (typeof requestIdleCallback === 'function') requestIdleCallback(step, { timeout: 4000 })
        else setTimeout(step, 500)
      })
  }
  if (typeof requestIdleCallback === 'function') requestIdleCallback(step, { timeout: 4000 })
  else setTimeout(step, 800)
}

// One delete dialog for every destructive action on this page (a switch, a floor plan). It is a
// request object rather than a boolean so the dialog can name the row that is about to vanish
// before any API call happens - a destructive confirmation that cannot show WHICH record it is
// about to destroy is just a speed bump.
type DeleteRequest =
  | { kind: 'switch'; sw: NetworkSwitch }
  | { kind: 'floor'; plan: FloorMapSummary }
  | { kind: 'rack'; rack: NetworkRackSummary }
const deleteRequest = ref<DeleteRequest | null>(null)
const showDeleteDialog = ref(false)
const deleting = ref(false)

const deleteDialog = computed(() => {
  const req = deleteRequest.value
  if (!req) {
    return { title: '', description: '', action: 'Delete', rows: [] as { label: string; value: string; mono?: boolean }[] }
  }
  if (req.kind === 'switch') {
    return {
      title: 'Delete this switch?',
      description: 'The switch leaves the rack register and is removed from the 3D view. This cannot be undone from the UI.',
      action: 'Delete switch',
      rows: [
        { label: 'Hostname', value: req.sw.hostname || '-', mono: true },
        { label: 'IP address', value: req.sw.ipAddress || '-', mono: true },
        { label: 'Rack', value: selectedRackDetail.value?.name ?? '-' },
        { label: 'U slot', value: req.sw.position?.trim() || 'Auto', mono: true }
      ] as { label: string; value: string; mono?: boolean }[]
    }
  }
  if (req.kind === 'rack') {
    return {
      title: 'Delete this rack?',
      description: 'The rack and every switch mounted in it are removed from this floor plan. This cannot be undone from the UI.',
      action: 'Delete rack',
      rows: [
        { label: 'Rack', value: req.rack.name },
        { label: 'Floor plan', value: floor.value?.name ?? '-' },
        { label: 'Switches', value: String(req.rack.switchCount) }
      ] as { label: string; value: string; mono?: boolean }[]
    }
  }
  return {
    title: 'Delete this floor plan?',
    description: req.plan.scope === 'both'
      ? 'This removes the plan from Network Rack together with every rack and switch on it. The Access Point page keeps the same plan and its markers.'
      : 'The plan, every rack mapped on it and every switch inside those racks are removed together. This cannot be undone from the UI.',
    action: 'Delete floor plan',
    rows: [
      { label: 'Floor plan', value: req.plan.name },
      { label: 'Racks on it', value: String(req.plan.rackCount) }
    ] as { label: string; value: string; mono?: boolean }[]
  }
})
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
  rackDetailCache.clear()          // details belong to this floor's racks
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
    warmRack3D()
    scheduleRackDetailPrefetch()
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
    floors.value = await apiListNetworkFloors('rack')
    const next = preferFloorId && floors.value.some(f => f.id === preferFloorId) ? preferFloorId
      : floors.value.some(f => f.id === activeFloorId.value) ? activeFloorId.value! : floors.value[0]?.id
    if (next) { await loadFloor(next); prefetchFloorImages() }
    else { ++loadVersion; activeFloorId.value = null; floor.value = null; setImageUrl('') }
  } catch (e) { error.value = message(e) }
  finally { loading.value = false }
}
onMounted(() => { void refresh() })
onBeforeUnmount(() => { disposed = true; ++loadVersion; floorImages.clear(); pendingFloorImages.clear(); mapResizeObserver?.disconnect(); setImageUrl(''); setUploadPreviewUrl(''); })

function pickImage(event: Event) {
  uploadFile.value = (event.target as HTMLInputElement).files?.[0] ?? null
  uploadError.value = ''
  setUploadPreviewUrl(uploadFile.value && ['image/png', 'image/jpeg', 'image/webp'].includes(uploadFile.value.type)
    ? URL.createObjectURL(uploadFile.value) : '')
}
function openUpload() {
  if (!isAdmin.value || busy.value) return
  replacingId.value = null
  uploadName.value = ''
  uploadFile.value = null
  setUploadPreviewUrl('')
  uploadError.value = ''
  showUpload.value = true
}
function openReplace(item: { id: number; name: string } | null) {
  if (!isAdmin.value || !item || busy.value) return
  replacingId.value = item.id
  uploadName.value = item.name
  uploadFile.value = null
  setUploadPreviewUrl('')
  uploadError.value = ''
  showUpload.value = true
}
async function createFloor() {
  if (!isAdmin.value || busy.value) return
  // When replacing, the floor already has a name and an image; both are optional now.
  if (replacingId.value === null && !uploadFile.value) { uploadError.value = 'Enter a floor name and choose a plan image.'; return }
  if (!uploadName.value.trim()) { uploadError.value = 'Enter a floor name.'; return }
  if (uploadFile.value && uploadFile.value.size > 8 * 1024 * 1024) { uploadError.value = 'The image must be smaller than 8 MB.'; return }
  const uploadedFile = uploadFile.value
  const uploadedName = uploadName.value.trim()
  const replaceTarget = replacingId.value
  busy.value = true; uploadError.value = ''
  try {
    if (replaceTarget !== null) {
      await apiUpdateNetworkFloor(replaceTarget, uploadedName, uploadedFile)
      // The cached blob is the old picture; drop it so loadFloor fetches the new one.
      floorImages.delete(replaceTarget)
      pendingFloorImages.delete(replaceTarget)
      showUpload.value = false
      replacingId.value = null
      await refresh(replaceTarget)
      toast.add({
        title: 'Floor plan updated',
        description: uploadedFile ? 'Racks keep their relative positions - drag any rack to realign it.' : undefined,
        color: 'primary',
        duration: 6000
      })
    } else {
      const created = await apiCreateNetworkFloor(uploadedName, uploadedFile!, 'rack')
      floorImages.set(created.id, uploadedFile!)
      if (floorImages.size > 3) floorImages.delete(floorImages.keys().next().value!)
      showUpload.value = false
      await refresh(created.id)
      toast.add({ title: 'Floor plan added', color: 'primary' })
    }
  } catch (e) { uploadError.value = message(e) }
  finally { busy.value = false }
}
function deleteFloor(item: FloorMapSummary) {
  if (!isAdmin.value || busy.value) return
  deleteRequest.value = { kind: 'floor', plan: item }
  showDeleteDialog.value = true
}
async function performDeleteFloor(item: FloorMapSummary) {
  busy.value = true; error.value = ''
  try {
    await apiDeleteNetworkFloor(item.id, 'rack')
    floorImages.delete(item.id)
    if (activeFloorId.value === item.id) activeFloorId.value = null
    await refresh()
    toast.add({ title: 'Floor plan deleted', color: 'primary' })
  } catch (e) { error.value = message(e) }
  finally { busy.value = false }
}
const floorListEl = ref<HTMLElement | null>(null)
// The strip is a hidden-scrollbar row; it is panned with the mouse like a filmstrip.
const floorPan = reactive({ active: false, pointerId: -1, startX: 0, startLeft: 0, moved: 0 })
function startFloorPan(event: PointerEvent) {
  const el = floorListEl.value
  if (!el || event.button !== 0 || event.pointerType === 'touch') return
  if (el.scrollWidth <= el.clientWidth + 2) return
  floorPan.active = true
  floorPan.pointerId = event.pointerId
  floorPan.startX = event.clientX
  floorPan.startLeft = el.scrollLeft
  floorPan.moved = 0
  window.addEventListener('pointermove', moveFloorPan)
  window.addEventListener('pointerup', endFloorPan)
  window.addEventListener('pointercancel', endFloorPan)
}
function moveFloorPan(event: PointerEvent) {
  const el = floorListEl.value
  if (!floorPan.active || !el || event.pointerId !== floorPan.pointerId) return
  const dx = event.clientX - floorPan.startX
  if (Math.abs(dx) > floorPan.moved) floorPan.moved = Math.abs(dx)
  if (floorPan.moved <= 3) return
  // Stop the browser from starting its own drag on the tab contents.
  event.preventDefault()
  el.scrollLeft = floorPan.startLeft - dx
}
function endFloorPan() {
  if (!floorPan.active) return
  floorPan.active = false
  window.removeEventListener('pointermove', moveFloorPan)
  window.removeEventListener('pointerup', endFloorPan)
  window.removeEventListener('pointercancel', endFloorPan)
  // Let the flag survive the click that follows the pointerup, then clear it.
  setTimeout(() => { floorPan.moved = 0 }, 0)
}
const floorNav = reactive({ left: false, right: false })
function syncFloorNav() {
  const el = floorListEl.value
  if (!el) return
  floorNav.left = el.scrollLeft > 4
  floorNav.right = el.scrollLeft + el.clientWidth < el.scrollWidth - 4
}
function scrollFloors(dir: number) {
  const el = floorListEl.value
  if (!el) return
  el.scrollBy({ left: dir * Math.max(176, el.clientWidth * 0.7), behavior: 'smooth' })
}
watch([floorListEl, floors, activeFloorId], async () => { await nextTick(); syncFloorNav() }, { deep: false })
if (typeof window !== 'undefined') window.addEventListener('resize', syncFloorNav)
// A drag must not also select the floor it ended on.
function selectFloor(id: number) {
  if (floorPan.moved > 3) return
  void loadFloor(id)
}
function setUploadPreviewUrl(url: string) { if (uploadPreviewUrl.value) URL.revokeObjectURL(uploadPreviewUrl.value); uploadPreviewUrl.value = url }
function openAddRack(x = 0.5, y = 0.5) {
  if (!isAdmin.value || !floor.value || busy.value) return
  showRackDetails.value = false
  editingRackId.value = null
  Object.assign(rackDraft, { name: '', x, y, sizeU: 42 })
  rackError.value = ''; showRackEditor.value = true
}
function openEditRack(rack: NetworkRackSummary) {
  if (!isAdmin.value || busy.value) return
  showRackDetails.value = false
  editingRackId.value = rack.id
  Object.assign(rackDraft, { name: rack.name, x: rack.x, y: rack.y, sizeU: rack.sizeU ?? 42 })
  rackError.value = ''; showRackEditor.value = true
}
async function saveRack() {
  if (!isAdmin.value || !floor.value || busy.value) return
  if (!rackDraft.name.trim()) { rackError.value = 'Rack name is required.'; return }
  const floorId = floor.value.id
  busy.value = true; rackError.value = ''
  try {
    const payload = { ...rackDraft, name: rackDraft.name.trim() }
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
function deleteRack(rack: NetworkRackSummary) {
  if (!isAdmin.value || busy.value) return
  // The rack DETAIL payload carries `switches` but no `switchCount` (the list endpoint is the one
  // that sends the count), so the dialog would otherwise print "undefined" for the switch row.
  const count = (rack as NetworkRackDetail).switches?.length ?? rack.switchCount ?? 0
  deleteRequest.value = { kind: 'rack', rack: { ...rack, switchCount: count } }
  showDeleteDialog.value = true
}
async function performDeleteRack(rack: NetworkRackSummary) {
  busy.value = true; error.value = ''
  try {
    await apiDeleteNetworkRack(floor.value!.id, rack.id)
    racks.value = racks.value.filter(r => r.id !== rack.id)
    rackDetailCache.delete(rack.id)
    if (selectedRackId.value === rack.id) {
      selectedRackId.value = null
      selectedRackDetail.value = null
      showRackDetails.value = false
    }
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
  warmRack3D()
  // Drop the previous rack BEFORE the fetch: the dialog is already mounted, so keeping the old
  // object renders its cabinet and LEDs for as long as the request takes.
  pendingRack.value = rack
  const cached = rackDetailCache.get(rack.id) ?? null
  selectedRackDetail.value = cached
  rackDetailLoading.value = cached === null
  loadRackDetail(rack.id)                                  // silent refresh even when cached
}
async function loadRackDetail(rackId: number) {
  const version = ++rackDetailVersion
  try {
    const detail = await apiGetNetworkRack(floor.value!.id, rackId)
    rackDetailCache.set(rackId, detail)
    if (version !== rackDetailVersion) return            // a newer rack was opened meanwhile
    selectedRackDetail.value = detail
  } catch (e) {
    if (version === rackDetailVersion && !selectedRackDetail.value) toast.add({ title: 'Could not load rack details', description: message(e), color: 'error' })
  } finally {
    if (version === rackDetailVersion) rackDetailLoading.value = false
  }
}
function closeRackDetails() {
  pendingRack.value = null
  rackDetailVersion++
  rackDetailLoading.value = false
  showRackDetails.value = false
  selectedRackId.value = null
  selectedRackDetail.value = null
}

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
function deleteSwitch(sw: NetworkSwitch) {
  if (!isAdmin.value || !selectedRackDetail.value || busy.value) return
  deleteRequest.value = { kind: 'switch', sw }
  showDeleteDialog.value = true
}
async function performDeleteSwitch(sw: NetworkSwitch) {
  if (!isAdmin.value || !selectedRackDetail.value || busy.value) return
  busy.value = true
  try {
    await apiDeleteNetworkSwitch(floor.value!.id, selectedRackDetail.value.id, sw.id)
    if (selectedRackDetail.value) {
      selectedRackDetail.value.switches = selectedRackDetail.value.switches.filter(s => s.id !== sw.id)
      selectedRackDetail.value.switchCount = selectedRackDetail.value.switches.length
    }
    const rackIdx = racks.value.findIndex(r => r.id === selectedRackDetail.value?.id)
    if (rackIdx >= 0) racks.value[rackIdx].switchCount = selectedRackDetail.value?.switchCount ?? 0
    toast.add({ title: 'Switch deleted', color: 'primary' })
  } catch (e) { toast.add({ title: 'Could not load rack details', description: message(e), color: 'error' }) }
  finally { busy.value = false }
}

// The 3D HUD mirrors the rack detail, so a delete it starts has to go through the same API path
// as the register's own delete button - otherwise the row survives the modal being closed.
// The 3D panel's own settings (UPS, door, toggles) are stored on the rack so reopening the modal
// does not reset them. Kept silent on purpose: these are small background saves, not user actions
// that deserve a toast.
async function saveHudSettingsFromRack3D(settings: string) {
  if (!isAdmin.value || !floor.value || !selectedRackDetail.value) return
  const floorId = floor.value.id
  const rackId = selectedRackDetail.value.id
  try {
    await apiSaveRackHudSettings(floorId, rackId, settings)
    // Keep the local copy in step so a reopen reads what was just saved without another GET
    selectedRackDetail.value = { ...selectedRackDetail.value, hudSettings: settings }
  } catch (e) {
    toast.add({ title: message(e), color: 'error' })
  }
}

// The 3D panel never writes to the database itself: it asks the page, which owns the API call and
// the refreshed rack detail. Without this the added/edited switch only existed in the canvas and
// disappeared on the next modal open.
async function saveSwitchFromRack3D(payload: { id: number | null; ipAddress: string; hostname: string; position: string }) {
  if (!isAdmin.value || !floor.value || !selectedRackDetail.value) return
  if (busy.value) return
  const floorId = floor.value.id
  const rackId = selectedRackDetail.value.id
  busy.value = true
  try {
    const body = { ipAddress: payload.ipAddress, hostname: payload.hostname, position: payload.position }
    if (payload.id === null) await apiAddNetworkSwitch(floorId, rackId, body)
    else await apiUpdateNetworkSwitch(floorId, rackId, payload.id, body)
    const detail = await apiGetNetworkRack(floorId, rackId)
    selectedRackDetail.value = detail
    const idx = racks.value.findIndex(r => r.id === rackId)
    if (idx >= 0) racks.value[idx].switchCount = detail.switches.length
  } catch (e) {
    toast.add({ title: message(e), color: 'error' })
    // Roll the canvas back to what the register actually holds
    try { selectedRackDetail.value = await apiGetNetworkRack(floorId, rackId) } catch {}
  } finally { busy.value = false }
}

function deleteSwitchFromRack3D(id: number) {
  const sw = selectedRackDetail.value?.switches.find(s => s.id === id)
  if (!sw) return
  return deleteSwitch(sw)
}

// Confirm button of the shared delete dialog.
async function confirmDelete() {
  const req = deleteRequest.value
  if (!req || deleting.value) return
  deleting.value = true
  try {
    if (req.kind === 'switch') await performDeleteSwitch(req.sw)
    else if (req.kind === 'rack') await performDeleteRack(req.rack)
    else await performDeleteFloor(req.plan)
    showDeleteDialog.value = false
    deleteRequest.value = null
  } finally {
    deleting.value = false
  }
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

// Marker LEDs: the three dark bay panels of the server-rack glyph (viewBox 512) and a
// deterministic pseudo-random pattern per rack, so a rack keeps its own blink rhythm instead of
// reshuffling on every re-render.
// Measured from the glyph's own path: the three drive bars sit at y = 55/199/343 (18 tall,
// 144 apart - NOT the 120 that the unit pitch suggests), so the dark panel tops are those minus 1
// and the LEDs ride the bar centres at 64/208/352.
const RACK_GLYPH_PATH = 'M41 25v78h430V25zm254 23h18v32h-18zm121 0a16 16 0 0 1 16 16a16 16 0 0 1-16 16a16 16 0 0 1-16-16a16 16 0 0 1 16-16M64 55h48v18H64zm80 0h48v18h-48zm80 0h48v18h-48zm-119 66v30h302v-30zm-64 48v78h430v-78zm254 23h18v32h-18zm121 0a16 16 0 0 1 16 16a16 16 0 0 1-16 16a16 16 0 0 1-16-16a16 16 0 0 1 16-16m-352 7h48v18H64zm80 0h48v18h-48zm80 0h48v18h-48zm-119 66v30h302v-30zm-64 48v78h430v-78zm254 23h18v32h-18zm121 0a16 16 0 0 1 16 16a16 16 0 0 1-16 16a16 16 0 0 1-16-16a16 16 0 0 1 16-16m-352 7h48v18H64zm80 0h48v18h-48zm80 0h48v18h-48zm13 66v30h38v-30zM25 457v30h130.2l20-30zm171.8 0l-20 30h158.4l-20-30zm140 0l20 30H487v-30z'
const RACK_BAYS = [54, 198, 342]
function rackLeds(id: number) {
  const leds: { x: number; y: number; blue: boolean; dur: number; delay: number }[] = []
  RACK_BAYS.forEach((bay, row) => {
    for (let col = 0; col < 3; col++) {
      const noise = Math.abs(Math.sin((id * 97 + row * 41 + col * 13) * 12.9898) * 43758.5453)
      const r = noise - Math.floor(noise)
      leds.push({
        x: 88 + col * 80,
        y: bay + 10,
        blue: r > 0.5,
        dur: 1.4 + r * 2.2,
        delay: -(0.2 + r * 3.4)
      })
    }
  })
  return leds
}

// Opening an add/edit dialog should land the caret in the first text box, no extra click.
function focusField(id: string) {
  // The modal grabs focus while it mounts, so wait a frame and then claim it back.
  requestAnimationFrame(() => {
    const el = document.getElementById(id) as HTMLInputElement | null
    if (!el) return
    el.focus()
    const end = el.value.length
    el.setSelectionRange?.(end, end)
  })
}
watch(showUpload, async (open) => { if (open) { await nextTick(); focusField('floor-name') } })
watch(showRackEditor, async (open) => { if (open) { await nextTick(); focusField('rack-name') } })
watch(showSwitchEditor, async (open) => { if (open) { await nextTick(); focusField('switch-ip') } })
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
                  <!-- Same LED treatment as the map markers, on a fixed seed so the page icon keeps
                       its own rhythm. -->
                  <rect v-for="bay in RACK_BAYS" :key="`icon-bay-${bay}`" class="ap-rack-bay" x="58" :y="bay" width="218" height="20" rx="4" />
                  <circle v-for="(led, i) in rackLeds(7)" :key="`icon-led-${i}`" class="ap-rack-led"
                    :class="led.blue ? 'ap-rack-led-blue' : 'ap-rack-led-amber'"
                    :cx="led.x" :cy="led.y" r="12"
                    :style="{ animationDuration: `${led.dur}s`, animationDelay: `${led.delay}s` }" />
                </svg>
              </RecordAddIcon>
            </span>
            <div class="min-w-0">
              <h2 class="text-lg font-bold tracking-wide text-highlighted">Network Rack Map</h2>
              <p class="text-xs text-muted">{{ isAdmin ? 'Click Map to add rack, Click rack for details.' : 'Select a rack to see its switches.' }}</p>
            </div>
          </div>
          <div class="ap-floor-strip">
            <button v-if="floorNav.left" type="button" class="ap-floor-nav" aria-label="Show earlier floor plans" @click="scrollFloors(-1)">
              <UIcon name="i-lucide-chevron-left" class="size-3.5" />
            </button>
            <div v-if="floors.length" ref="floorListEl" class="ap-floor-list flex w-full min-w-0 gap-2 overflow-x-auto"
              :class="[floorPan.active ? 'ap-floor-dragging' : '']" aria-label="Floor plans" @pointerdown="startFloorPan" @scroll="syncFloorNav">
            <div v-for="item in floors" :key="item.id" class="ap-floor-tab relative w-[156px] shrink-0 rounded-lg border sm:w-[176px]"
              :class="activeFloorId === item.id ? 'border-primary/50 bg-primary/10 text-primary' : 'border-default bg-default/60 text-muted hover:border-primary/40 hover:text-highlighted'">
              <button type="button" class="flex h-12 w-full flex-col justify-center gap-0.5 rounded-lg py-1 pl-3 text-left focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
                :class="isAdmin ? 'pr-10' : 'pr-3'" :aria-pressed="activeFloorId === item.id" :disabled="busy" @click="selectFloor(item.id)">
                <span class="w-full truncate text-sm font-semibold leading-tight" :title="item.name">{{ item.name }}</span>
                <span class="text-[11px] leading-tight opacity-70">{{ item.rackCount }} rack{{ item.rackCount === 1 ? '' : 's' }}</span>
              </button>
              <UButton v-if="isAdmin" type="button" icon="i-lucide-trash-2" size="xs" color="neutral" variant="ghost"
                class="ap-plan-delete absolute right-1 top-1" :aria-label="`Delete floor plan ${item.name}`" :title="`Delete ${item.name}`"
                :disabled="busy" @click.stop="deleteFloor(item)" />
            </div>
            </div>
            <button v-if="floorNav.right" type="button" class="ap-floor-nav" aria-label="Show later floor plans" @click="scrollFloors(1)">
              <UIcon name="i-lucide-chevron-right" class="size-3.5" />
            </button>
          </div>
          <div v-if="isAdmin" class="ap-header-actions flex w-full shrink-0 items-center justify-end gap-2">
            <UButton v-if="floors.length" type="button" size="sm" icon="game-icons:server-rack" class="ap-rack-add-action record-add-hover stable-hover-hitbox" :disabled="busy || !floor" @click="openAddRack()">Add Rack</UButton>
            <UButton type="button" size="sm" icon="iconoven:floor-plan" variant="soft" class="ap-floor-add-action record-add-hover stable-hover-hitbox" :disabled="busy" @click="openUpload()">Add floor plan</UButton>
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
          <UCard :ui="{ body: 'relative flex min-h-0 flex-1 flex-col p-2 sm:p-3' }" class="flex min-h-0 min-w-0 flex-1 flex-col">
            <!-- UButton's own base class sets position: relative, so the anchor is this wrapper. -->
            <!-- Solid accent + dark halo: a soft/tinted button disappears on a white plan image. -->
            <div v-if="isAdmin && floor" class="absolute right-3 top-3 z-20">
              <UButton type="button" size="sm" icon="i-lucide-image-up" variant="solid" color="primary"
                class="ap-map-update-action record-add-hover stable-hover-hitbox shadow-lg shadow-black/30 ring-1 ring-black/10"
                :disabled="busy" :title="`Update map - ${floor.name}`" @click="openReplace(floor)">Update map</UButton>
            </div>
            <div v-if="floor && imageUrl" ref="mapStageEl" class="flex min-h-0 flex-1 items-center justify-center overflow-hidden">
              <div ref="mapEl" :style="mapFitStyle" class="relative shrink-0 overflow-hidden rounded-lg border border-default bg-elevated" :class="isAdmin ? 'cursor-crosshair' : ''" @click="placeOnMap">
                <img :src="imageUrl" :alt="`Floor plan: ${floor.name}`" draggable="false" decoding="async" class="block h-full w-full select-none" @load="onMapImageLoad" />
                <button v-for="rack in racks" :key="rack.id" type="button"
                  class="ap-rack-marker absolute -translate-x-1/2 -translate-y-1/2 transition-[box-shadow] focus-visible:outline-2"
                  :class="[selectedRackId === rack.id ? 'ring-4 ring-primary/40' : 'hover:ring-4 hover:ring-primary/30', isAdmin ? 'cursor-grab active:cursor-grabbing touch-none' : 'cursor-pointer']"
                  :style="{ left: `${rack.x * 100}%`, top: `${rack.y * 100}%` }"
                  :aria-label="`Rack ${rack.name}, ${rack.switchCount} switches`"
                  :title="rack.name" @pointerenter="prefetchRackDetail(rack); warmRack3D()" @focusin="prefetchRackDetail(rack)"
                  @click.stop="openRackDetails(rack)" @pointerdown.stop="pointerDown($event, rack)" @pointermove="pointerMove($event, rack)" @pointerup="pointerUp($event, rack)" @pointercancel="pointerUp($event, rack, true)">
                  <span class="ap-rack-body relative grid size-12 place-items-center rounded-xl border-2 border-primary bg-elevated shadow-md">
                    <!-- Same server-rack silhouette as the icon, but inlined so the three dark bay
                         panels can carry blinking LEDs. HIRO: "i prefer led nya di kotak-kotak hitam
                         pada server, blink random biru dan kuning". -->
                    <svg class="ap-rack-svg size-9 text-primary" viewBox="0 0 512 512" aria-hidden="true">
                      <path class="ap-rack-shell" :d="RACK_GLYPH_PATH" />
                      <rect v-for="bay in RACK_BAYS" :key="`bay-${bay}`" class="ap-rack-bay" x="58" :y="bay" width="218" height="20" rx="4" />
                      <circle v-for="(led, i) in rackLeds(rack.id)" :key="`led-${i}`" class="ap-rack-led"
                        :class="led.blue ? 'ap-rack-led-blue' : 'ap-rack-led-amber'"
                        :cx="led.x" :cy="led.y" r="12"
                        :style="{ animationDuration: `${led.dur}s`, animationDelay: `${led.delay}s` }" />
                    </svg>
                  </span>
                  <!-- Sits OUTSIDE .ap-rack-body: that element scales to 1.1 on hover, and a chip
                       anchored to its bottom edge would be carried downwards by the scale.
                       Shows the rack NAME (not its size) so the plan reads without opening each
                       rack; long names are clipped to keep neighbouring markers apart. -->
                  <span class="pointer-events-none absolute -bottom-2 left-1/2 max-w-[7rem] -translate-x-1/2 truncate whitespace-nowrap rounded-full bg-elevated px-1.5 py-px text-[9px] font-semibold text-primary ring-1 ring-inset ring-primary/40">{{ rack.name }}</span>
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
      <UModal v-if="isAdmin" v-model:open="showUpload" :title="replacingId === null ? 'Add floor plan' : 'Update floor plan'" :description="replacingId === null ? 'Upload an image and give this map a clear name.' : 'Replace the map image — the racks stay where they are.'" :dismissible="!busy" :close="!busy" :ui="{ content: 'max-w-lg ap-upload-modal network-modal-glow', header: 'relative z-10', body: 'relative z-10 p-4 sm:p-5' }">
        <template #body>
          <form class="space-y-4" @submit.prevent="createFloor">
            <div class="flex items-start gap-3 rounded-xl border border-primary/20 bg-primary/5 p-3 text-sm text-muted">
              <span class="ap-modal-icon grid size-9 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary"><UIcon :name="replacingId === null ? 'i-lucide-upload' : 'i-lucide-image-up'" class="ap-upload-glyph size-5" /></span>
              <p v-if="replacingId === null">Set floor name and floor layout, then place racks directly on the plan.</p>
              <p v-else>Racks keep their relative positions. If the new drawing is cropped differently, drag them into place afterwards.</p>
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
              <p class="mt-1.5 text-xs text-muted">{{ replacingId === null ? 'PNG, JPG or WebP · up to 8 MB' : 'Leave empty to keep the current image. PNG, JPG or WebP · up to 8 MB' }}</p>
              <div v-if="uploadPreviewUrl" class="mt-3 overflow-hidden rounded-lg border border-default bg-elevated p-2">
                <img :src="uploadPreviewUrl" alt="Preview of selected floor plan" class="h-36 w-full object-contain" />
                <p class="mt-1.5 truncate text-xs text-muted">{{ uploadFile?.name }}</p>
              </div>
            </div>
            <p role="alert" class="min-h-5 text-sm text-error">{{ uploadError }}</p>
            <div class="flex justify-end gap-2 border-t border-default pt-4">
              <UButton type="button" color="red" variant="ghost" icon="basil:cancel-outline" class="cancel-hover" :disabled="busy" @click="showUpload = false">Cancel</UButton>
              <UButton type="submit" :icon="replacingId === null ? 'i-lucide-upload' : 'i-lucide-save'" class="record-save-hover stable-hover-hitbox" :loading="busy" :disabled="busy">{{ replacingId === null ? 'Upload plan' : 'Save changes' }}</UButton>
            </div>
          </form>
        </template>
      </UModal>

      <!-- Rack editor modal -->
      <UModal v-if="isAdmin" v-model:open="showRackEditor" :title="editingRackId === null ? 'Add rack' : 'Edit rack'" :description="editingRackId === null ? 'Place a network rack on this floor plan.' : 'Update this rack’s details.'" :dismissible="!busy" :close="!busy" :ui="{ content: 'max-w-lg network-modal-glow', body: 'p-4 sm:p-5' }">
        <template #body>
          <form class="space-y-4" @submit.prevent="saveRack">
            <div class="flex items-center gap-3 rounded-xl border border-primary/20 bg-primary/5 p-3">
              <span class="ap-modal-icon grid size-10 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary">
                <!-- Same LED treatment as the map markers / details header, on a fixed seed of its own. -->
                <svg class="ap-rack-svg size-6" viewBox="0 0 512 512" aria-hidden="true">
                  <path class="ap-rack-shell" :d="RACK_GLYPH_PATH" />
                  <rect v-for="bay in RACK_BAYS" :key="`add-bay-${bay}`" class="ap-rack-bay" x="58" :y="bay" width="218" height="20" rx="4" />
                  <circle v-for="(led, i) in rackLeds(3)" :key="`add-led-${i}`" class="ap-rack-led"
                    :class="led.blue ? 'ap-rack-led-blue' : 'ap-rack-led-amber'"
                    :cx="led.x" :cy="led.y" r="12"
                    :style="{ animationDuration: `${led.dur}s`, animationDelay: `${led.delay}s` }" />
                </svg>
              </span>
              <div class="min-w-0"><p class="text-sm font-medium text-highlighted">{{ floor?.name }}</p></div>
            </div>
            <fieldset :disabled="busy">
              <legend class="mb-1.5 text-sm font-medium text-highlighted">Rack name</legend>
              <UInput id="rack-name" v-model="rackDraft.name" class="w-full" maxlength="60" placeholder="e.g. A,B,B-B2,D-B1" :disabled="busy" />
            </fieldset>
            <fieldset :disabled="busy">
              <legend class="mb-2 flex w-full items-baseline justify-between gap-3">
                <span class="text-sm font-medium text-highlighted">Rack size</span>
                <span class="text-xs text-muted">{{ rackDraft.sizeU }}U cabinet · {{ rackSizeHeight(rackDraft.sizeU) }}</span>
              </legend>
              <div class="grid grid-cols-4 gap-2">
                <button v-for="opt in RACK_SIZES" :key="opt" type="button"
                  class="group relative flex flex-col items-center gap-1.5 overflow-hidden rounded-xl border px-1 pb-2 pt-2.5 transition-colors"
                  :class="rackDraft.sizeU === opt ? 'border-primary/60 bg-primary/10 ring-1 ring-primary/40' : 'border-default bg-elevated/40 hover:border-primary/35 hover:bg-primary/5'"
                  :aria-pressed="rackDraft.sizeU === opt"
                  :title="`${opt}U cabinet, ${rackSizeHeight(opt)} tall`"
                  @click="rackDraft.sizeU = opt">
                  <span class="flex h-14 w-full items-end justify-center">
                    <span class="relative w-6 rounded-[3px] border transition-colors"
                      :class="rackDraft.sizeU === opt ? 'border-primary/70 bg-primary/15 text-primary' : 'border-default bg-elevated text-muted'"
                      :style="{ height: `${Math.round((opt / 42) * 100)}%` }">
                      <span class="absolute inset-x-[3px] inset-y-[3px] rounded-[1px] opacity-60"
                        style="background-image: repeating-linear-gradient(to bottom, currentColor 0 1px, transparent 1px 4px)" />
                    </span>
                  </span>
                  <span class="text-xs font-semibold leading-none" :class="rackDraft.sizeU === opt ? 'text-primary' : 'text-highlighted'">{{ opt }}U</span>
                  <span class="text-[10px] leading-none text-muted">{{ rackSizeHeight(opt) }}</span>
                </button>
              </div>
              <p class="mt-2 text-[11px] leading-relaxed text-muted">
                The 3D cabinet, its U numbering and the free-U count all follow this size. Existing racks keep their own size when edited.
              </p>
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
      <UModal v-if="isAdmin" v-model:open="showSwitchEditor" :title="editingSwitchId === null ? 'Add switch' : 'Edit switch'" :description="editingSwitchId === null ? 'Add a switch to this rack.' : 'Update this switch’s details.'" :dismissible="!busy" :close="!busy" :ui="{ content: 'max-w-lg network-modal-glow', body: 'p-4 sm:p-5' }">
        <template #body>
          <form class="space-y-4" @submit.prevent="saveSwitch">
            <div class="flex items-center gap-3 rounded-xl border border-primary/20 bg-primary/5 p-3">
              <span class="ap-modal-icon grid size-10 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary"><UIcon name="i-lucide-cpu" class="size-5" /></span>
              <div class="min-w-0"><p class="text-sm font-medium text-highlighted">{{ selectedRackDetail?.name }}</p><p class="text-xs text-muted">Rack on {{ floor?.name }}</p></div>
            </div>
            <fieldset :disabled="busy">
              <legend class="mb-1.5 text-sm font-medium text-highlighted">IP address</legend>
              <UInput id="switch-ip" v-model="switchDraft.ipAddress" class="w-full" maxlength="45" placeholder="e.g. 192.168.1.1" :disabled="busy" />
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

      <!-- Rack modal -->
      <UModal v-model:open="showRackDetails" :dismissible="!busy" :close="!busy" :ui="{ content: 'max-w-7xl network-modal-glow', body: 'p-4 sm:p-5' }">
        <template #header="{ close }">
          <div v-if="headerRack" class="flex w-full items-center gap-3">
            <span class="ap-modal-icon grid size-12 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary ring-1 ring-inset ring-primary/20">
              <!-- Same rack glyph and LED bays as the map marker; seeded with THIS rack's id so the
                   modal icon blinks in step with that rack on the plan. -->
              <svg class="ap-rack-svg size-7" viewBox="0 0 512 512" aria-hidden="true">
                <path class="ap-rack-shell" :d="RACK_GLYPH_PATH" />
                <rect v-for="bay in RACK_BAYS" :key="`detail-bay-${bay}`" class="ap-rack-bay" x="58" :y="bay" width="218" height="20" rx="4" />
                <circle v-for="(led, i) in rackLeds(headerRack.id)" :key="`detail-led-${i}`" class="ap-rack-led"
                  :class="led.blue ? 'ap-rack-led-blue' : 'ap-rack-led-amber'"
                  :cx="led.x" :cy="led.y" r="12"
                  :style="{ animationDuration: `${led.dur}s`, animationDelay: `${led.delay}s` }" />
              </svg>
            </span>
            <div class="min-w-0 flex-1">
              <div class="flex items-center gap-2">
                <h3 class="truncate text-base font-semibold text-highlighted">{{ headerRack.name }}</h3>
                <span class="shrink-0 rounded-full bg-primary/10 px-2 py-0.5 text-[11px] font-semibold tabular-nums text-primary ring-1 ring-inset ring-primary/25">{{ headerRack.sizeU }}U</span>
              </div>
              <p class="truncate text-xs text-muted">{{ floor?.name }} · {{ rackSizeHeight(headerRack.sizeU) }} cabinet</p>
            </div>
            <UButton v-if="isAdmin && selectedRackDetail" type="button" icon="i-lucide-trash-2" variant="ghost" color="error" size="sm"
              :disabled="busy" :aria-label="`Delete ${headerRack.name}`" :title="`Delete ${headerRack.name}`"
              @click="deleteRack(selectedRackDetail)" />
            <UButton type="button" icon="i-lucide-x" variant="ghost" color="neutral" size="sm" class="ap-close-action" :disabled="busy" :aria-label="`Close ${headerRack.name}`" @click="close" />
          </div>
        </template>
        <template #body>
          <!-- Deliberately the SAME panel as Rack3DView's #loading-screen (dark plate, 48px primary
               ring, mono uppercase caption) so the changeover is invisible: one initialising state. -->
          <div v-if="!selectedRackDetail" class="grid h-[min(680px,calc(100dvh_-_18rem))] place-items-center overflow-hidden rounded-xl border border-default bg-[#06090f]">
            <div class="flex flex-col items-center justify-center">
              <div class="mb-4 h-12 w-12 animate-spin rounded-full border-4 border-primary/30 border-t-primary"></div>
              <div class="text-sm font-mono tracking-widest uppercase text-primary">Initializing {{ headerRack?.sizeU ?? 42 }}U Rack &amp; Switch Telemetry...</div>
            </div>
          </div>
          <div v-else>
            <LazyRack3DView :key="`rack3d-${selectedRackDetail.id}-${selectedRackDetail.sizeU}`" :switches="selectedRackDetail.switches"
              :total-u="selectedRackDetail.sizeU" :hud-settings="selectedRackDetail.hudSettings || ''"
              @delete-switch="deleteSwitchFromRack3D" @save-switch="saveSwitchFromRack3D"
              @save-settings="saveHudSettingsFromRack3D"
              class="h-[min(680px,calc(100dvh_-_18rem))] overflow-hidden rounded-xl border border-default" />
          </div>
        </template>
      </UModal>

      <!-- Shared delete confirmation, built like the PC Ledger / CCTV row-delete dialogs: a wrapper
           that clips, a soft danger wash bleeding in from the top-right corner, the identity of the
           record on a raised box above that wash, and a footer band on the elevated surface. -->
      <UModal v-model:open="showDeleteDialog" :dismissible="!deleting" :close="!deleting" :ui="{ content: 'max-w-md network-modal-glow' }">
        <template #content>
          <div class="overflow-hidden rounded-xl">
            <div class="relative overflow-hidden px-5 pb-5 pt-5">
              <div aria-hidden="true" class="pointer-events-none absolute -right-16 -top-24 size-48 rounded-full bg-error/20 blur-3xl" />
              <div class="relative flex items-start gap-3">
                <span class="grid size-11 shrink-0 place-items-center rounded-full bg-error/10 text-error ring-1 ring-inset ring-error/25">
                  <UIcon name="i-lucide-trash-2" class="size-5" />
                </span>
                <div class="min-w-0">
                  <h2 class="text-sm font-semibold text-highlighted">{{ deleteDialog.title }}</h2>
                  <p class="mt-1 text-xs leading-relaxed text-muted">{{ deleteDialog.description }}</p>
                </div>
              </div>

              <dl class="relative mt-4 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 rounded-lg bg-elevated/60 p-3 text-xs ring-1 ring-inset ring-default">
                <template v-for="row in deleteDialog.rows" :key="row.label">
                  <dt class="text-muted">{{ row.label }}</dt>
                  <dd class="truncate font-medium text-highlighted" :class="row.mono ? 'font-mono tabular-nums' : ''">{{ row.value }}</dd>
                </template>
              </dl>
            </div>

            <div class="flex items-center justify-end gap-2 bg-elevated/40 px-5 py-4">
              <UButton color="neutral" variant="ghost" label="Cancel" :disabled="deleting" @click="showDeleteDialog = false" />
              <UButton color="error" class="trash-bin-hover" :label="deleteDialog.action" :loading="deleting" @click="confirmDelete">
                <template #leading>
                  <TrashBinIcon class="size-5" data-slot="leadingIcon" />
                </template>
              </UButton>
            </div>
          </div>
        </template>
      </UModal>
    </template>
  </UDashboardPanel>
</template>

<style scoped>
/* Server marker LEDs: dark bay panels on the rack silhouette with indicators blinking blue/amber.
   Kept in CSS so the rhythm is free (no JS timer per marker). */
.ap-rack-svg { display: block; }
.ap-rack-shell { fill: currentColor; opacity: .9; }
.ap-rack-bay { fill: #030712; opacity: .92; }
.ap-rack-led {
  animation-name: ap-led-blink;
  animation-iteration-count: infinite;
  animation-timing-function: linear;
}
.ap-rack-led-blue { fill: #38bdf8; filter: drop-shadow(0 0 2.5px #38bdf8); }
.ap-rack-led-amber { fill: #facc15; filter: drop-shadow(0 0 2.5px #facc15); }
@keyframes ap-led-blink {
  0%, 40% { opacity: 1; }
  41%, 52% { opacity: .14; }
  53%, 74% { opacity: 1; }
  75%, 86% { opacity: .14; }
  87%, 100% { opacity: 1; }
}
@media (prefers-reduced-motion: reduce) {
  .ap-rack-led { animation: none; opacity: .62; }
}
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
  /* Fits three 176px cards plus the arrow that says another floor is waiting. */
  .ap-header-row { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 588px) minmax(0, 1fr); }
  .ap-header-actions { grid-column: 3; }
}
.ap-map-update-action { box-shadow: 0 6px 16px rgb(0 0 0 / 0.35), 0 1px 0 rgb(255 255 255 / 0.18) inset; }
.ap-floor-list { justify-content: safe center; scrollbar-width: none; cursor: grab; }
/* No scrollbar: the strip is panned by dragging it. */
.ap-floor-list.ap-floor-dragging { cursor: grabbing; scroll-behavior: auto; user-select: none; }
.ap-floor-strip { display: flex; width: 100%; min-width: 0; align-items: center; gap: 4px; }
/* A small chevron that only exists while another floor is off-screen. */
.ap-floor-nav {
  display: grid;
  flex: none;
  place-items: center;
  width: 20px;
  height: 32px;
  border-radius: 6px;
  border: 1px solid var(--ui-border);
  background: color-mix(in srgb, var(--ui-bg-elevated) 88%, transparent);
  color: var(--ui-text-muted);
  transition: color 160ms ease, border-color 160ms ease, background-color 160ms ease;
}
.ap-floor-nav:hover { color: var(--ui-primary); border-color: color-mix(in srgb, var(--ui-primary) 55%, transparent); background: color-mix(in srgb, var(--ui-primary) 12%, transparent); }
.ap-floor-list::-webkit-scrollbar { display: none; }
.ap-floor-tab { transition: border-color 180ms ease, background-color 180ms ease, box-shadow 180ms ease; }
.ap-plan-delete {
  color: var(--ui-text-muted);
  transition: color 180ms ease, background-color 180ms ease, transform 180ms ease, box-shadow 180ms ease;
}
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
  /* HIRO: "animasikan tombol delete card ... ketika di hover" - hover-only motion, no permanent
     colour: lift + error ring + the household trash nudge. */
  .ap-plan-delete:not(:disabled):not([aria-disabled='true']):hover {
    color: var(--ui-error);
    background-color: color-mix(in srgb, var(--ui-error) 14%, transparent);
    transform: translateY(-1px) scale(1.1);
    /* No ring/border on hover - HIRO: "hilangkan bordernya". Only the soft drop glow stays. */
    box-shadow: 0 4px 12px color-mix(in srgb, var(--ui-error) 26%, transparent);
  }
  .ap-plan-delete:not(:disabled):not([aria-disabled='true']):hover :deep([data-slot='leadingIcon']) {
    animation: record-delete-nudge 520ms ease-out;
  }
  .ap-close-action:not(:disabled):hover :deep([data-slot='leadingIcon']) { transform: scale(1.14) rotate(12deg); }
  .ap-rack-marker:hover:not(:active) { box-shadow: 0 4px 16px color-mix(in srgb, var(--ui-primary) 35%, transparent); }
}
.ap-rack-marker:focus-visible { box-shadow: 0 4px 16px color-mix(in srgb, var(--ui-primary) 35%, transparent); }
.ap-stable-action.record-delete-hover:not(:disabled):not([aria-disabled='true']):is(:hover, :focus-visible) { transform: none; }
@media (prefers-reduced-motion: reduce) {
  .ap-floor-tab, .ap-plan-delete, .ap-close-action :deep([data-slot='leadingIcon']), .ap-rack-marker { transition: none; transform: none !important; }
  .ap-plan-delete :deep([data-slot='leadingIcon']) { animation: none !important; }
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
