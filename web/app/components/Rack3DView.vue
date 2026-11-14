<script setup lang="ts">
import type { NetworkSwitch } from '~/types'

// 3D visualisation of one 24U floor rack and the Cisco-style switches inside it.
// Real switches from the rack keep their declared U-position and hostname/IP label;
// standby units top the scene up to 10 so the rack never renders empty.
const props = defineProps<{ switches: NetworkSwitch[] }>()

const TOTAL_U = 24
const DEMO_UNITS = 10
const FRAME_W = 12
const FRAME_D = 10

const host = ref<HTMLElement | null>(null)

interface PlacedUnit { startU: number; heightU: number; label: string; standby: boolean }

function parseUnitRange(raw: string): { start: number; end: number } | null {
  const m = /u\s*(\d+)\s*(?:[-–]\s*(?:u\s*)?(\d+))?/i.exec(raw.trim())
  if (!m) return null
  const a = Number(m[1]); const b = m[2] ? Number(m[2]) : a
  if (!Number.isFinite(a) || !Number.isFinite(b)) return null
  const lo = Math.min(a, b); const hi = Math.max(a, b)
  if (lo < 1 || hi > TOTAL_U) return null
  return { start: lo, end: hi }
}

function layoutUnits(switches: NetworkSwitch[]): PlacedUnit[] {
  const taken = new Set<number>()
  const placed: PlacedUnit[] = []
  for (const sw of switches) {
    const range = parseUnitRange(sw.position)
    let start = range?.start ?? 0
    if (!start) {
      start = 1
      while (start <= TOTAL_U && taken.has(start)) start++
      if (start > TOTAL_U) continue
    }
    const height = range ? range.end - range.start + 1 : 1
    const end = Math.min(start + height - 1, TOTAL_U)
    for (let u = start; u <= end; u++) taken.add(u)
    placed.push({ startU: start, heightU: end - start + 1, label: sw.hostname.trim() || sw.ipAddress || `SW-${String(placed.length + 1).padStart(2, '0')}`, standby: false })
  }
  let n = 0
  for (let u = 1; u <= TOTAL_U && placed.length + n < DEMO_UNITS; u++) {
    if (taken.has(u)) continue
    taken.add(u)
    n++
    placed.push({ startU: u, heightU: 1, label: `SW-${String(switches.length + n).padStart(2, '0')}`, standby: true })
  }
  return placed.sort((a, b) => a.startU - b.startU)
}

onMounted(async () => {
  if (!host.value) return
  try {
    const THREE = await import('three')
    const { OrbitControls } = await import('three/examples/jsm/controls/OrbitControls.js')

    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches

    const scene = new THREE.Scene()
    const camera = new THREE.PerspectiveCamera(40, 1, 0.1, 200)
    camera.position.set(4, 12.5, 33)

    const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true })
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2))
    renderer.shadowMap.enabled = true
    renderer.shadowMap.type = THREE.PCFSoftShadowMap
    renderer.toneMapping = THREE.ACESFilmicToneMapping
    renderer.toneMappingExposure = 1.15
    host.value.appendChild(renderer.domElement)
    renderer.domElement.style.display = 'block'

    // --- lights ---
    scene.add(new THREE.HemisphereLight(0x93c5fd, 0x0b1220, 0.55))
    const key = new THREE.DirectionalLight(0xffffff, 2.8)
    key.position.set(7, 15, 9)
    key.castShadow = true
    key.shadow.mapSize.set(2048, 2048)
    key.shadow.camera.left = -16; key.shadow.camera.right = 16
    key.shadow.camera.top = 18; key.shadow.camera.bottom = -4
    key.shadow.bias = -0.0005
    scene.add(key)
    const rim = new THREE.DirectionalLight(0x7dd3fc, 0.9)
    rim.position.set(-9, 10, -7)
    scene.add(rim)
    const accent = new THREE.PointLight(0x00c16a, 8, 34, 2)
    accent.position.set(0, 6, 11)
    scene.add(accent)

    // --- ground ---
    const ground = new THREE.Mesh(
      new THREE.CircleGeometry(26, 48),
      new THREE.ShadowMaterial({ opacity: 0.35 })
    )
    ground.rotation.x = -Math.PI / 2
    ground.receiveShadow = true
    scene.add(ground)
    const grid = new THREE.GridHelper(60, 30, 0x164e3a, 0x1b2534)
    grid.position.y = 0.01
    scene.add(grid)

    // --- 24U frame ---
    const frameMat = new THREE.MeshStandardMaterial({ color: 0x374151, metalness: 0.7, roughness: 0.35 })
    const railMat = new THREE.MeshStandardMaterial({ color: 0x111827, metalness: 0.5, roughness: 0.5 })
    const H = TOTAL_U + 0.5
    const postGeo = new THREE.BoxGeometry(0.5, H, 0.5)
    for (const [px, pz] of [[-1, -1], [1, -1], [-1, 1], [1, 1]] as const) {
      const post = new THREE.Mesh(postGeo, frameMat)
      post.position.set(px * (FRAME_W / 2 - 0.25), H / 2, pz * (FRAME_D / 2 - 0.25))
      post.castShadow = true
      scene.add(post)
    }
    const railGeo = new THREE.BoxGeometry(FRAME_W - 0.4, 0.07, 0.12)
    for (let u = 0; u <= TOTAL_U; u++) {
      const rail = new THREE.Mesh(railGeo, railMat)
      rail.position.set(0, u, FRAME_D / 2 - 0.06)
      scene.add(rail)
    }
    const capGeo = new THREE.BoxGeometry(FRAME_W + 0.4, 0.3, FRAME_D + 0.4)
    const capTop = new THREE.Mesh(capGeo, railMat)
    capTop.position.set(0, H + 0.15, 0)
    capTop.castShadow = true
    scene.add(capTop)
    const capBottom = new THREE.Mesh(capGeo, railMat)
    capBottom.position.set(0, 0.15, 0)
    capBottom.castShadow = true
    capBottom.receiveShadow = true
    scene.add(capBottom)

    // --- switch front panels ---
    function frontPanelTexture(label: string, standby: boolean, heightU: number): THREE.CanvasTexture {
      const c = document.createElement('canvas')
      c.width = 512
      c.height = 64 * heightU
      const g = c.getContext('2d')!
      g.fillStyle = standby ? '#0f141c' : '#151b26'
      g.fillRect(0, 0, c.width, c.height)
      g.fillStyle = '#0a0e14'
      g.fillRect(0, 0, 18, c.height)
      g.fillRect(494, 0, 18, c.height)
      g.fillStyle = standby ? '#7d8a99' : '#e5e7eb'
      g.font = '600 20px ui-monospace, SFMono-Regular, Menlo, monospace'
      g.textBaseline = 'middle'
      g.fillText(label.slice(0, 18), 30, 16)
      if (!standby) {
        for (let i = 0; i < 3; i++) {
          g.fillStyle = i === 1 ? '#f59e0b' : '#22c55e'
          g.beginPath()
          g.arc(34 + i * 12, 38, 4, 0, Math.PI * 2)
          g.fill()
        }
      }
      const rows = heightU * 2
      const rowH = (c.height - 8) / rows
      for (let row = 0; row < rows; row++) {
        for (let i = 0; i < 12; i++) {
          const x = 168 + i * 22
          const yy = 4 + row * rowH + (rowH - 12) / 2
          g.fillStyle = '#05070c'
          g.fillRect(x, yy, 16, 12)
          g.strokeStyle = '#3b4757'
          g.strokeRect(x + 0.5, yy + 0.5, 15, 11)
        }
      }
      for (let i = 0; i < 2; i++) {
        g.fillStyle = '#05070c'
        g.fillRect(438 + i * 24, 12, 18, 16)
        g.strokeStyle = '#3b4757'
        g.strokeRect(438.5 + i * 24, 12.5, 17, 15)
      }
      const t = new THREE.CanvasTexture(c)
      t.colorSpace = THREE.SRGBColorSpace
      t.anisotropy = 4
      return t
    }

    const units = layoutUnits(props.switches)
    const switchBodyMat = new THREE.MeshStandardMaterial({ color: 0x1f2937, metalness: 0.55, roughness: 0.45 })
    const ledMeshes: { mat: THREE.MeshStandardMaterial; phase: number; standby: boolean }[] = []
    const interiorW = FRAME_W - 1.4
    const interiorD = FRAME_D - 2.4
    for (let i = 0; i < units.length; i++) {
      const unit = units[i]
      const h = unit.heightU * 1 - 0.1
      const y = (unit.startU - 1) + unit.heightU / 2
      const body = new THREE.Mesh(new THREE.BoxGeometry(interiorW, h, interiorD), switchBodyMat)
      body.position.set(0, y, 0.4)
      body.castShadow = true
      body.receiveShadow = true
      scene.add(body)
      const front = new THREE.Mesh(
        new THREE.PlaneGeometry(interiorW, h),
        new THREE.MeshBasicMaterial({ map: frontPanelTexture(unit.label, unit.standby, unit.heightU) })
      )
      front.position.set(0, y, 0.4 + interiorD / 2 + 0.01)
      scene.add(front)
      const ledMat = new THREE.MeshStandardMaterial({
        color: 0x0b1220,
        emissive: unit.standby ? 0x1f2937 : 0x22ff88,
        emissiveIntensity: unit.standby ? 0.15 : 1.6
      })
      const led = new THREE.Mesh(new THREE.BoxGeometry(0.18, 0.1, 0.06), ledMat)
      led.position.set(interiorW / 2 - 0.5, y + h / 2 - 0.14, 0.4 + interiorD / 2 + 0.04)
      scene.add(led)
      ledMeshes.push({ mat: ledMat, phase: i * 1.31, standby: unit.standby })
    }

    // --- controls ---
    const controls = new OrbitControls(camera, renderer.domElement)
    controls.target.set(0, 11.2, 0)
    controls.enableDamping = true
    controls.dampingFactor = 0.08
    controls.minDistance = 10
    controls.maxDistance = 44
    controls.maxPolarAngle = 1.52
    controls.autoRotate = !reduceMotion
    controls.autoRotateSpeed = 0.7
    controls.addEventListener('start', () => { controls.autoRotate = false })

    function fit() {
      const w = host.value?.clientWidth ?? 0
      const hh = host.value?.clientHeight ?? 0
      if (!w || !hh) return
      renderer.setSize(w, hh)
      camera.aspect = w / hh
      camera.updateProjectionMatrix()
    }
    fit()
    const ro = new ResizeObserver(fit)
    ro.observe(host.value)

    const clock = new THREE.Clock()
    let raf = 0
    const tick = () => {
      raf = requestAnimationFrame(tick)
      controls.update()
      if (!reduceMotion) {
        const t = clock.getElapsedTime()
        for (const led of ledMeshes) {
          if (!led.standby) led.mat.emissiveIntensity = 1.1 + Math.sin(t * 3 + led.phase) * 0.7
        }
      }
      renderer.render(scene, camera)
    }
    tick()

    watchEffect(onCleanup => {
      onCleanup(() => {
        cancelAnimationFrame(raf)
        ro.disconnect()
        controls.dispose()
        scene.traverse(obj => {
          const mesh = obj as THREE.Mesh
          if (mesh.geometry) mesh.geometry.dispose()
          const mat = (mesh as THREE.Mesh).material as THREE.Material | THREE.Material[] | undefined
          if (Array.isArray(mat)) mat.forEach(m => { const s = m as THREE.MeshStandardMaterial; s.map?.dispose(); s.dispose() })
          else if (mat) { const s = mat as THREE.MeshStandardMaterial; s.map?.dispose(); s.dispose() }
        })
        renderer.dispose()
        renderer.domElement.remove()
      })
    })
  } catch {
    host.value.innerHTML = '<div class="grid h-full place-items-center text-xs text-muted">3D view is not available in this browser.</div>'
  }
})
</script>

<template>
  <div class="rack3d-host">
    <div ref="host" class="rack3d-canvas" role="img" :aria-label="`3D view of a 24U network rack with ${switches.length} switch${switches.length === 1 ? '' : 'es'}`"></div>
    <div class="rack3d-panel"><slot name="panel" /></div>
    <span class="rack3d-hint">Drag to orbit · scroll to zoom</span>
  </div>
</template>

<style scoped>
.rack3d-host {
  position: relative;
  background:
    radial-gradient(120% 90% at 50% 0%, #16202f 0%, #0b1220 55%, #070c15 100%);
}
.rack3d-canvas {
  position: absolute;
  inset: 0;
}
.rack3d-panel {
  position: absolute;
  top: 10px;
  bottom: 10px;
  left: 10px;
  width: 232px;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border-radius: 10px;
  border: 1px solid rgb(148 163 184 / 0.18);
  background: rgb(2 6 23 / 0.72);
  backdrop-filter: blur(8px);
  box-shadow: 0 10px 30px rgb(0 0 0 / 0.45);
  pointer-events: auto;
}
.rack3d-hint {
  position: absolute;
  right: 8px;
  bottom: 6px;
  font-size: 10px;
  color: rgb(148 163 184 / 0.8);
  pointer-events: none;
  user-select: none;
}
</style>
