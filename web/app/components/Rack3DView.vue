<script setup lang="ts">
// Offline-vendored fonts (replaces the sample's Google Fonts links)
import '@fontsource/inter/400.css'
import '@fontsource/inter/500.css'
import '@fontsource/inter/600.css'
import '@fontsource/inter/700.css'
import '@fontsource/jetbrains-mono/400.css'
import '@fontsource/jetbrains-mono/500.css'
import '@fontsource/jetbrains-mono/600.css'

// Ported from D:\WORK\PANASONIC\Web\INFRA-CAP\reff\3d_24u_network_rack_cisco_switches.html
// Offline adaptations: three + OrbitControls from npm (no jsdelivr importmap), fonts from
// @fontsource (no Google Fonts), Tailwind from the app build (no Play CDN), window-sized
// renderer -> container-sized + ResizeObserver, pointer/tooltip math relative to the container,
// and full dispose on unmount so the modal can open/close repeatedly without leaks.
import * as THREE from 'three'
import { RoomEnvironment } from 'three/examples/jsm/environments/RoomEnvironment.js'
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js'
import type { NetworkSwitch } from '~/types'

// The rack's real switches are the only source of hardware in this scene. `totalU` is the cabinet
// height in rack units, stored per rack by the register: 19U/22U/27U/42U all render from the same
// geometry spec, so a smaller cabinet is simply a shorter one with fewer numbered slots.
const props = withDefaults(defineProps<{ switches: NetworkSwitch[]; totalU?: number; hudSettings?: string }>(), { totalU: 42, hudSettings: '' })

// Deleting a switch is not a local edit: it has to reach the register, or the next load of the
// rack detail simply re-reads the row and the device reappears. The parent page owns that API
// call, the confirmation and its error toast, so the panel asks it instead of dropping the row.
const emit = defineEmits<{
  (e: 'delete-switch', id: number): void
  // Anything invented or edited inside this panel is a REGISTER row: the page owns the API call,
  // otherwise the change only lives in this component and vanishes when the modal is reopened.
  (e: 'save-switch', payload: { id: number | null; ipAddress: string; hostname: string; position: string }): void
}>()

const root = ref<HTMLElement | null>(null)
const $ = (id: string) => root.value?.querySelector(`#${id}`)

// Both drawers can fold down to a narrow rail so the 3D rack gets the whole canvas. The panels are
// only hidden with v-show, never unmounted: the scene script drives them by id (hud-toast, tab-*,
// view-*, the inputs), so removing them from the DOM would break every one of those lookups.
const leftCollapsed = ref(false)
const rightCollapsed = ref(false)
const DRAWER_KEY = 'rack3d.drawers'
onMounted(() => {
  try {
    const saved = JSON.parse(localStorage.getItem(DRAWER_KEY) || '{}')
    leftCollapsed.value = !!saved.left
    rightCollapsed.value = !!saved.right
  } catch {
    /* a corrupt value just means both drawers start expanded */
  }
})
watch([leftCollapsed, rightCollapsed], ([l, r]) => {
  try {
    localStorage.setItem(DRAWER_KEY, JSON.stringify({ left: l, right: r }))
  } catch {
    /* private mode: the fold still works for this session */
  }
})

// Teardown and live-data hooks are registered synchronously HERE, at setup level. The scene is
// built inside an async onMounted that awaits the vendored fonts first, and a lifecycle hook
// registered after that await is no longer bound to the component instance, so it would be
// silently dropped and the WebGL context would leak on every modal open/close.
let disposeScene: (() => void) | null = null
let applySwitches: ((list: NetworkSwitch[]) => void) | null = null
onUnmounted(() => disposeScene?.())
watch(() => props.switches, list => applySwitches?.(list ?? []), { deep: true })

// Height of the cabinet in U. Module level because the slot parser below needs the same ceiling
// as the geometry spec inside onMounted - when this was hardcoded to 24 a stored "U30" in the
// register was silently treated as unparseable and the switch got repacked into the lowest free
// slot instead of the slot the user chose.
const DEFAULT_TOTAL_U = 42
const ALLOWED_TOTALS = [19, 22, 27, 42]
// Written once per mount from the `totalU` prop before any geometry is built, and read by the
// helpers below, so every rail, slot option and free-U count agrees with the same cabinet height.
let RACK_TOTAL_U: number = DEFAULT_TOTAL_U

// The panel's own settings, read from the rack record. Parsed once per mount: these used to be plain
// component variables, so every close/reopen of the modal reset the UPS, the door and the toggles.
type HudSettings = {
  upsModel?: string
  upsSerial?: string
  doorOpen?: boolean
  sidesVisible?: boolean
  traffic?: boolean
  autoRotate?: boolean
  intensity?: number
}
function parseHudSettings(raw?: string): HudSettings {
  if (!raw) return {}
  try {
    const parsed = JSON.parse(raw)
    return parsed && typeof parsed === 'object' ? parsed as HudSettings : {}
  } catch {
    return {}
  }
}
const initialHud = parseHudSettings(props.hudSettings)

function normalizeTotalU(value: number | undefined): number {
  const n = Math.round(Number(value))
  return ALLOWED_TOTALS.includes(n) ? n : DEFAULT_TOTAL_U
}

// "U6", "U6-U7", "u 6 - u 7" -> { start, end }; anything unparseable returns null.
function parseUnitRange(raw: string): { start: number; end: number } | null {
  const m = /u\s*(\d+)\s*(?:[-\u2013]\s*(?:u\s*)?(\d+))?/i.exec((raw || '').trim())
  if (!m) return null
  const a = Number(m[1])
  const b = m[2] ? Number(m[2]) : a
  if (!Number.isFinite(a) || !Number.isFinite(b)) return null
  const lo = Math.min(a, b)
  const hi = Math.max(a, b)
  if (lo < 1 || hi > RACK_TOTAL_U) return null
  return { start: lo, end: hi }
}

onMounted(async () => {
  const container = $('canvas-container')
  if (!container) return

  // Resolve the cabinet height before anything reads it: rails, U numbering, slot options,
  // free-U badge and the camera ratios all derive from RACK_TOTAL_U.
  RACK_TOTAL_U = normalizeTotalU(props.totalU)

  try {
    // The scene's colour tone follows the app's accent (--ui-primary), which the user can switch
    // live from the theme picker. The value is read ONCE per mount: the modal rebuilds the scene
    // on every open, so a changed accent is picked up the next time the rack is opened.
    const accentCss = (() => {
      const probe = document.createElement('span')
      probe.style.color = 'var(--ui-primary, #00c16a)'
      probe.style.display = 'none'
      document.body.appendChild(probe)
      const css = getComputedStyle(probe).color
      probe.remove()
      return css
    })()
    // MEASURED PITFALL: `--ui-primary` is an oklch() colour (oklch(0.702 0.183 293.541) for violet).
    // THREE.Color cannot parse oklch/color-mix, and does not throw - it leaves the colour at its
    // default, which is pure white. That is why every accent-tinted surface in here once rendered
    // white no matter what the user picked. Painting the value into a 1x1 canvas lets the browser do
    // the conversion and hands back plain sRGB bytes that THREE.Color can consume.
    const accentCanvas = document.createElement('canvas')
    accentCanvas.width = 1
    accentCanvas.height = 1
    const accentCtx = accentCanvas.getContext('2d', { willReadFrequently: true })
    const resolveAccentBytes = (css: string) => {
      if (!accentCtx || !css) return null
      accentCtx.clearRect(0, 0, 1, 1)
      accentCtx.fillStyle = '#000000'
      accentCtx.fillStyle = css
      if (accentCtx.fillStyle === '#000000' && !css.startsWith('rgb(0, 0, 0')) return null
      accentCtx.fillRect(0, 0, 1, 1)
      const [r, g, b] = accentCtx.getImageData(0, 0, 1, 1).data
      return { r, g, b }
    }

    const initialAccentBytes = resolveAccentBytes(accentCss) || { r: 0, g: 193, b: 106 }
    const accentColor = new THREE.Color().setRGB(
      initialAccentBytes.r / 255,
      initialAccentBytes.g / 255,
      initialAccentBytes.b / 255,
      THREE.SRGBColorSpace
    )

    // Every accent-tinted surface registers here, and the accent is re-read a couple of times per
    // second while the scene runs. Without this the modal keeps the accent it was opened with and a
    // colour picked from the user menu never reaches the 3D view.
    const readAccentCss = () => {
      const probe = document.createElement('span')
      probe.style.color = 'var(--ui-primary, #00c16a)'
      probe.style.display = 'none'
      document.body.appendChild(probe)
      const css = getComputedStyle(probe).color
      probe.remove()
      return css
    }
    let lastAccentCss = accentCss
    const accentFollowers: Array<(c: THREE.Color) => void> = []
    const followAccent = (fn: (c: THREE.Color) => void) => {
      accentFollowers.push(fn)
      fn(accentColor)
    }
    const refreshAccent = () => {
      const css = readAccentCss()
      if (!css || css === lastAccentCss) return
      const bytes = resolveAccentBytes(css)
      if (!bytes) return
      lastAccentCss = css
      accentColor.setRGB(bytes.r / 255, bytes.g / 255, bytes.b / 255, THREE.SRGBColorSpace)
      accentFollowers.forEach((fn) => fn(accentColor))
    }

    // =====================================================================
    // 1. SCENE, RENDERER, CAMERA & CURSOR-TARGETED ZOOM CONTROLS
    // =====================================================================
    const scene = new THREE.Scene()
    scene.background = new THREE.Color(0x0c121c) // lifted off pure black: the cabinet edges read as
      // silhouettes when the room is this dark, so the whole viewer looked dim.
    scene.fog = new THREE.FogExp2(0x06090f, 0.055)

    // ---- Rack geometry spec (1 unit = 1 metre, real cabinet dimensions) ----------
    // 42U floor-standing cabinet: 42 * 0.04445 = 1.8669 m of usable interior, plus a
    // 0.085 m plinth and a 0.075 m roof, so the enclosure stands 2.027 m tall.
    // Everything below derives from these numbers: rails, U numbering, doors, side
    // panels and the camera framing all follow TOTAL_U.
    const U_HEIGHT = 0.04445
    const TOTAL_U = RACK_TOTAL_U
    const RACK_W = 0.60
    const RACK_D = 0.80
    const BASE_H = 0.085
    const TOP_H = 0.075
    const RACK_INNER_H = TOTAL_U * U_HEIGHT
    const TOTAL_RACK_H = BASE_H + RACK_INNER_H + TOP_H

    // Camera framing is expressed as a ratio of the enclosure height (the multipliers are
    // the angles that were tuned for the original shorter cabinet), so a taller rack is not cropped and
    // needs no retuning when the U count changes.
    const CAM_TARGET_Y = TOTAL_RACK_H * 0.587
    const CAM_PAN_Y_MAX = TOTAL_RACK_H * 1.18

    const camera = new THREE.PerspectiveCamera(42, 1, 0.05, 60)
    camera.position.set(TOTAL_RACK_H * 1.182, TOTAL_RACK_H * 0.937, TOTAL_RACK_H * 1.589)

    const renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' })
    renderer.setSize(container.clientWidth, container.clientHeight)
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2))
    renderer.shadowMap.enabled = true
    renderer.shadowMap.type = THREE.PCFSoftShadowMap
    renderer.toneMapping = THREE.ACESFilmicToneMapping
    renderer.toneMappingExposure = 1.05

    // Image-based lighting: a small procedural "room" baked into an env map. Metallic cabinet parts
    // have almost no diffuse response, so without an environment they only ever showed the two
    // directional lights and read as black shapes no matter how much those were cranked up.
    const pmrem = new THREE.PMREMGenerator(renderer)
    const envRT = pmrem.fromScene(new RoomEnvironment(), 0.04)
    scene.environment = envRT.texture
    pmrem.dispose()
    container.appendChild(renderer.domElement)

    const controls = new OrbitControls(camera, renderer.domElement)
    controls.enableDamping = true
    controls.dampingFactor = 0.06
    controls.enablePan = true
    controls.screenSpacePanning = true // Drag atas/bawah menggeser kamera secara vertikal sejajar layar
    controls.panSpeed = 1.0
    controls.mouseButtons = {
      LEFT: THREE.MOUSE.PAN,      // Klik kiri + drag = geser kamera (drag ke atas -> kamera turun / objek naik)
      MIDDLE: THREE.MOUSE.DOLLY,
      RIGHT: THREE.MOUSE.ROTATE   // Klik kanan + drag = putar sudut pandang 3D
    }
    controls.touches = {
      ONE: THREE.TOUCH.PAN,
      TWO: THREE.TOUCH.DOLLY_ROTATE
    }
    controls.enableZoom = false // Disabled default center-zoom; replaced with cursor-targeted zoom below
    controls.minDistance = 0.35
    controls.maxDistance = 6.5
    controls.maxPolarAngle = Math.PI * 0.495
    controls.target.set(0, CAM_TARGET_Y, 0)

    // Batasi area geser (pan) agar kamera tidak terlalu jauh keluar dari area rack
    controls.addEventListener('change', () => {
      const minTarget = new THREE.Vector3(-0.85, 0.05, -0.85)
      const maxTarget = new THREE.Vector3(0.85, CAM_PAN_Y_MAX, 0.85)
      const prevTarget = controls.target.clone()
      controls.target.clamp(minTarget, maxTarget)
      camera.position.add(controls.target.clone().sub(prevTarget))
    })

    // Studio / Data Center Lighting
    const hemiLight = new THREE.HemisphereLight(0xe4f0ff, 0x16243a, 0.85)
    scene.add(hemiLight)

    const keyLight = new THREE.DirectionalLight(0xffffff, 1.7)
    keyLight.position.set(2.8, 4.2, 3.2)
    keyLight.castShadow = true
    keyLight.shadow.mapSize.set(2048, 2048)
    keyLight.shadow.camera.near = 0.5
    keyLight.shadow.camera.far = 12
    const d = 1.8
    keyLight.shadow.camera.left = -d
    keyLight.shadow.camera.right = d
    keyLight.shadow.camera.top = d
    keyLight.shadow.camera.bottom = -d
    keyLight.shadow.bias = -0.0005
    scene.add(keyLight)

    // Cool cyan rim light & subtle warm back fill for metallic edge definition
    // Deliberately NOT accent-coloured: this is the light that shows up as a reflection on the floor
    // and cabinet edges at the angled view, and HIRO wants that sheen to stay white.
    const rimLight = new THREE.DirectionalLight(0xdfe9ff, 0.6)
    rimLight.position.set(-2.5, 2.2, -2.5)
    scene.add(rimLight)

    // A point light this close to the hardware obeys inverse-square falloff, so at close-up zoom the
    // nearest switch rows were getting ten times the light they need and their colours washed out to
    // white. Gentler intensity, longer decay and a wider radius keep the fill even instead.
    const frontFillLight = new THREE.PointLight(0xe8f4ff, 0.2, 6.5)
    frontFillLight.position.set(0, 1.1, 1.6)
    frontFillLight.decay = 1.2
    scene.add(frontFillLight)

    // Broad white wash from the viewer's side. The key light comes from above/right, which left the
    // cabinet face and the equipment inside it reading almost black.
    const faceWash = new THREE.DirectionalLight(0xf2f7ff, 0.55)
    faceWash.position.set(0.8, 1.8, 4.2)
    scene.add(faceWash)

    // =====================================================================
    // 2. DATA CENTER FLOOR & SUBTLE REFLECTIVE GRID
    // =====================================================================
    const floorGeo = new THREE.PlaneGeometry(64, 64)
    const floorMat = new THREE.MeshStandardMaterial({
      color: 0x131c29,
      roughness: 0.55,
      metalness: 0.35
    })
    const floor = new THREE.Mesh(floorGeo, floorMat)
    floor.rotation.x = -Math.PI / 2
    floor.receiveShadow = true
    scene.add(floor)

    // Floor grid, drawn in a shader instead of a GridHelper. Two jobs, kept strictly apart: the grid
    // itself is a still white reference surface, and the theme accent shows up only as a soft halo
    // that swells out from the rack's feet. Tinting the lines as well made the floor read like a
    // highlighter smear, which is what this replaces.
    const FLOOR_GRID_SIZE = 44
    const floorLightUniforms = {
      uTime: { value: 0 },
      uAccent: { value: accentColor.clone() },
      uSize: { value: FLOOR_GRID_SIZE }
    }

    const gridFloorMat = new THREE.ShaderMaterial({
      uniforms: floorLightUniforms,
      transparent: true,
      depthWrite: false,
      vertexShader: `
        varying vec2 vUv;
        void main() {
          vUv = uv;
          gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
        }
      `,
      fragmentShader: `
        uniform float uTime;
        uniform vec3 uAccent;
        uniform float uSize;
        varying vec2 vUv;

        // Anti-aliased grid lines: 1px wide regardless of how far the camera is.
        float gridMask(vec2 p, float cell, float thickness) {
          vec2 coord = p / cell;
          vec2 grid = abs(fract(coord - 0.5) - 0.5) / max(fwidth(coord), vec2(1e-5));
          float line = 1.0 - min(min(grid.x, grid.y) / thickness, 1.0);
          return clamp(line, 0.0, 1.0);
        }

        void main() {
          vec2 p = (vUv - 0.5) * uSize;
          float radius = length(p);

          float minor = gridMask(p, 0.5, 1.0);
          float major = gridMask(p, 2.0, 1.4);

          // Past a few metres the 0.5 m lines are thinner than a pixel and would shimmer into moire.
          minor *= 1.0 - smoothstep(7.0, 16.0, radius);

          // The plane must not show an edge: the grid keeps running far past the rack and only
          // dissolves near the horizon, so no line ever appears to stop in mid-air.
          float fade = 1.0 - smoothstep(uSize * 0.31, uSize * 0.47, radius);

          // ONE ROLE PER ELEMENT. The grid is a still, plain white reference surface and carries no
          // colour at all - tinting the lines is what made the floor look like a highlighter smear.
          // The accent appears exactly once, as a halo leaving the feet of the cabinet.
          float lineAlpha = (minor * 0.22 + major * 0.42) * fade;
          vec3 lineColor = mix(vec3(0.30, 0.31, 0.33), vec3(0.70, 0.72, 0.75), major);

          // A constant, very soft pool under the cabinet so the accent is present even between
          // pulses, plus one halo that swells outward from the feet and dissolves as it widens.
          float pool = exp(-radius * radius * 0.55) * 0.16;

          // 3.5 s per pulse instead of 7 s, and the swell now reaches ~6.5 m instead of 3.6 m, so the
          // halo is clearly moving instead of just breathing in place.
          float phase = fract(uTime / 3.5);
          float ringRadius = phase * 6.5;
          float ringWidth = 0.35 + phase * 1.05;
          float ring = exp(-pow((radius - ringRadius) / ringWidth, 2.0)) * (1.0 - phase) * (1.0 - phase) * 0.34;

          float accentAlpha = clamp(pool + ring, 0.0, 0.55) * fade;

          // White lines sit ON TOP of the halo; the halo never tints them.
          float outAlpha = lineAlpha + accentAlpha * (1.0 - lineAlpha);
          vec3 color = (lineColor * lineAlpha + uAccent * accentAlpha * (1.0 - lineAlpha)) / max(outAlpha, 1e-4);

          gl_FragColor = vec4(color, clamp(outAlpha, 0.0, 0.85));
        }
      `
    })

    followAccent((c) => floorLightUniforms.uAccent.value.copy(c))

    const gridFloor = new THREE.Mesh(new THREE.PlaneGeometry(FLOOR_GRID_SIZE, FLOOR_GRID_SIZE), gridFloorMat)
    gridFloor.rotation.x = -Math.PI / 2
    gridFloor.position.y = 0.0015
    gridFloor.renderOrder = 1
    scene.add(gridFloor)

    // Subtle accent-coloured under-rack glow strip
    const glowPlaneMat = new THREE.MeshBasicMaterial({ color: accentColor.clone(), transparent: true, opacity: 0.14 })
    followAccent((c) => glowPlaneMat.color.copy(c))
    const glowPlane = new THREE.Mesh(new THREE.PlaneGeometry(0.68, 0.86), glowPlaneMat)
    glowPlane.rotation.x = -Math.PI / 2
    glowPlane.position.y = 0.002
    scene.add(glowPlane)

    // =====================================================================
    // 3. PROCEDURAL CANVAS TEXTURES (Brushed Metal, U-Labels, Cisco Branding)
    // =====================================================================
    function createBrushedMetalTexture() {
      const canvas = document.createElement('canvas')
      canvas.width = 256
      canvas.height = 256
      const ctx = canvas.getContext('2d')
      ctx.fillStyle = '#2d3540'
      ctx.fillRect(0, 0, 256, 256)

      for (let y = 0; y < 256; y++) {
        const shade = 40 + Math.floor(Math.random() * 18)
        ctx.fillStyle = `rgb(${shade}, ${shade + 6}, ${shade + 14})`
        ctx.fillRect(0, y, 256, 1)
      }
      const tex = new THREE.CanvasTexture(canvas)
      tex.wrapS = THREE.RepeatWrapping
      tex.wrapT = THREE.RepeatWrapping
      return tex
    }

    function createRailNumberTexture() {
      const canvas = document.createElement('canvas')
      canvas.width = 64
      canvas.height = 64 * TOTAL_U
      const ctx = canvas.getContext('2d')
      ctx.fillStyle = '#1e293b'
      ctx.fillRect(0, 0, 64, canvas.height)

      // One band per U, numbered from the top of the cabinet down, as on a real rack rail.
      const step = canvas.height / TOTAL_U
      for (let i = 0; i < TOTAL_U; i++) {
        const uNum = TOTAL_U - i
        const yTop = i * step

        ctx.strokeStyle = 'rgba(148, 163, 184, 0.35)'
        ctx.lineWidth = 1
        ctx.beginPath()
        ctx.moveTo(0, yTop)
        ctx.lineTo(64, yTop)
        ctx.stroke()

        // Three EIA-310 square cage-nut holes per U
        ctx.fillStyle = '#090d16'
        const cageHoleOffsets = [0.22, 0.5, 0.78]
        cageHoleOffsets.forEach(rel => {
          ctx.fillRect(8, yTop + step * rel - 4, 8, 8)
        })

        ctx.fillStyle = '#94a3b8'
        ctx.font = 'bold 18px "JetBrains Mono", monospace'
        ctx.fillText(String(uNum).padStart(2, '0'), 24, yTop + step * 0.58)
      }

      const tex = new THREE.CanvasTexture(canvas)
      tex.anisotropy = 4
      return tex
    }

    function createOtbFaceTexture() {
      const canvas = document.createElement('canvas')
      canvas.width = 1024
      canvas.height = 96
      const ctx = canvas.getContext('2d')

      // Plate - deliberately a shade lighter than the cabinet interior so each 1U panel reads as
      // its own object instead of merging into one striped column.
      const plate = ctx.createLinearGradient(0, 0, 0, canvas.height)
      plate.addColorStop(0, '#1b2129')
      plate.addColorStop(0.42, '#11161c')
      plate.addColorStop(1, '#0a0d11')
      ctx.fillStyle = plate
      ctx.fillRect(0, 0, canvas.width, canvas.height)

      // Top edge highlight: gives the eye a boundary between neighbouring panels
      ctx.fillStyle = 'rgba(255,255,255,0.07)'
      ctx.fillRect(0, 0, canvas.width, 2)

      // Fine brushed texture
      for (let i = 0; i < 220; i++) {
        ctx.fillStyle = `rgba(255,255,255,${Math.random() * 0.012})`
        ctx.fillRect(Math.random() * canvas.width, 0, 1, canvas.height)
      }

      // Orange wave, traced from the NETVEL plate: low on the left, kink, then a long run right
      // Printed orange, not glowing: with one panel per U a bloom reads as a light bar repeating
      // down the whole rack, which is far louder than the reference plate.
      ctx.strokeStyle = '#e2600c'
      ctx.lineWidth = 8
      ctx.lineJoin = 'round'
      ctx.lineCap = 'round'
      ctx.beginPath()
      ctx.moveTo(canvas.width * 0.055, canvas.height * 0.70)
      ctx.lineTo(canvas.width * 0.40, canvas.height * 0.62)
      ctx.lineTo(canvas.width * 0.47, canvas.height * 0.42)
      ctx.lineTo(canvas.width * 0.78, canvas.height * 0.40)
      ctx.stroke()

      // Wordmark
      ctx.fillStyle = '#ee6a12'
      ctx.font = 'italic 700 28px "Segoe UI", Arial, sans-serif'
      ctx.textAlign = 'right'
      ctx.textBaseline = 'middle'
      ctx.fillText('NETVIEL', canvas.width - 26, canvas.height * 0.50)

      // Mounting screws at both ends
      ;[0.022, 0.978].forEach(fx => {
        ctx.beginPath()
        ctx.fillStyle = '#242a33'
        ctx.arc(canvas.width * fx, canvas.height * 0.5, 6, 0, Math.PI * 2)
        ctx.fill()
      })

      const texture = new THREE.CanvasTexture(canvas)
      texture.colorSpace = THREE.SRGBColorSpace
      texture.anisotropy = 4
      return texture
    }

    function createUpsLcdTexture(vaRating, loadPct, runtimeMins) {
      const canvas = document.createElement('canvas')
      canvas.width = 512
      canvas.height = 192
      const ctx = canvas.getContext('2d')

      // Solid orange backlit LCD with black lettering (HIRO): one flat fill, no gradient.
      ctx.fillStyle = '#f97316'
      ctx.fillRect(0, 0, 512, 192)

      // Bezel: a darker orange so the screen keeps an edge against the chassis face.
      ctx.strokeStyle = '#c2410c'
      ctx.lineWidth = 6
      ctx.strokeRect(4, 4, 504, 184)

      // Top status header
      ctx.fillStyle = '#000000'
      ctx.font = 'bold 24px "JetBrains Mono", monospace'
      ctx.fillText(`APC Smart-UPS ${vaRating}VA`, 22, 40)

      ctx.fillStyle = '#000000'
      ctx.font = 'bold 22px "JetBrains Mono", monospace'
      ctx.fillText('ONLINE // NORMAL', 22, 74)

      // Metrics bar
      ctx.fillStyle = '#000000'
      ctx.font = '600 23px "JetBrains Mono", monospace'
      ctx.fillText(`LOAD:${loadPct}%   BATT:100%`, 22, 118)

      ctx.fillStyle = '#000000'
      ctx.font = '20px "JetBrains Mono", monospace'
      ctx.fillText(`IN:230.4V  OUT:230.0V  ~${runtimeMins}m`, 22, 158)

      const tex = new THREE.CanvasTexture(canvas)
      tex.anisotropy = 4
      return tex
    }

    // Red APC wordmark for the left corner of the UPS face. Drawn as transparent text rather than a
    // filled plate so it sits straight on the chassis like the real silkscreen print.
    function createApcBrandTexture() {
      const canvas = document.createElement('canvas')
      canvas.width = 320
      canvas.height = 132
      const ctx = canvas.getContext('2d')

      ctx.fillStyle = '#d81f26'
      ctx.textAlign = 'center'
      ctx.textBaseline = 'middle'
      // A wide, heavy grotesque: the real mark is noticeably broader than a default bold.
      ctx.font = '900 92px "Segoe UI", "Arial Black", Arial, sans-serif'
      ctx.setTransform(1.08, 0, 0, 1, -12.8, 4)
      ctx.fillText('APC', 160, 64)
      ctx.setTransform(1, 0, 0, 1, 0, 0)

      const tex = new THREE.CanvasTexture(canvas)
      tex.anisotropy = 4
      return tex
    }

    const brushedTex = createBrushedMetalTexture()
    const railNumTex = createRailNumberTexture()

    // Shared Materials
    const matRackSteel = new THREE.MeshStandardMaterial({
      color: 0x11161f,
      roughness: 0.42,
      metalness: 0.82
    })
    const matRackTrim = new THREE.MeshStandardMaterial({
      color: 0x1e293b,
      roughness: 0.35,
      metalness: 0.85
    })
    // Practically matte on purpose. Any polished value here lets the key light and the env map draw
    // a specular band across the chassis, and with the glass door open that lands straight in the
    // camera - the glare HIRO kept seeing. Roughness this high keeps only diffuse shading.
    const matSwitchChassis = new THREE.MeshStandardMaterial({
      map: brushedTex,
      color: 0x39424c,
      roughness: 0.88,
      metalness: 0.12,
      envMapIntensity: 0.12
    })
    const matSwitchBezel = new THREE.MeshStandardMaterial({
      color: 0x2a3340,
      roughness: 0.8,
      metalness: 0.2,
      envMapIntensity: 0.15
    })
    // A muted accent, not a neon strip. This face is the brightest thing on screen at close zoom, and
    // at full accent colour it read as a glaring green bar along every switch.
    // Catalyst faceplate furniture: a lighter brushed rail along the top edge, a slightly raised
    // frame around each 12-port group, and the painted yellow LED band under the groups.
    const matSwitchTopRail = new THREE.MeshStandardMaterial({
      color: 0x3f4a56,
      roughness: 0.62,
      metalness: 0.45,
      envMapIntensity: 0.15
    })
    const matSwitchGroupFrame = new THREE.MeshStandardMaterial({
      color: 0x232b34,
      roughness: 0.8,
      metalness: 0.2,
      envMapIntensity: 0.1
    })
    const matLedBand = new THREE.MeshStandardMaterial({
      color: 0x8a7420,
      roughness: 0.95,
      metalness: 0.05,
      envMapIntensity: 0.05
    })
    const matCiscoTeal = new THREE.MeshStandardMaterial({
      color: accentColor.clone().multiplyScalar(0.38),
      // Recoloured from the followAccent() call right after this block.
      roughness: 0.7,
      metalness: 0.15,
      emissive: accentColor.clone().multiplyScalar(0.12),
      emissiveIntensity: 0.04,
      envMapIntensity: 0.1
    })
    followAccent((c) => {
      matCiscoTeal.color.copy(c).multiplyScalar(0.38)
      matCiscoTeal.emissive.copy(c).multiplyScalar(0.12)
    })
    // 48 of these per switch sit side by side, so their collective brightness is what reads as a
    // glaring band across the switch body. Dark steel, almost no reflectivity.
    const matPortMetal = new THREE.MeshStandardMaterial({
      color: 0x39424e,
      roughness: 0.9,
      metalness: 0.15,
      envMapIntensity: 0.05
    })
    const matPortCavity = new THREE.MeshStandardMaterial({
      color: 0x05070b,
      roughness: 0.85,
      metalness: 0.1
    })
    const matScrewChrome = new THREE.MeshStandardMaterial({
      color: 0xd1d5db,
      roughness: 0.2,
      metalness: 0.95,
      envMapIntensity: 0.4
    })

    // =====================================================================
    // 4. BUILD THE 42U SERVER / NETWORK RACK ENCLOSURE
    // =====================================================================
    // Dimensions come from the rack spec at the top of this script: 0.60 m wide, 0.80 m deep,
    // 1U = 0.04445 m (1.75 in), 42U interior = 1.8669 m, overall 2.027 m with plinth and roof.

    const rackGroup = new THREE.Group()
    scene.add(rackGroup)

    // 4 Heavy-Duty Leveling Feet / Casters
    const footGeo = new THREE.CylinderGeometry(0.024, 0.028, 0.035, 16)
    const footOffsets = [
      [-RACK_W / 2 + 0.05, 0.0175, -RACK_D / 2 + 0.06],
      [ RACK_W / 2 - 0.05, 0.0175, -RACK_D / 2 + 0.06],
      [-RACK_W / 2 + 0.05, 0.0175,  RACK_D / 2 - 0.06],
      [ RACK_W / 2 - 0.05, 0.0175,  RACK_D / 2 - 0.06]
    ]
    footOffsets.forEach(([fx, fy, fz]) => {
      const foot = new THREE.Mesh(footGeo, matRackTrim)
      foot.position.set(fx, fy, fz)
      foot.castShadow = true
      rackGroup.add(foot)
    })

    // Bottom Plinth / Base Frame
    const baseMesh = new THREE.Mesh(new THREE.BoxGeometry(RACK_W, BASE_H - 0.03, RACK_D), matRackSteel)
    baseMesh.position.y = 0.03 + (BASE_H - 0.03) / 2
    baseMesh.castShadow = true
    baseMesh.receiveShadow = true
    rackGroup.add(baseMesh)

    // Top Roof Assembly with Fan Exhaust Louvers
    const topY = BASE_H + RACK_INNER_H + TOP_H / 2
    const topMesh = new THREE.Mesh(new THREE.BoxGeometry(RACK_W, TOP_H, RACK_D), matRackSteel)
    topMesh.position.y = topY
    topMesh.castShadow = true
    topMesh.receiveShadow = true
    rackGroup.add(topMesh)

    // Top Fan Grill Accent Panel
    const topGrill = new THREE.Mesh(
      new THREE.BoxGeometry(0.38, 0.008, 0.48),
      new THREE.MeshStandardMaterial({ color: 0x090d14, roughness: 0.6, metalness: 0.8 })
    )
    topGrill.position.set(0, BASE_H + RACK_INNER_H + TOP_H + 0.003, 0)
    rackGroup.add(topGrill)

    // 4 Outer Corner Posts
    const postW = 0.042
    const postH = RACK_INNER_H
    const postCenterY = BASE_H + RACK_INNER_H / 2
    const cornerPositions = [
      [-RACK_W / 2 + postW / 2, postCenterY, -RACK_D / 2 + postW / 2],
      [ RACK_W / 2 - postW / 2, postCenterY, -RACK_D / 2 + postW / 2],
      [-RACK_W / 2 + postW / 2, postCenterY,  RACK_D / 2 - postW / 2],
      [ RACK_W / 2 - postW / 2, postCenterY,  RACK_D / 2 - postW / 2]
    ]
    cornerPositions.forEach(([px, py, pz]) => {
      const post = new THREE.Mesh(new THREE.BoxGeometry(postW, postH, postW), matRackSteel)
      post.position.set(px, py, pz)
      post.castShadow = true
      post.receiveShadow = true
      rackGroup.add(post)
    })

    // 19-inch EIA-310 Vertical Mounting Rails (Front Left/Right & Rear Left/Right)
    // Standard 19" opening between rail inner edges = 0.45085m; rail centers at +/- 0.233m
    const railOffsetX = 0.233
    const frontRailZ = RACK_D / 2 - 0.068
    const rearRailZ = -RACK_D / 2 + 0.068

    const railFaceMat = new THREE.MeshStandardMaterial({
      map: railNumTex,
      roughness: 0.4,
      metalness: 0.75
    })

    const railSidesX = [-railOffsetX, railOffsetX]
    railSidesX.forEach(rx => {
      // Front vertical mounting rail
      const fRail = new THREE.Mesh(new THREE.BoxGeometry(0.022, RACK_INNER_H, 0.025), matRackTrim)
      fRail.position.set(rx, postCenterY, frontRailZ)
      rackGroup.add(fRail)

      // U-Numbering strip on front face of rail
      const uStrip = new THREE.Mesh(new THREE.PlaneGeometry(0.020, RACK_INNER_H), railFaceMat)
      uStrip.position.set(rx, postCenterY, frontRailZ + 0.0128)
      rackGroup.add(uStrip)

      // Rear vertical mounting rail
      const rRail = new THREE.Mesh(new THREE.BoxGeometry(0.022, RACK_INNER_H, 0.025), matRackTrim)
      rRail.position.set(rx, postCenterY, rearRailZ)
      rackGroup.add(rRail)
    })

    // Removable Side Panels (Left & Right)
    const sidePanelsGroup = new THREE.Group()
    rackGroup.add(sidePanelsGroup)

    const sidePanelDirs = [-1, 1]
    sidePanelDirs.forEach(dir => {
      const panelW = 0.012
      const sidePanel = new THREE.Mesh(
        new THREE.BoxGeometry(panelW, RACK_INNER_H - 0.01, RACK_D - 0.09),
        new THREE.MeshStandardMaterial({ color: 0x151b26, roughness: 0.45, metalness: 0.8 })
      )
      sidePanel.position.set(dir * (RACK_W / 2 - panelW / 2), postCenterY, 0)
      sidePanel.castShadow = true
      sidePanel.receiveShadow = true
      sidePanelsGroup.add(sidePanel)

      // Side panel recessed latch handle
      const latch = new THREE.Mesh(new THREE.BoxGeometry(0.015, 0.07, 0.03), matRackTrim)
      latch.position.set(dir * (RACK_W / 2 - 0.004), postCenterY + 0.35, 0)
      sidePanelsGroup.add(latch)
    })

    // Rear Perforated Steel Door (Static slightly open/vented rear enclosure look)
    const rearDoor = new THREE.Mesh(
      new THREE.BoxGeometry(RACK_W - 0.08, RACK_INNER_H - 0.01, 0.012),
      new THREE.MeshStandardMaterial({ color: 0x11161f, roughness: 0.5, metalness: 0.8, transparent: true, opacity: 0.88 })
    )
    rearDoor.position.set(0, postCenterY, -RACK_D / 2 + 0.012)
    rackGroup.add(rearDoor)

    // =====================================================================
    // 5. REAR VERTICAL PDU & STRUCTURED CABLING
    // =====================================================================
    const pduGroup = new THREE.Group()
    rackGroup.add(pduGroup)

    // Vertical 0U Power Distribution Unit bar on rear right
    const pduBar = new THREE.Mesh(
      new THREE.BoxGeometry(0.042, RACK_INNER_H - 0.08, 0.042),
      new THREE.MeshStandardMaterial({ color: 0x1e293b, roughness: 0.4, metalness: 0.85 })
    )
    pduBar.position.set(0.255, postCenterY, -RACK_D / 2 + 0.11)
    pduGroup.add(pduBar)

    // PDU Digital Ammeter Display (Glowing red/amber LED readout)
    const pduMeter = new THREE.Mesh(
      new THREE.BoxGeometry(0.032, 0.045, 0.008),
      new THREE.MeshStandardMaterial({ color: 0xef4444, emissive: 0xdc2626, emissiveIntensity: 1.4 })
    )
    pduMeter.position.set(0.255, postCenterY + 0.25, -RACK_D / 2 + 0.088)
    pduGroup.add(pduMeter)

    // C13 Outlets along PDU
    for (let i = 0; i < 14; i++) {
      const outlet = new THREE.Mesh(
        new THREE.BoxGeometry(0.026, 0.022, 0.008),
        matPortCavity
      )
      outlet.position.set(0.255, BASE_H + 0.12 + i * 0.062, -RACK_D / 2 + 0.088)
      pduGroup.add(outlet)
    }

    // =====================================================================
    // 6. HINGED FRONT TEMPERED-GLASS DOOR (INTERACTIVE)
    // =====================================================================
    // Hinge pivot placed on the front-left vertical corner so it swings naturally
    const doorPivot = new THREE.Group()
    doorPivot.position.set(-RACK_W / 2 + 0.018, postCenterY, RACK_D / 2 - 0.012)
    rackGroup.add(doorPivot)

    const doorW = RACK_W - 0.036
    const doorH = RACK_INNER_H + 0.01
    const frameThickness = 0.042
    const doorDepth = 0.018

    // Metallic perimeter frame (Left, Right, Top, Bottom stiles)
    const stileLeft = new THREE.Mesh(new THREE.BoxGeometry(frameThickness, doorH, doorDepth), matRackTrim)
    stileLeft.position.set(frameThickness / 2, 0, 0)
    doorPivot.add(stileLeft)

    const stileRight = new THREE.Mesh(new THREE.BoxGeometry(frameThickness, doorH, doorDepth), matRackTrim)
    stileRight.position.set(doorW - frameThickness / 2, 0, 0)
    doorPivot.add(stileRight)

    const railTop = new THREE.Mesh(new THREE.BoxGeometry(doorW - frameThickness * 2, frameThickness, doorDepth), matRackTrim)
    railTop.position.set(doorW / 2, doorH / 2 - frameThickness / 2, 0)
    doorPivot.add(railTop)

    const railBottom = new THREE.Mesh(new THREE.BoxGeometry(doorW - frameThickness * 2, frameThickness, doorDepth), matRackTrim)
    railBottom.position.set(doorW / 2, -doorH / 2 + frameThickness / 2, 0)
    doorPivot.add(railBottom)

    // Tinted Tempered Glass Pane
    const glassMat = new THREE.MeshPhysicalMaterial({
      color: 0x0f172a,
      transparent: true,
      opacity: 0.28,
      roughness: 0.08,
      metalness: 0.1,
      reflectivity: 0.55,
      clearcoat: 0.6,
      clearcoatRoughness: 0.06,
      // The scene now has an environment map. At full strength the pane mirrored the whole bright
      // room, and when the camera zoomed onto the switches that reflection covered the screen as a
      // grey veil - it read as fog sitting in front of the hardware.
      envMapIntensity: 0.22,
      depthWrite: false
    })
    const glassPane = new THREE.Mesh(
      new THREE.BoxGeometry(doorW - frameThickness * 2, doorH - frameThickness * 2, 0.006),
      glassMat
    )
    glassPane.position.set(doorW / 2, 0, 0)
    glassPane.name = 'frontGlassDoor'
    doorPivot.add(glassPane)

    // Subtle accent-coloured vertical pinstripes on glass edge
    const glassStripeMat = new THREE.MeshBasicMaterial({ color: accentColor.clone(), transparent: true, opacity: 0.45 })
    followAccent((c) => glassStripeMat.color.copy(c))
    const glassStripe = new THREE.Mesh(new THREE.BoxGeometry(0.004, doorH - frameThickness * 2, 0.007), glassStripeMat)
    glassStripe.position.set(doorW - frameThickness - 0.012, 0, 0)
    doorPivot.add(glassStripe)

    // Chrome Swing-Handle Lock on Right Stile
    const lockHousing = new THREE.Mesh(new THREE.BoxGeometry(0.022, 0.11, 0.018), matScrewChrome)
    lockHousing.position.set(doorW - frameThickness / 2, 0, 0.014)
    lockHousing.name = 'frontGlassDoor'
    doorPivot.add(lockHousing)

    // =====================================================================
    // 7. DYNAMIC HARDWARE STATE: CISCO 1U SWITCHES & BOTTOM APC UPS
    // =====================================================================
    function getUSlotY(uNumber) {
      return BASE_H + (uNumber - 0.5) * U_HEIGHT
    }

    // Dynamic Equipment Groups inside rackGroup
    const dynamicEquipmentGroup = new THREE.Group()
    rackGroup.add(dynamicEquipmentGroup)

    // UPS State: 'none' | '3000' (2U: U01-U02) | '5000' (3U: U01-U03)
    let upsModel = ['none', '3000', '5000'].includes(initialHud.upsModel ?? '') ? initialHud.upsModel! : '3000'
    let upsSerial = typeof initialHud.upsSerial === 'string' ? initialHud.upsSerial : 'AS2408190442'
    function getUpsHeightU() {
      if (upsModel === '3000') return 2 // U01..U02
      if (upsModel === '5000') return 3 // U01..U03
      return 0
    }

    // Hardware comes from the rack register (props.switches). Placement honours the stored U
    // position; a switch whose position is empty, unparseable, inside the UPS reservation or
    // already taken by another switch is packed into the lowest free U above the UPS, so every
    // real switch still shows up exactly once.
    let nextSwitchSeq = 1
    // U reserved for the default NETVIEL OTB plate. New switches must land BELOW it and never on
    // top of another device, so the allocator skips this slot.
    let otbSlotU = 0
    let switchesData: any[] = []
    let selectedSwitchId: number | null = null

    function seedSwitches(list: NetworkSwitch[]) {
      const upsU = getUpsHeightU()
      const occ = new Set()
      for (let u = 1; u <= upsU; u++) occ.add(u)

      const out = []
      for (const sw of list || []) {
        let u = parseUnitRange(sw.position)?.start ?? 0
        if (!u || u <= upsU || occ.has(u)) {
          u = 0
          for (let cand = upsU + 1; cand <= TOTAL_U; cand++) {
            if (!occ.has(cand)) { u = cand; break }
          }
        }
        if (!u) continue // rack full: 42U cannot hold another 1U switch
        occ.add(u)

        out.push({
          id: sw.id,
          // Distinguishes a real register row from a device invented by the Add button below.
          fromRegister: true,
          u,
          // The register has no model or serial column: the faceplate badge shows the series,
          // the tooltip prints UNASSIGNED, and the port-count line is omitted rather than
          // inventing telemetry that the database does not hold.
          model: 'Catalyst 9300',
          hostname: (sw.hostname || '').trim() || sw.ipAddress || `SW-${String(out.length + 1).padStart(2, '0')}`,
          ip: sw.ipAddress || '',
          sn: '',
          meta: 'Click to Edit',
          // Port LEDs need a stable pattern per row; derived from the record id, never displayed.
          activePorts: 24 + ((Number(sw.id) * 7) % 25)
        })
      }
      nextSwitchSeq = out.length + 1
      return out
    }

    switchesData = seedSwitches(props.switches)
    selectedSwitchId = switchesData[0]?.id ?? null

    // LED InstancedMesh State (Rebuilt cleanly whenever switches are added/removed/moved)
    const LED_COUNT_PER_SWITCH = 48 + 4 + 5 // 57: 48 port-band + 4 uplink + 5 status
    // LEDs are triangles: apex-up for odd port order (1,3,5...), flipped by PI for the even ones.
    const LED_TRI = 0.0034
    const ledShape = new THREE.Shape()
    ledShape.moveTo(0, LED_TRI * 0.58)
    ledShape.lineTo(-LED_TRI * 0.5, -LED_TRI * 0.42)
    ledShape.lineTo(LED_TRI * 0.5, -LED_TRI * 0.42)
    ledShape.closePath()
    const ledGeo = new THREE.ShapeGeometry(ledShape)
    // Unlit and therefore unaffected by exposure: pure white made these the brightest pixels on screen
    // once the door was open. A soft blue-white keeps them reading as LEDs without the glare.
    const ledMat = new THREE.MeshBasicMaterial({ color: 0xc9d6e8 })
    let ledInstancedMesh = null
    let ledStates = []

    const dummyObj = new THREE.Object3D()
    // Palette follows a real Catalyst faceplate: port activity blinks YELLOW, the status block and
    // the SFP+ uplinks blink BLUE, PoE ports sit slightly hotter (orange). Values are muted on
    // purpose - the unlit instances are drawn at their literal colour, so neon read as glare.
    const COLOR_GREEN_ON  = new THREE.Color(0x35e05f) // green: port activity
    const COLOR_GREEN_DIM = new THREE.Color(0x0b3a1c)
    const COLOR_AMBER_ON  = new THREE.Color(0xffb020) // amber: the other half of the random blink
    const COLOR_AMBER_DIM = new THREE.Color(0x3d2600)
    const COLOR_CYAN_ON   = new THREE.Color(0x3d8bff) // blue: status LEDs and SFP+ uplinks
    const COLOR_CYAN_DIM  = new THREE.Color(0x0a1c3d)
    const COLOR_OFF       = new THREE.Color(0x0a0e14)

    let interactiveHitboxes = [] // Switch & UPS hitboxes for Raycaster

    // Fiber & Power Cord Materials
    const matFiberAqua = new THREE.MeshStandardMaterial({ color: 0x06b6d4, roughness: 0.35, metalness: 0.1 })
    const matFiberOrange = new THREE.MeshStandardMaterial({ color: 0xf97316, roughness: 0.35, metalness: 0.1 })
    const matPowerCord = new THREE.MeshStandardMaterial({ color: 0x111827, roughness: 0.75 })
    const matUpsBlack = new THREE.MeshStandardMaterial({ color: 0x12151c, roughness: 0.42, metalness: 0.78 })
    // UPS front badge/screen accent: blue, not the red APC brand colour (HIRO asked for blue).

    function showToast(msg) {
      const toast = $('hud-toast')
      if (!toast) return
      toast.textContent = msg
      toast.classList.remove('hidden')
      clearTimeout(toast._timer)
      toast._timer = setTimeout(() => toast.classList.add('hidden'), 3600)
    }

    // Helper: find free U slots above the UPS
    function getOccupiedSet() {
      const occ = new Set()
      const upsU = getUpsHeightU()
      for (let u = 1; u <= upsU; u++) occ.add(u)
      switchesData.forEach(s => occ.add(s.u))
      return occ
    }

    // A U that carries a brush cable-management bar (the slot directly below an installed switch)
    // is physically taken even though no device sits in it. Shared by the panel builder and the
    // allocator so the two can never disagree.
    function getCableBarSlots(): Set<number> {
      const occ = getOccupiedSet()
      const slots = new Set<number>()
      switchesData.forEach(sw => {
        const belowU = sw.u - 1
        if (belowU >= 1 && !occ.has(belowU)) slots.add(belowU)
      })
      return slots
    }

    function getFreeSlots() {
      const occ = getOccupiedSet()
      const cableBars = getCableBarSlots()
      const free = []
      for (let u = TOTAL_U; u >= 1; u--) {
        // Reserved: occupied by the UPS/switch, the OTB plate, or a cable-management bar
        if (!occ.has(u) && u !== otbSlotU && !cableBars.has(u)) free.push(u)
      }
      return free
    }

    // Enforce strict rule: UPS ALWAYS occupies U01..upsU at the bottom of the rack.
    // If any switches sit in U01..upsU, shift them cleanly into the lowest available U slots above the UPS.
    function enforceBottomUpsRule() {
      const upsU = getUpsHeightU()
      if (upsU === 0) return

      const displaced = switchesData
        .filter(s => s.u <= upsU)
        .sort((a, b) => a.u - b.u)

      if (displaced.length === 0) return

      const occ = new Set()
      for (let u = 1; u <= upsU; u++) occ.add(u)
      switchesData.forEach(s => {
        if (s.u > upsU) occ.add(s.u)
      })

      let shiftedCount = 0
      for (const sw of displaced) {
        let targetU = -1
        for (let candidate = upsU + 1; candidate <= TOTAL_U; candidate++) {
          if (!occ.has(candidate)) {
            targetU = candidate
            break
          }
        }
        if (targetU !== -1) {
          sw.u = targetU
          occ.add(targetU)
          shiftedCount++
        } else {
          // Rack completely full: remove excess switch that cannot fit
          switchesData = switchesData.filter(item => item.id !== sw.id)
        }
      }

      if (shiftedCount > 0) {
        showToast(`Shifted ${shiftedCount} switch(es) upward to keep APC UPS mounted at bottom U01–U0${upsU}.`)
      }
    }

    // Build Bottom APC Smart-UPS 3D Mesh (Always U01..U02 for 3000VA, U01..U03 for 5000VA)
    // Printed legend across the top rail: the Cisco wordmark on the left, the port-group ranges
    // (1x/12x/13x/24x ...) over each group, and the series name on the right. Drawn as a transparent
    // texture so the brushed rail shows through, exactly like the silk-screen on the real faceplate.
    function createSwitchTopLabelTexture(groupT: number[], groupB: number[]) {
      const canvas = document.createElement('canvas')
      canvas.width = 2048
      canvas.height = 96
      const ctx = canvas.getContext('2d')
      if (!ctx) return null as unknown as THREE.Texture

      ctx.clearRect(0, 0, canvas.width, canvas.height)
      ctx.textBaseline = 'middle'
      ctx.fillStyle = 'rgba(226, 235, 244, 0.86)'

      // Cisco wordmark, left end
      ctx.font = '600 40px Inter, sans-serif'
      ctx.textAlign = 'left'
      ctx.fillText('CISCO', 26, 44)

      // "LAN BASE" block between the services block and the first port group
      ctx.font = '600 20px Inter, sans-serif'
      ctx.fillStyle = 'rgba(200, 212, 224, 0.55)'
      ctx.fillText('LAN BASE', 250, 46)

      // Port-range labels sitting on the group edges
      ctx.font = '600 22px Inter, sans-serif'
      ctx.fillStyle = 'rgba(226, 235, 244, 0.72)'
      for (let g = 0; g < groupT.length; g++) {
        ctx.textAlign = 'left'
        ctx.fillText(`${g * 12 + 1}x`, groupT[g] * canvas.width + 4, 44)
        ctx.textAlign = 'right'
        ctx.fillText(`${g * 12 + 12}x`, groupB[g] * canvas.width - 4, 44)
      }

      // Series name, right end
      ctx.textAlign = 'right'
      ctx.font = '500 20px Inter, sans-serif'
      ctx.fillStyle = 'rgba(200, 212, 224, 0.6)'
      ctx.fillText('Catalyst 2960-X Series', canvas.width - 24, 46)

      const tex = new THREE.CanvasTexture(canvas)
      tex.colorSpace = THREE.SRGBColorSpace
      tex.anisotropy = 4
      return tex
    }

    let upsStatusBarMat: THREE.MeshBasicMaterial | null = null
    let upsBlinkTime = 0

    function buildUpsMesh() {
      const upsU = getUpsHeightU()
      if (upsU === 0) return

      const upsGroup = new THREE.Group()
      const bottomY = BASE_H
      const totalH = upsU * U_HEIGHT - 0.002
      const centerY = bottomY + (upsU * U_HEIGHT) / 2
      const zFront = frontRailZ + 0.015
      const chassisDepth = 0.54
      const chassisWidth = 0.442

      upsGroup.position.set(0, centerY, zFront)
      dynamicEquipmentGroup.add(upsGroup)

      // Main heavy steel UPS chassis
      const body = new THREE.Mesh(new THREE.BoxGeometry(chassisWidth, totalH, chassisDepth), matUpsBlack)
      body.position.set(0, 0, -chassisDepth / 2)
      body.castShadow = true
      body.receiveShadow = true
      upsGroup.add(body)

      // Front sculpted bezel
      const bezel = new THREE.Mesh(new THREE.BoxGeometry(chassisWidth + 0.004, totalH, 0.014), matRackSteel)
      bezel.position.set(0, 0, 0.005)
      upsGroup.add(bezel)

      // Heavy Rack Ears & Chrome Bolts
      const upsEarSides = [-1, 1]
      upsEarSides.forEach(side => {
        const ear = new THREE.Mesh(new THREE.BoxGeometry(0.022, totalH, 0.005), matUpsBlack)
        ear.position.set(side * (chassisWidth / 2 + 0.011), 0, 0.003)
        upsGroup.add(ear)

        for (let uIdx = 0; uIdx < upsU; uIdx++) {
          const boltY = -totalH / 2 + (uIdx + 0.5) * U_HEIGHT
          const bolt = new THREE.Mesh(new THREE.CylinderGeometry(0.0038, 0.0038, 0.005, 10), matScrewChrome)
          bolt.rotation.x = Math.PI / 2
          bolt.position.set(side * (chassisWidth / 2 + 0.011), boltY, 0.006)
          upsGroup.add(bolt)
        }
      })

      // Ventilation Intake Louvers on Left & Right Bezel
      const ventBoxOffsetsX = [-0.145, 0.145]
      ventBoxOffsetsX.forEach(vx => {
        const ventBox = new THREE.Mesh(new THREE.BoxGeometry(0.11, totalH * 0.76, 0.016), matPortCavity)
        ventBox.position.set(vx, 0, 0.005)
        upsGroup.add(ventBox)

        const louverCount = upsU * 4
        for (let l = 0; l < louverCount; l++) {
          const ly = -totalH * 0.34 + (l / (louverCount - 1)) * (totalH * 0.68)
          const slat = new THREE.Mesh(new THREE.BoxGeometry(0.105, 0.0022, 0.017), matRackTrim)
          slat.position.set(vx, ly, 0.0055)
          upsGroup.add(slat)
        }
      })

      // APC wordmark on the left of the face: red lettering, no coloured block behind it.
      const apcBadge = new THREE.Mesh(
        new THREE.PlaneGeometry(0.044, 0.018),
        new THREE.MeshBasicMaterial({ map: createApcBrandTexture(), transparent: true, toneMapped: false })
      )
      // Sits slightly proud of z=0.013: the intake-louver box in front of this corner ends exactly on
      // that plane, which was covering the wordmark. The old badge was a 4 mm-deep box, so its face
      // poked through; a flat plane has to be pushed forward to match.
      apcBadge.position.set(-0.185, totalH / 2 - 0.016, 0.0165)
      upsGroup.add(apcBadge)

      // Center Illuminated LCD Status Panel
      const loadPct = Math.min(92, Math.round((switchesData.length * 340 + 180) / (upsModel === '5000' ? 45 : 27)))
      const runtimeMins = upsModel === '5000' ? Math.max(18, 68 - switchesData.length * 3) : Math.max(11, 44 - switchesData.length * 2)
      const lcdTex = createUpsLcdTexture(upsModel, loadPct, runtimeMins)

      // toneMapped:false matters here - the scene's ACES curve desaturates the backlit orange into a
      // cream wash, so the screen bypasses tone mapping and shows the authored colour.
      const lcdScreen = new THREE.Mesh(
        new THREE.PlaneGeometry(0.135, Math.min(0.056, totalH * 0.56)),
        new THREE.MeshBasicMaterial({ map: lcdTex, toneMapped: false })
      )
      lcdScreen.position.set(0, 0.004, 0.013)
      upsGroup.add(lcdScreen)

      // Green online strip below the LCD. Blinking, so the material is kept and pulsed in animate().
      upsStatusBarMat = new THREE.MeshBasicMaterial({ color: 0x10ff70, transparent: true, toneMapped: false })
      const statusBar = new THREE.Mesh(new THREE.BoxGeometry(0.09, 0.004, 0.004), upsStatusBarMat)
      statusBar.position.set(0, -totalH * 0.32, 0.013)
      upsGroup.add(statusBar)

      // Hitbox for Hover Inspection
      const upsHitbox = new THREE.Mesh(
        new THREE.BoxGeometry(0.482, totalH, 0.03),
        new THREE.MeshBasicMaterial({ visible: false })
      )
      upsHitbox.position.set(0, 0, 0.015)
      upsHitbox.userData = {
        kind: 'ups',
        slot: `U01–U0${upsU}`,
        ip: `LOAD ${loadPct}%`,
        sn: upsSerial || 'UNASSIGNED',
        name: `APC Smart-UPS ${upsModel}VA (${upsU}U)`,
        meta: `Battery 100% • 230V Pure Sinewave • ~${runtimeMins} min runtime`
      }
      upsGroup.add(upsHitbox)
      interactiveHitboxes.push(upsHitbox)
    }

    // Rebuild Entire Dynamic Rack Contents (Switches, Cable Bars, UPS, Instanced LEDs)
    // A single NETVIEL OTB panel, mounted in the TOP-most free unit of the cabinet. It is the
    // rack's default first unit - not a filler for every slot - so it never repeats down the rack.
    // Skipped only when the whole cabinet is full.
    function buildOtbPanels() {
      const occupied = getOccupiedSet()
      const cableBarSlots = getCableBarSlots()

      let targetU = 0
      for (let u = TOTAL_U; u >= 1; u--) {
        if (!occupied.has(u) && !cableBarSlots.has(u)) { targetU = u; break }
      }
      otbSlotU = targetU
      if (!targetU) return

      const matBody = new THREE.MeshStandardMaterial({
        color: 0x0b0e13, roughness: 0.9, metalness: 0.08, envMapIntensity: 0.12
      })
      const matFace = new THREE.MeshStandardMaterial({
        map: createOtbFaceTexture(), roughness: 0.82, metalness: 0.05, envMapIntensity: 0.12
      })
      // BoxGeometry takes one material per side: px, nx, py, ny, pz(front), nz
      const faceMats = [matBody, matBody, matBody, matBody, matFace, matBody]

      const panelH = U_HEIGHT - 0.0018
      const panelW = 0.442

      const group = new THREE.Group()
      group.position.set(0, getUSlotY(targetU), frontRailZ + 0.010)

      const body = new THREE.Mesh(new THREE.BoxGeometry(panelW, panelH, 0.150), matBody)
      body.position.set(0, 0, -0.076)
      group.add(body)

      const face = new THREE.Mesh(new THREE.BoxGeometry(panelW, panelH, 0.006), faceMats)
      face.position.set(0, 0, 0.002)
      group.add(face)

      ;[-1, 1].forEach(side => {
        const ear = new THREE.Mesh(new THREE.BoxGeometry(0.021, panelH, 0.004), matBody)
        ear.position.set(side * (panelW / 2 + 0.0105), 0, 0.003)
        group.add(ear)
      })

      group.userData = { kind: 'otb', u: targetU }
      dynamicEquipmentGroup.add(group)
    }

    function rebuildRackEquipment() {
      enforceBottomUpsRule()

      // Clear previous dynamic meshes
      while (dynamicEquipmentGroup.children.length > 0) {
        dynamicEquipmentGroup.remove(dynamicEquipmentGroup.children[0])
      }
      if (ledInstancedMesh) {
        scene.remove(ledInstancedMesh)
        ledInstancedMesh.dispose()
        ledInstancedMesh = null
      }
      interactiveHitboxes = []
      ledStates = []

      // Build Bottom UPS if enabled
      buildUpsMesh()

      // Place 1U Brush Cable Management Bars in free slots directly below switches (if not occupied by another switch or UPS)
      const occupied = getOccupiedSet()
      switchesData.forEach(sw => {
        const belowU = sw.u - 1
        if (belowU >= 1 && !occupied.has(belowU)) {
          const cmGroup = new THREE.Group()
          cmGroup.position.set(0, getUSlotY(belowU), frontRailZ + 0.015)

          const bar = new THREE.Mesh(new THREE.BoxGeometry(0.482, U_HEIGHT * 0.88, 0.014), matRackSteel)
          cmGroup.add(bar)

          const brushSlot = new THREE.Mesh(
            new THREE.BoxGeometry(0.41, U_HEIGHT * 0.42, 0.016),
            new THREE.MeshStandardMaterial({ color: 0x070a0f, roughness: 0.95 })
          )
          cmGroup.add(brushSlot)

          const cableRingOffsets = [-0.18, -0.06, 0.06, 0.18]
          cableRingOffsets.forEach(dx => {
            const ring = new THREE.Mesh(new THREE.BoxGeometry(0.012, U_HEIGHT * 0.72, 0.036), matRackTrim)
            ring.position.set(dx, 0, 0.018)
            cmGroup.add(ring)
          })
          dynamicEquipmentGroup.add(cmGroup)
        }
      })

      buildOtbPanels()

      // Allocate InstancedMesh for all switches' LEDs
      const totalLeds = Math.max(1, switchesData.length * LED_COUNT_PER_SWITCH)
      ledInstancedMesh = new THREE.InstancedMesh(ledGeo, ledMat, totalLeds)
      ledInstancedMesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage)
      ledInstancedMesh.visible = switchesData.length > 0
      scene.add(ledInstancedMesh)

      let ledIndex = 0

      switchesData.forEach((cfg) => {
        const switchGroup = new THREE.Group()
        const yPos = getUSlotY(cfg.u)
        const zFront = frontRailZ + 0.014
        const chassisDepth = 0.36
        const chassisHeight = U_HEIGHT - 0.0018
        const chassisWidth = 0.442
        const isSelected = cfg.id === selectedSwitchId

        switchGroup.position.set(0, yPos, zFront)
        dynamicEquipmentGroup.add(switchGroup)

        // 1. Main Brushed Metal Chassis Body
        const chassis = new THREE.Mesh(
          new THREE.BoxGeometry(chassisWidth, chassisHeight, chassisDepth),
          matSwitchChassis
        )
        chassis.position.set(0, 0, -chassisDepth / 2)
        chassis.castShadow = true
        chassis.receiveShadow = true
        switchGroup.add(chassis)

        // Click/Hover Hitbox
        const hitbox = new THREE.Mesh(
          new THREE.BoxGeometry(0.482, chassisHeight, 0.03),
          new THREE.MeshBasicMaterial({ visible: false })
        )
        hitbox.position.set(0, 0, 0.005)
        hitbox.userData = {
          kind: 'switch',
          switchId: cfg.id,
          slot: `U${String(cfg.u).padStart(2, '0')}`,
          ip: cfg.ip,
          sn: cfg.sn || 'UNASSIGNED',
          name: `${cfg.hostname} — ${cfg.model}`,
          meta: cfg.meta || `${cfg.activePorts}/48 Active Ports • 4x 10G SFP+ • Click to Edit`
        }
        switchGroup.add(hitbox)
        interactiveHitboxes.push(hitbox)

        // 2. Front Faceplate: graphite plate with a lighter brushed rail along the top edge, the way
        //    a Catalyst 2960-X is laid out. Left: services block. Middle: 4 groups of 12 ports, each
        //    with its own raised frame and a painted yellow LED band underneath. Right: uplinks.
        const faceplate = new THREE.Mesh(
          new THREE.BoxGeometry(chassisWidth, chassisHeight, 0.006),
          matSwitchBezel
        )
        faceplate.position.set(0, 0, 0.001)
        switchGroup.add(faceplate)

        const railH = chassisHeight * 0.18
        const topRail = new THREE.Mesh(
          new THREE.BoxGeometry(chassisWidth, railH, 0.0064),
          isSelected ? new THREE.MeshBasicMaterial({ color: 0xfbbf24 }) : matSwitchTopRail
        )
        topRail.position.set(0, chassisHeight / 2 - railH / 2, 0.0013)
        switchGroup.add(topRail)

        // ---- layout -------------------------------------------------------
        const halfW = chassisWidth / 2
        const leftBlockW = 0.072
        const rightBlockW = 0.052
        const groupGap = 0.0072
        const portAreaX = -halfW + leftBlockW
        const groupW = (chassisWidth - leftBlockW - rightBlockW - groupGap * 3) / 4
        const colPitch = groupW / 6
        // Square RJ45 openings, as on the real faceplate
        const portSide = colPitch * 0.78
        const portW = portSide
        const portH = portSide
        const rowY = [chassisHeight * 0.17, -chassisHeight * 0.13]
        const bandY = -chassisHeight * 0.40

        // Group range labels (1x/12x/13x/24x ...) are printed over each group on the top rail.
        const groupT: number[] = []
        const groupB: number[] = []
        for (let g = 0; g < 4; g++) {
          const gx = portAreaX + g * (groupW + groupGap)
          groupT.push((gx + halfW) / chassisWidth)
          groupB.push((gx + groupW + halfW) / chassisWidth)
        }
        const labelTex = createSwitchTopLabelTexture(groupT, groupB)
        if (labelTex) {
          const labelMesh = new THREE.Mesh(
            new THREE.PlaneGeometry(chassisWidth, railH),
            new THREE.MeshBasicMaterial({ map: labelTex, transparent: true })
          )
          labelMesh.position.set(0, chassisHeight / 2 - railH / 2, 0.0047)
          switchGroup.add(labelMesh)
        }

        // ---- left services block: 2 isolated RJ45 + 2 console ports ----
        const svcX = -halfW + 0.021
        const conX = -halfW + 0.040
        for (let row = 0; row < 2; row++) {
          const svcHousing = new THREE.Mesh(new THREE.BoxGeometry(portW, portH, 0.003), matPortMetal)
          svcHousing.position.set(svcX, rowY[row], 0.0042)
          switchGroup.add(svcHousing)

          const svcCavity = new THREE.Mesh(new THREE.BoxGeometry(portW * 0.76, portH * 0.72, 0.0034), matPortCavity)
          svcCavity.position.set(svcX, rowY[row] - 0.0004, 0.0044)
          switchGroup.add(svcCavity)

          const con = new THREE.Mesh(new THREE.BoxGeometry(0.0072, 0.0056, 0.003), matPortMetal)
          con.position.set(conX, rowY[row], 0.0042)
          switchGroup.add(con)
        }

        // Mode button + 5 system LEDs above the services block
        const modeBtn = new THREE.Mesh(new THREE.CylinderGeometry(0.0032, 0.0032, 0.004, 12), matPortMetal)
        modeBtn.rotation.x = Math.PI / 2
        modeBtn.position.set(-halfW + 0.056, chassisHeight * 0.30, 0.0045)
        switchGroup.add(modeBtn)

        // Status LEDs sit level with the yellow port bands (bottom of the ports), not at the top edge.
        for (let sys = 0; sys < 5; sys++) {
          const lx = -halfW + 0.012 + sys * 0.0078
          const ly = bandY
          dummyObj.rotation.z = 0
          dummyObj.position.set(lx, yPos + ly, zFront + 0.0048)
          dummyObj.updateMatrix()
          ledInstancedMesh.setMatrixAt(ledIndex, dummyObj.matrix)
          ledInstancedMesh.setColorAt(ledIndex, sys === 4 ? COLOR_AMBER_ON : COLOR_CYAN_ON)

          ledStates.push({
            type: 'system',
            active: true,
            isAmber: sys === 4,
            phase: Math.random() * Math.PI * 2,
            speed: sys === 1 ? 8.0 : 0.8
          })
          ledIndex++
        }

        // ---- 4 x 12 ports, each group framed with a yellow LED band underneath ----
        for (let g = 0; g < 4; g++) {
          const gx = portAreaX + g * (groupW + groupGap)

          const frame = new THREE.Mesh(
            new THREE.BoxGeometry(groupW + 0.0012, (rowY[0] - rowY[1]) + portSide + 0.005, 0.0012),
            matSwitchGroupFrame
          )
          frame.position.set(gx + groupW / 2, (rowY[0] + rowY[1]) / 2, 0.0035)
          switchGroup.add(frame)

          for (let col = 0; col < 6; col++) {
            const px = gx + colPitch * (col + 0.5)

            for (let row = 0; row < 2; row++) {
              const housing = new THREE.Mesh(new THREE.BoxGeometry(portW, portH, 0.003), matPortMetal)
              housing.position.set(px, rowY[row], 0.0044)
              switchGroup.add(housing)

              const cavity = new THREE.Mesh(
                new THREE.BoxGeometry(portW * 0.76, portH * 0.72, 0.0034),
                matPortCavity
              )
              cavity.position.set(px, rowY[row] - 0.0004, 0.0046)
              switchGroup.add(cavity)
            }
          }

          // Painted yellow band; the blinking LEDs sit inside it, 12 per group.
          const band = new THREE.Mesh(
            new THREE.BoxGeometry(groupW, chassisHeight * 0.085, 0.0009),
            matLedBand
          )
          band.position.set(gx + groupW / 2, bandY, 0.0040)
          switchGroup.add(band)

          for (let i = 0; i < 12; i++) {
            const lx = gx + (i + 0.5) * (colPitch / 2)
            const portOrder = g * 12 + i
            // Triangle apex-up for order 1, apex-down for order 2, alternating through to 48.
            dummyObj.rotation.z = portOrder % 2 === 0 ? 0 : Math.PI
            dummyObj.position.set(lx, yPos + bandY, zFront + 0.0048)
            dummyObj.updateMatrix()
            ledInstancedMesh.setMatrixAt(ledIndex, dummyObj.matrix)

            const portNum = g * 12 + Math.floor(i / 6) * 6 + (i % 6)
            const isPortActive = portNum < cfg.activePorts
            const isPoEPort = isPortActive && Math.random() < 0.45

            ledInstancedMesh.setColorAt(
              ledIndex,
              !isPortActive ? COLOR_OFF : (isPoEPort ? COLOR_AMBER_ON : COLOR_GREEN_ON)
            )

            ledStates.push({
              type: 'rj45',
              active: isPortActive,
              isAmber: isPoEPort,
              phase: Math.random() * Math.PI * 2,
              speed: 4.0 + Math.random() * 14.0,
              burstFreq: 0.7 + Math.random() * 2.5,
              burstOffset: Math.random() * 10.0
            })
            ledIndex++
          }
        }

        // ---- right block: 2 x 2 SFP+ cages + a column of status LEDs ----
        const sfpCols = [halfW - 0.040, halfW - 0.020]
        // Port numbering is row-major: 1 = top-left, 2 = top-right, 3 = bottom-left, 4 = bottom-right.
        const sfpY = [chassisHeight * 0.19, -chassisHeight * 0.14]
        let sfpSeq = 0
        for (let row = 0; row < 2; row++) {
          for (let c = 0; c < 2; c++) {
            const sx = sfpCols[c]
            const sy = sfpY[row]

            const cage = new THREE.Mesh(new THREE.BoxGeometry(0.0105, 0.0110, 0.005), matPortMetal)
            cage.position.set(sx, sy, 0.0048)
            switchGroup.add(cage)

            const sfpHole = new THREE.Mesh(new THREE.BoxGeometry(0.008, 0.0086, 0.0055), matPortCavity)
            sfpHole.position.set(sx, sy, 0.005)
            switchGroup.add(sfpHole)

            const hasTransceiver = sfpSeq < 2
            if (hasTransceiver) {
              const transceiver = new THREE.Mesh(
                new THREE.BoxGeometry(0.0078, 0.0092, 0.015), matScrewChrome
              )
              transceiver.position.set(sx, sy, 0.011)
              switchGroup.add(transceiver)

              const latch = new THREE.Mesh(
                new THREE.BoxGeometry(0.0082, 0.0024, 0.004),
                sfpSeq === 0 ? matFiberAqua : matFiberOrange
              )
              latch.position.set(sx, sy - 0.0035, 0.017)
              switchGroup.add(latch)

              const curve = new THREE.CatmullRomCurve3([
                new THREE.Vector3(sx, sy, 0.017),
                new THREE.Vector3(sx + 0.010, sy - 0.004, 0.040),
                new THREE.Vector3(0.246, sy - 0.012, 0.030),
                new THREE.Vector3(0.252, -0.22, -0.04)
              ])
              const tubeMesh = new THREE.Mesh(
                new THREE.TubeGeometry(curve, 16, 0.0015, 6, false),
                sfpSeq === 0 ? matFiberAqua : matFiberOrange
              )
              switchGroup.add(tubeMesh)
            }

            // One LED per uplink port, sitting exactly under its own cage. Only ports 1 and 3 blink.
            const portNo = sfpSeq + 1
            const blinks = portNo === 1 || portNo === 2
            dummyObj.rotation.z = 0
            dummyObj.position.set(sx, yPos + sy - 0.0068, zFront + 0.0052)
            dummyObj.updateMatrix()
            ledInstancedMesh.setMatrixAt(ledIndex, dummyObj.matrix)
            ledInstancedMesh.setColorAt(ledIndex, blinks ? COLOR_CYAN_ON : COLOR_OFF)
            ledStates.push({
              type: 'sfp',
              active: blinks,
              isAmber: false,
              phase: Math.random() * Math.PI * 2,
              speed: 14.0 + Math.random() * 10.0,
              burstFreq: 1.8 + Math.random() * 2.0,
              burstOffset: Math.random() * 5.0
            })
            ledIndex++
            sfpSeq++
          }
        }

        // 7. Rear Chassis PSUs & Power Cord to Vertical PDU
        const rearZ = -chassisDepth
        const rearFanOffsets = [-0.12, -0.02]
        rearFanOffsets.forEach(fx => {
          const fanGrill = new THREE.Mesh(new THREE.BoxGeometry(0.075, chassisHeight * 0.78, 0.004), matPortCavity)
          fanGrill.position.set(fx, 0, rearZ - 0.001)
          switchGroup.add(fanGrill)
        })

        const psuModule = new THREE.Mesh(new THREE.BoxGeometry(0.095, chassisHeight * 0.82, 0.012), matRackTrim)
        psuModule.position.set(0.14, 0, rearZ - 0.004)
        switchGroup.add(psuModule)

        const psuLed = new THREE.Mesh(new THREE.BoxGeometry(0.004, 0.004, 0.004), new THREE.MeshBasicMaterial({ color: 0x10ff70 }))
        psuLed.position.set(0.175, 0.008, rearZ - 0.01)
        switchGroup.add(psuLed)

        const cordCurve = new THREE.CatmullRomCurve3([
          new THREE.Vector3(0.13, 0, rearZ - 0.01),
          new THREE.Vector3(0.17, -0.015, rearZ - 0.055),
          new THREE.Vector3(0.245, -0.02, rearZ - 0.05)
        ])
        const cordMesh = new THREE.Mesh(new THREE.TubeGeometry(cordCurve, 12, 0.0032, 6, false), matPowerCord)
        switchGroup.add(cordMesh)
      })

      ledInstancedMesh.instanceMatrix.needsUpdate = true
      if (ledInstancedMesh.instanceColor) ledInstancedMesh.instanceColor.needsUpdate = true

      syncHudUI()
    }

    // Fast live update of a single switch's 3D faceplate label without rebuilding geometry
    function updateSingleSwitch3DLabel(sw) {
      const hb = interactiveHitboxes.find(h => h.userData.switchId === sw.id)
      if (hb) {
        hb.userData.ip = sw.ip
        hb.userData.sn = sw.sn || 'UNASSIGNED'
        hb.userData.name = `${sw.hostname} — ${sw.model}`
      }
    }

    // Clipboard helper (safe inside sandboxed iframes)
    function copyToClipboard(text, label) {
      if (!text) return
      const temp = document.createElement('textarea')
      temp.value = text
      temp.style.position = 'fixed'
      temp.style.opacity = '0'
      document.body.appendChild(temp)
      temp.select()
      try {
        document.execCommand('copy')
        showToast(`Copied ${label}: ${text}`)
      } catch (err) {
        showToast(`Unable to copy ${label}`)
      }
      document.body.removeChild(temp)
    }

    // =====================================================================
    // 8. UI SYNCHRONIZATION, HARDWARE CRUD & CURSOR-TARGETED ZOOM
    // =====================================================================
    const inputHostname = $('input-hostname')
    const inputIp = $('input-ip')
    const inputSwitchSn = $('input-switch-sn')
    const inputUpsSn = $('input-ups-sn')
    const upsSnWrapper = $('ups-sn-wrapper')
    const selectSlot = $('select-slot')
    const editorCard = $('switch-editor-card')
    const editorTitle = $('editor-title')
    const inventoryList = $('inventory-list')

    function syncHudUI() {
      const swCount = switchesData.length
      const upsU = getUpsHeightU()
      const freeU = TOTAL_U - swCount - upsU

      $('free-u-count').textContent = `${freeU}U Free`

      // Update UPS Buttons, Slot Badge & Serial Input
      const upsBadge = $('ups-slot-badge')
      upsBadge.textContent = upsU > 0 ? `U01–U0${upsU} (${upsU}U)` : 'Not Installed'
      if (upsU > 0) {
        upsSnWrapper.classList.remove('hidden')
        if (document.activeElement !== inputUpsSn) inputUpsSn.value = upsSerial
      } else {
        upsSnWrapper.classList.add('hidden')
      }

      document.querySelectorAll('.ups-btn').forEach(btn => {
        const val = btn.getAttribute('data-ups')
        btn.className = val === upsModel
          ? 'ups-btn py-1.5 px-2 rounded-lg text-[11px] font-semibold bg-primary/25 text-primary border border-primary/50 transition'
          : 'ups-btn py-1.5 px-2 rounded-lg text-[11px] font-medium bg-slate-800 text-slate-400 border border-slate-700 hover:text-white transition'
      })

      // Sync Selected Switch Editor
      let selected = switchesData.find(s => s.id === selectedSwitchId)
      if (!selected && switchesData.length > 0) {
        selected = switchesData[0]
        selectedSwitchId = selected.id
      }

      if (selected) {
        editorCard.classList.remove('hidden')
        editorTitle.textContent = `Switch Asset (U${String(selected.u).padStart(2, '0')})`
        if (document.activeElement !== inputHostname) inputHostname.value = selected.hostname
        if (document.activeElement !== inputIp) inputIp.value = selected.ip
        if (document.activeElement !== inputSwitchSn) inputSwitchSn.value = selected.sn || ''

        // Populate slot dropdown (current U + all free U's above UPS)
        const freeSlots = getFreeSlots()
        const availableOptions = [...new Set([selected.u, ...freeSlots])].sort((a, b) => b - a)
        selectSlot.innerHTML = availableOptions
          .map(u => `<option value="${u}" ${u === selected.u ? 'selected' : ''}>U${String(u).padStart(2, '0')}</option>`)
          .join('')
      } else {
        editorCard.classList.add('hidden')
      }

      // Build Sorted Inventory List (U24 down to U01)
      const sortedSwitches = [...switchesData].sort((a, b) => b.u - a.u)
      let html = sortedSwitches.map(sw => {
        const active = sw.id === selectedSwitchId
        return `
                    <div draggable="true" data-select-id="${sw.id}" class="inv-row flex items-center justify-between gap-2 px-2.5 py-2 rounded-xl border cursor-grab active:cursor-grabbing transition ${
                        active
                            ? 'bg-primary/15 border-primary/50 text-white'
                            : 'bg-slate-900/60 border-slate-800 hover:border-slate-700 text-slate-300'
                    }">
                        <div class="text-slate-500 hover:text-primary shrink-0" title="Drag to reorder">
                            <svg xmlns="http://www.w3.org/2000/svg" class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 8h16M4 16h16" />
                            </svg>
                        </div>
                        <div class="min-w-0 flex-1">
                            <div class="flex items-center gap-1.5">
                                <span class="text-[10px] font-mono font-bold px-1.5 py-0.2 rounded bg-slate-800 text-primary">U${String(sw.u).padStart(2, '0')}</span>
                                <span class="text-xs font-mono font-semibold truncate">${sw.hostname}</span>
                            </div>
                            <div class="flex items-center gap-1.5 mt-0.5 pl-0.5 flex-wrap">
                                <span class="text-[11px] font-mono text-amber-300/90">${sw.ip}</span>
                                <span class="text-[10px] font-mono px-1.5 py-0.2 rounded bg-slate-950/90 text-emerald-300 border border-slate-700/80">S/N: ${sw.sn || 'N/A'}</span>
                            </div>
                        </div>
                        <button data-remove-id="${sw.id}" title="Remove Switch" class="p-1.5 rounded-lg text-slate-400 hover:text-rose-400 hover:bg-rose-500/15 transition">
                            <svg xmlns="http://www.w3.org/2000/svg" class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                            </svg>
                        </button>
                    </div>
                `
      }).join('')

      if (upsU > 0) {
        html += `
                    <div class="flex items-center justify-between gap-2 px-2.5 py-2 rounded-xl bg-emerald-950/35 border border-emerald-500/30">
                        <div class="min-w-0 flex-1">
                            <div class="flex items-center gap-1.5">
                                <span class="text-[10px] font-mono font-bold px-1.5 py-0.2 rounded bg-emerald-500/20 text-emerald-300">U01–U0${upsU}</span>
                                <span class="text-xs font-semibold text-emerald-200 truncate">APC Smart-UPS ${upsModel}VA</span>
                            </div>
                            <div class="flex items-center gap-1.5 mt-0.5 flex-wrap">
                                <span class="text-[10px] font-mono text-emerald-400/80">${upsU}U Backup</span>
                                <span class="text-[10px] font-mono px-1.5 py-0.2 rounded bg-slate-950/90 text-emerald-300 border border-emerald-500/30">S/N: ${upsSerial || 'N/A'}</span>
                            </div>
                        </div>
                        <button id="btn-remove-ups-inv" title="Remove UPS" class="p-1.5 rounded-lg text-emerald-300/70 hover:text-rose-400 hover:bg-rose-500/15 transition">
                            <svg xmlns="http://www.w3.org/2000/svg" class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                            </svg>
                        </button>
                    </div>
                `
      }

      inventoryList.innerHTML = html

      // Attach row click, drag-and-drop sorting & remove listeners
      let draggedSwitchId = null

      inventoryList.querySelectorAll('.inv-row').forEach(row => {
        row.addEventListener('click', (e) => {
          if (e.target.closest('[data-remove-id]')) return
          selectedSwitchId = Number(row.getAttribute('data-select-id'))
          rebuildRackEquipment()
        })

        row.addEventListener('dragstart', (e) => {
          draggedSwitchId = Number(row.getAttribute('data-select-id'))
          row.classList.add('opacity-45', 'border-primary')
          if (e.dataTransfer) {
            e.dataTransfer.effectAllowed = 'move'
            e.dataTransfer.setData('text/plain', String(draggedSwitchId))
          }
        })

        row.addEventListener('dragend', () => {
          draggedSwitchId = null
          inventoryList.querySelectorAll('.inv-row').forEach(el => {
            el.classList.remove('opacity-45', 'border-primary', 'border-t-2', 'border-t-primary', 'border-b-2', 'border-b-primary')
          })
        })

        row.addEventListener('dragover', (e) => {
          e.preventDefault()
          const targetId = Number(row.getAttribute('data-select-id'))
          if (draggedSwitchId === null || draggedSwitchId === targetId) return

          const rect = row.getBoundingClientRect()
          const isTopHalf = (e.clientY - rect.top) < rect.height / 2
          row.classList.toggle('border-t-2', isTopHalf)
          row.classList.toggle('border-t-primary', isTopHalf)
          row.classList.toggle('border-b-2', !isTopHalf)
          row.classList.toggle('border-b-primary', !isTopHalf)
        })

        row.addEventListener('dragleave', () => {
          row.classList.remove('border-t-2', 'border-t-primary', 'border-b-2', 'border-b-primary')
        })

        row.addEventListener('drop', (e) => {
          e.preventDefault()
          const targetId = Number(row.getAttribute('data-select-id'))
          if (draggedSwitchId === null || draggedSwitchId === targetId) return

          // Ambil urutan switch saat ini dari atas ke bawah (U tertinggi -> terendah)
          const ordered = [...switchesData].sort((a, b) => b.u - a.u)
          // Simpan daftar slot U yang sedang dipakai (tetap berurutan dari atas ke bawah)
          const activeUSlots = ordered.map(s => s.u)

          const fromIndex = ordered.findIndex(s => s.id === draggedSwitchId)
          let toIndex = ordered.findIndex(s => s.id === targetId)
          if (fromIndex === -1 || toIndex === -1) return

          const rect = row.getBoundingClientRect()
          const isTopHalf = (e.clientY - rect.top) < rect.height / 2
          if (!isTopHalf && fromIndex > toIndex) toIndex += 1
          if (isTopHalf && fromIndex < toIndex) toIndex -= 1

          const [movedItem] = ordered.splice(fromIndex, 1)
          ordered.splice(toIndex, 0, movedItem)

          // Terapkan kembali slot U ke urutan switch yang baru sehingga model 3D langsung ikut berpindah
          ordered.forEach((sw, idx) => {
            sw.u = activeUSlots[idx]
          })

          selectedSwitchId = movedItem.id
          showToast(`Moved ${movedItem.hostname} to U${String(movedItem.u).padStart(2, '0')}.`)
          rebuildRackEquipment()
        })
      })

      inventoryList.querySelectorAll('[data-remove-id]').forEach(btn => {
        btn.addEventListener('click', (e) => {
          e.stopPropagation()
          removeSwitchById(Number(btn.getAttribute('data-remove-id')))
        })
      })

      const btnRemoveUpsInv = $('btn-remove-ups-inv')
      if (btnRemoveUpsInv) {
        btnRemoveUpsInv.addEventListener('click', () => {
          upsModel = 'none'
          queueHudSettings()
          rebuildRackEquipment()
        })
      }
    }

    // Mirror an edit into the register. Typing debounces; slot moves go out immediately.
    let persistTimer: ReturnType<typeof setTimeout> | null = null
    function persistSwitch(sw: any, immediate = false) {
      if (!sw || typeof sw.id !== 'number' || sw.id < 0) return // creation still in flight
      const payload = {
        id: sw.id as number,
        ipAddress: sw.ip,
        hostname: sw.hostname,
        position: `U${String(sw.u).padStart(2, '0')}`
      }
      const send = () => emit('save-switch', payload)
      if (persistTimer) clearTimeout(persistTimer)
      if (immediate) { send(); return }
      persistTimer = setTimeout(send, 700)
    }

    function removeSwitchById(id) {
      const target = switchesData.find(s => s.id === id)
      if (!target) return

      // A register row cannot be deleted here: this list is only a mirror of the rack detail, so
      // closing and reopening the modal re-reads it and the switch came back. Ask the page to
      // delete it in the database; the row leaves this list when the refreshed props arrive.
      if ((target as any).fromRegister) {
        // No toast here: the page answers with a confirmation dialog that names the switch, so a
        // "removing…" banner in this panel would announce a delete that has not been confirmed yet.
        emit('delete-switch', target.id)
        return
      }

      switchesData = switchesData.filter(s => s.id !== id)
      if (selectedSwitchId === id) {
        selectedSwitchId = switchesData.length > 0 ? switchesData[0].id : null
      }
      showToast(`Removed ${target.hostname} from U${String(target.u).padStart(2, '0')}.`)
      rebuildRackEquipment()
    }

    // Live Hostname, IP & Serial Number Input Listeners
    inputHostname.addEventListener('input', (e) => {
      const sw = switchesData.find(s => s.id === selectedSwitchId)
      if (!sw) return
      sw.hostname = e.target.value.trim() || 'SW-UNNAMED'
      updateSingleSwitch3DLabel(sw)
      syncHudUI()
      persistSwitch(sw)
    })

    inputIp.addEventListener('input', (e) => {
      const sw = switchesData.find(s => s.id === selectedSwitchId)
      if (!sw) return
      sw.ip = e.target.value.trim() || '0.0.0.0'
      updateSingleSwitch3DLabel(sw)
      syncHudUI()
      persistSwitch(sw)
    })

    inputSwitchSn.addEventListener('input', (e) => {
      const sw = switchesData.find(s => s.id === selectedSwitchId)
      if (!sw) return
      sw.sn = e.target.value.toUpperCase().trim()
      updateSingleSwitch3DLabel(sw)
      syncHudUI()
    })

    inputUpsSn.addEventListener('input', (e) => {
      upsSerial = e.target.value.toUpperCase().trim()
      const upsHb = interactiveHitboxes.find(h => h.userData.kind === 'ups')
      if (upsHb) upsHb.userData.sn = upsSerial || 'UNASSIGNED'
      syncHudUI()
      queueHudSettings()
    })

    $('btn-copy-sw-sn').addEventListener('click', () => {
      const sw = switchesData.find(s => s.id === selectedSwitchId)
      if (sw) copyToClipboard(sw.sn, `${sw.hostname} S/N`)
    })

    $('btn-copy-ups-sn').addEventListener('click', () => {
      copyToClipboard(upsSerial, 'APC UPS S/N')
    })

    selectSlot.addEventListener('change', (e) => {
      const sw = switchesData.find(s => s.id === selectedSwitchId)
      if (!sw) return
      sw.u = Number(e.target.value)
      rebuildRackEquipment()
      persistSwitch(sw, true)
    })

    $('btn-delete-selected').addEventListener('click', () => {
      if (selectedSwitchId !== null) removeSwitchById(selectedSwitchId)
    })

    // Add Cisco 1U Switch Button
    $('btn-add-switch').addEventListener('click', () => {
      const freeSlots = getFreeSlots()
      if (freeSlots.length === 0) {
        showToast('Rack is 100% full! Remove a switch or UPS to free a U slot.')
        return
      }
      // Highest free U that is still free: the rack fills from the top downwards, and because
      // getFreeSlots() already reserves the OTB slot, the new device always lands just BELOW the
      // OTB plate and never on top of an existing device.
      const chosenU = Math.max(...freeSlots)

      // A device invented here gets a NEGATIVE id while the POST is in flight. Register ids are
      // small positive numbers, and a local device that reused one made Remove delete the wrong row.
      const seq = nextSwitchSeq++
      const newId = -seq
      const usedIps = new Set(switchesData.map(s => s.ip))
      let hostOctet = 11
      while (hostOctet < 250 && usedIps.has(`10.10.10.${hostOctet}`)) hostOctet++
      const newSwitch = {
        id: newId,
        u: chosenU,
        model: 'Catalyst 9300-48P',
        hostname: `SW-EDGE-${String(seq).padStart(2, '0')}`,
        ip: `10.10.10.${hostOctet}`,
        sn: `FCW26${String(10 + seq).padStart(2, '0')}X${String(seq).padStart(2, '0')}Z`,
        activePorts: 36 + (seq % 11)
      }
      switchesData.push(newSwitch)
      selectedSwitchId = newId
      showToast(`Adding ${newSwitch.hostname} (${newSwitch.ip}) at U${String(chosenU).padStart(2, '0')}…`)
      rebuildRackEquipment()
      // Persist through the page: a switch that only lives in this list disappears as soon as the
      // modal is closed and the rack detail is re-read.
      emit('save-switch', {
        id: null,
        ipAddress: newSwitch.ip,
        hostname: newSwitch.hostname,
        position: `U${String(chosenU).padStart(2, '0')}`
      })
    })

    // UPS Model Selection Buttons (None / 3000VA / 5000VA)
    document.querySelectorAll('.ups-btn').forEach(btn => {
      btn.addEventListener('click', () => {
        const chosen = btn.getAttribute('data-ups')
        if (chosen === upsModel) return
        upsModel = chosen
        rebuildRackEquipment()
        queueHudSettings()
      })
    })

    // Drawer Tab Switching
    const tabHardware = $('tab-hardware')
    const tabRack = $('tab-rack')
    const viewHardware = $('view-hardware')
    const viewRack = $('view-rack')

    tabHardware.addEventListener('click', () => {
      viewHardware.classList.remove('hidden')
      viewRack.classList.add('hidden')
      tabHardware.className = 'flex-1 py-1.5 text-xs font-semibold rounded-lg bg-primary/20 text-primary border border-primary/30 transition'
      tabRack.className = 'flex-1 py-1.5 text-xs font-medium rounded-lg text-slate-400 hover:text-slate-200 transition'
    })

    tabRack.addEventListener('click', () => {
      viewRack.classList.remove('hidden')
      viewHardware.classList.add('hidden')
      tabRack.className = 'flex-1 py-1.5 text-xs font-semibold rounded-lg bg-primary/20 text-primary border border-primary/30 transition'
      tabHardware.className = 'flex-1 py-1.5 text-xs font-medium rounded-lg text-slate-400 hover:text-slate-200 transition'
    })

    // =====================================================================
    // 8A. ENCLOSURE & TELEMETRY CONTROLS
    // =====================================================================
    let isDoorOpen = false
    let targetDoorAngle = 0
    let areSidesVisible = true
    let isTrafficRunning = true
    let trafficIntensity = 0.75

    const btnDoor = $('btn-door')
    const btnSides = $('btn-sides')
    const btnTraffic = $('btn-traffic')
    const btnAutoRotate = $('btn-autorotate')
    const sliderIntensity = $('slider-intensity')
    const intensityVal = $('intensity-val')

    function toggleDoor() {
      isDoorOpen = !isDoorOpen
      targetDoorAngle = isDoorOpen ? -Math.PI * 0.62 : 0
      btnDoor.textContent = isDoorOpen ? 'Open (Click to Close)' : 'Closed (Click to Open)'
      btnDoor.className = isDoorOpen
        ? 'px-3 py-1.5 rounded-xl text-xs font-medium bg-amber-500/20 text-amber-300 border border-amber-500/40 hover:bg-amber-500/30 transition'
        : 'px-3 py-1.5 rounded-xl text-xs font-medium bg-primary/20 text-primary border border-primary/40 hover:bg-primary/30 transition'
      queueHudSettings()
    }

    btnDoor.addEventListener('click', toggleDoor)

    btnSides.addEventListener('click', () => {
      areSidesVisible = !areSidesVisible
      sidePanelsGroup.visible = areSidesVisible
      btnSides.textContent = areSidesVisible ? 'Installed' : 'Removed (Open)'
      btnSides.className = areSidesVisible
        ? 'px-3 py-1.5 rounded-xl text-xs font-medium bg-slate-800 text-slate-300 border border-slate-700 hover:bg-slate-700 transition'
        : 'px-3 py-1.5 rounded-xl text-xs font-medium bg-primary/20 text-primary border border-primary/40 hover:bg-primary/30 transition'
      queueHudSettings()
    })

    btnTraffic.addEventListener('click', () => {
      isTrafficRunning = !isTrafficRunning
      btnTraffic.textContent = isTrafficRunning ? 'Active' : 'Paused'
      btnTraffic.className = isTrafficRunning
        ? 'px-3 py-1.5 rounded-xl text-xs font-medium bg-emerald-500/20 text-emerald-300 border border-emerald-500/40 hover:bg-emerald-500/30 transition'
        : 'px-3 py-1.5 rounded-xl text-xs font-medium bg-rose-500/20 text-rose-300 border border-rose-500/40 hover:bg-rose-500/30 transition'
      queueHudSettings()
    })

    btnAutoRotate.addEventListener('click', () => {
      controls.autoRotate = !controls.autoRotate
      controls.autoRotateSpeed = 1.4
      btnAutoRotate.textContent = controls.autoRotate ? 'On' : 'Off'
      btnAutoRotate.className = controls.autoRotate
        ? 'px-3 py-1.5 rounded-xl text-xs font-medium bg-primary/20 text-primary border border-primary/40 hover:bg-primary/30 transition'
        : 'px-3 py-1.5 rounded-xl text-xs font-medium bg-slate-800 text-slate-300 border border-slate-700 hover:bg-slate-700 transition'
      queueHudSettings()
    })

    sliderIntensity.addEventListener('input', (e) => {
      trafficIntensity = Number(e.target.value) / 100
      intensityVal.textContent = `${e.target.value}%`
      queueHudSettings()
    })

    // Persist the panel's own state per rack. Without this the UPS, the door/side-panel toggles and
    // the traffic settings were plain variables and every close/reopen reset them.
    let hudTimer: ReturnType<typeof setTimeout> | null = null
    function hudSnapshot(): string {
      return JSON.stringify({
        upsModel,
        upsSerial,
        doorOpen: isDoorOpen,
        sidesVisible: areSidesVisible,
        traffic: isTrafficRunning,
        autoRotate: !!controls?.autoRotate,
        intensity: Math.round(trafficIntensity * 100)
      })
    }
    function queueHudSettings() {
      if (hudTimer) clearTimeout(hudTimer)
      hudTimer = setTimeout(() => emit('save-settings', hudSnapshot()), 400)
    }

    // Replay the stored settings onto the already-built scene, reusing the same handlers so the
    // button labels and colours stay in sync with the state.
    function applyStoredHudSettings() {
      if (initialHud.doorOpen === true && !isDoorOpen) toggleDoor()
      if (initialHud.sidesVisible === false && areSidesVisible) btnSides.click()
      if (initialHud.traffic === false && isTrafficRunning) btnTraffic.click()
      if (initialHud.autoRotate === true && !controls.autoRotate) btnAutoRotate.click()
      if (typeof initialHud.intensity === 'number' && initialHud.intensity >= 5 && initialHud.intensity <= 100) {
        sliderIntensity.value = String(initialHud.intensity)
        trafficIntensity = initialHud.intensity / 100
        intensityVal.textContent = `${initialHud.intensity}%`
      }
      inputUpsSn.value = upsSerial
    }

    applyStoredHudSettings()

    // Mobile HUD toggle
    const toggleHudBtn = $('toggle-hud-btn')
    const controlPanel = $('control-panel')
    toggleHudBtn.addEventListener('click', () => {
      controlPanel.classList.toggle('hidden')
      controlPanel.classList.toggle('flex')
    })

    // Smooth Camera Transition Presets
    let camTween = null
    // Wide shots scale with the cabinet; the two close-ups stay absolute because the UPS and
    // the switches are still mounted at the bottom of the rack (UPS at U01, switches above it).
    const presets = {
      angled: { pos: new THREE.Vector3(TOTAL_RACK_H * 1.182, TOTAL_RACK_H * 0.937, TOTAL_RACK_H * 1.589), target: new THREE.Vector3(0, CAM_TARGET_Y, 0) },
      front:  { pos: new THREE.Vector3(0, TOTAL_RACK_H * 0.636, TOTAL_RACK_H * 1.900),                      target: new THREE.Vector3(0, CAM_TARGET_Y, 0) },
      ports:  { pos: new THREE.Vector3(0.18, 0.92, 0.92), target: new THREE.Vector3(0, 0.88, 0.32) },
      ups:    { pos: new THREE.Vector3(0.45, 0.32, 1.15), target: new THREE.Vector3(0, 0.16, 0.25) }
    }

    document.querySelectorAll('.cam-btn').forEach(btn => {
      btn.addEventListener('click', () => {
        const key = btn.getAttribute('data-cam')
        const preset = presets[key]
        if (!preset) return

        if ((key === 'ports' || key === 'ups') && !isDoorOpen) {
          toggleDoor()
        }

        camTween = {
          startPos: camera.position.clone(),
          endPos: preset.pos.clone(),
          startTarget: controls.target.clone(),
          endTarget: preset.target.clone(),
          progress: 0
        }

        document.querySelectorAll('.cam-btn').forEach(b => {
          b.className = 'cam-btn px-3.5 py-2 rounded-xl text-xs font-medium text-slate-300 hover:bg-slate-800/80 transition whitespace-nowrap'
        })
        btn.className = 'cam-btn active px-3.5 py-2 rounded-xl text-xs font-medium bg-primary text-slate-950 font-semibold shadow-lg shadow-primary/25 transition whitespace-nowrap'
      })
    })

    // =========================================================================
    // 8B. CURSOR-TARGETED ZOOM (ZOOM FOLLOWS MOUSE POINTER)
    // =========================================================================
    const zoomRaycaster = new THREE.Raycaster()
    const zoomPointer = new THREE.Vector2()
    const fallbackZoomPlane = new THREE.Plane()
    const planeNormal = new THREE.Vector3()

    function onWheel(e) {
      e.preventDefault()
      camTween = null // Cancel preset tween if user manually zooms

      const rect = renderer.domElement.getBoundingClientRect()
      zoomPointer.x = ((e.clientX - rect.left) / rect.width) * 2 - 1
      zoomPointer.y = -((e.clientY - rect.top) / rect.height) * 2 + 1

      zoomRaycaster.setFromCamera(zoomPointer, camera)

      // Raycast against rack hardware & enclosure first; fallback to a plane facing the camera through controls.target
      const hits = zoomRaycaster.intersectObjects(rackGroup.children, true)
      const focusPoint = new THREE.Vector3()

      if (hits.length > 0) {
        focusPoint.copy(hits[0].point)
      } else {
        camera.getWorldDirection(planeNormal)
        fallbackZoomPlane.setFromNormalAndCoplanarPoint(planeNormal, controls.target)
        if (!zoomRaycaster.ray.intersectPlane(fallbackZoomPlane, focusPoint)) {
          focusPoint.copy(controls.target)
        }
      }

      // Zoom factor (< 1 zooms in, > 1 zooms out)
      const zoomScale = e.deltaY < 0 ? 0.86 : 1.16
      const currentDist = camera.position.distanceTo(controls.target)
      const nextDist = THREE.MathUtils.clamp(currentDist * zoomScale, controls.minDistance, controls.maxDistance)
      const actualScale = nextDist / currentDist

      // Shift both camera.position and controls.target proportionally toward the 3D point under the mouse cursor
      const shiftFactor = 1 - actualScale
      const targetDelta = focusPoint.clone().sub(controls.target).multiplyScalar(shiftFactor)

      // Keep target bounded near the rack so user doesn't drift off into empty space. The vertical
      // limit is derived from the cabinet height (CAM_PAN_Y_MAX): a hardcoded 1.35 was left over
      // from the 24U model, so on the 42U rack - 2.03 m tall - the target could never follow the
      // cursor above 1.35 m and zooming near the top of the rack appeared to ignore the pointer.
      const newTarget = controls.target.clone().add(targetDelta)
      newTarget.x = THREE.MathUtils.clamp(newTarget.x, -0.75, 0.75)
      newTarget.y = THREE.MathUtils.clamp(newTarget.y, 0.05, CAM_PAN_Y_MAX)
      newTarget.z = THREE.MathUtils.clamp(newTarget.z, -0.75, 0.75)

      const appliedDelta = newTarget.clone().sub(controls.target)
      const offset = camera.position.clone().sub(controls.target).multiplyScalar(actualScale)

      controls.target.copy(newTarget)
      camera.position.copy(controls.target).add(offset).add(appliedDelta.multiplyScalar(0.15))
    }
    renderer.domElement.addEventListener('wheel', onWheel, { passive: false })

    // =========================================================================
    // 8C. RAYCASTER FOR HOVER TOOLTIP, SWITCH SELECTION & DOOR TOGGLE
    // =========================================================================
    const raycaster = new THREE.Raycaster()
    const pointer = new THREE.Vector2(-99, -99)
    const tooltip = $('hover-tooltip')
    const ttSlot = $('tt-slot')
    const ttIp = $('tt-ip')
    const ttSn = $('tt-sn')
    const ttName = $('tt-name')
    const ttMeta = $('tt-meta')

    let pointerDownPos = { x: 0, y: 0 }

    function toLocal(e) {
      const rect = container.getBoundingClientRect()
      return { x: e.clientX - rect.left, y: e.clientY - rect.top }
    }

    function onPointerMove(e) {
      const rect = container.getBoundingClientRect()
      pointer.x = ((e.clientX - rect.left) / rect.width) * 2 - 1
      pointer.y = -((e.clientY - rect.top) / rect.height) * 2 + 1

      raycaster.setFromCamera(pointer, camera)

      // Check switch & UPS hover
      const deviceHits = raycaster.intersectObjects(interactiveHitboxes, false)
      if (deviceHits.length > 0) {
        const data = deviceHits[0].object.userData
        ttSlot.textContent = data.slot
        ttIp.textContent = data.ip
        ttSn.textContent = data.sn || 'UNASSIGNED'
        ttName.textContent = data.name
        ttMeta.textContent = data.meta
        const local = toLocal(e)
        tooltip.style.left = `${local.x}px`
        tooltip.style.top = `${local.y}px`
        tooltip.style.opacity = '1'
        renderer.domElement.style.cursor = 'pointer'
      } else {
        tooltip.style.opacity = '0'
        const doorHits = raycaster.intersectObjects([glassPane, lockHousing], false)
        renderer.domElement.style.cursor = doorHits.length > 0 ? 'pointer' : 'default'
      }
    }
    container.addEventListener('pointermove', onPointerMove)
    container.addEventListener('pointerleave', () => {
      tooltip.style.opacity = '0'
    })

    function onPointerDown(e) {
      pointerDownPos = { x: e.clientX, y: e.clientY }
    }
    container.addEventListener('pointerdown', onPointerDown)

    function onPointerUp(e) {
      if (e.target !== renderer.domElement) return
      const dist = Math.hypot(e.clientX - pointerDownPos.x, e.clientY - pointerDownPos.y)
      if (dist > 6) return

      const rect = container.getBoundingClientRect()
      pointer.x = ((e.clientX - rect.left) / rect.width) * 2 - 1
      pointer.y = -((e.clientY - rect.top) / rect.height) * 2 + 1
      raycaster.setFromCamera(pointer, camera)

      // If door is open (or clicking behind door), check if user clicked a switch to edit it
      const deviceHits = raycaster.intersectObjects(interactiveHitboxes, false)
      const doorHits = raycaster.intersectObjects([glassPane, lockHousing], false)

      if (isDoorOpen && deviceHits.length > 0) {
        const data = deviceHits[0].object.userData
        if (data.kind === 'switch') {
          selectedSwitchId = data.switchId
          rebuildRackEquipment()
          // Switch right drawer to Hardware tab & focus hostname input
          tabHardware.click()
          controlPanel.classList.remove('hidden')
          controlPanel.classList.add('flex')
          return
        }
      }

      if (doorHits.length > 0) {
        toggleDoor()
        return
      }

      if (deviceHits.length > 0 && deviceHits[0].object.userData.kind === 'switch') {
        selectedSwitchId = deviceHits[0].object.userData.switchId
        rebuildRackEquipment()
        tabHardware.click()
      }
    }
    container.addEventListener('pointerup', onPointerUp)

    // =========================================================================
    // 9. RESPONSIVE RESIZE HANDLER
    // =========================================================================
    // The right-hand HUD floats OVER the canvas, so a camera aimed at the canvas centre renders the
    // rack half-hidden under the panel and it reads as off-centre. When the panel is on screen we
    // pan the projection by half the panel's width, which puts the rack in the middle of the area
    // that is actually visible. setViewOffset(width, height, x, 0, width, height) only shifts the
    // frustum, so the framing and field of view stay exactly as before.
    const fitViewport = () => {
      const w = container.clientWidth
      const h = container.clientHeight
      if (!w || !h) return
      // Two drawers now flank the canvas - hardware on the left, inventory on the right - so the
      // rack has to sit in the band BETWEEN them. setViewOffset pans the frustum: a positive x
      // moves the projected content LEFT, hence (right inset - left inset) / 2 rather than half of
      // any single panel.
      const panel = $('control-panel')
      const panelShown = !!panel && getComputedStyle(panel).display !== 'none'
      const invPanel = $('inventory-panel')
      const invShown = !!invPanel && getComputedStyle(invPanel).display !== 'none'
      const leftInset = panelShown ? panel.offsetWidth + 12 : 0
      const rightInset = invShown ? invPanel.offsetWidth + 12 : 0
      const shiftX = Math.round((rightInset - leftInset) / 2)
      // The camera preset dock sits over the bottom edge, so the rack base was hidden behind the
      // buttons. Reserve the dock's height, then centre the rack's own projected bounding box in
      // what is left - centring the camera TARGET was not enough, because the target sits above the
      // middle of the enclosure and the rack still read as low.
      const dock = $('cam-dock')
      const dockShown = !!dock && getComputedStyle(dock).display !== 'none'
      const reserveY = dockShown ? dock.offsetHeight + 12 : 0
      const freeCentreY = (h - reserveY) / 2

      // Centre the enclosure itself (front face 0..TOTAL_RACK_H), not the camera target: the target
      // sits above the middle of the rack, which is what made the whole thing read as low. Using
      // the rack's Box3 is wrong here - it also contains rear-door and cabling geometry that
      // projects below the base - so the two ends of the enclosure are projected directly.
      const screenYOf = (worldY: number) => {
        const p = new THREE.Vector3(0, worldY, 0).project(camera)
        return (-p.y * 0.5 + 0.5) * h
      }
      const centreAt = (offsetY: number) => {
        camera.setViewOffset(w, h, shiftX, offsetY, w, h)
        camera.updateProjectionMatrix()
        camera.updateMatrixWorld()
        return (screenYOf(0) + screenYOf(TOTAL_RACK_H)) / 2
      }

      camera.aspect = w / h
      camera.updateProjectionMatrix()
      const centreAtZero = centreAt(0)
      const perUnit = (centreAtZero - centreAt(40)) / 40 || 1
      const shiftY = Math.max(0, Math.round((centreAtZero - freeCentreY) / perUnit))

      if (shiftX !== 0 || shiftY > 0) camera.setViewOffset(w, h, shiftX, shiftY, w, h)
      else camera.clearViewOffset()
      camera.updateProjectionMatrix()
      renderer.setSize(w, h)
    }

    // Fitting needs one rendered frame: project() reads camera.matrixWorldInverse, which only
    // reflects the controls' camera placement after the first render. Calls that arrive earlier
    // (mount, the observer's initial fire, a data push) are held until that frame has happened,
    // otherwise the measurement is taken from a camera that is not where it will be.
    let frameRendered = false
    let fitPending = false
    const applyViewport = () => {
      if (!frameRendered) {
        fitPending = true
        return
      }
      fitViewport()
    }
    const resizeObserver = new ResizeObserver(() => applyViewport())
    resizeObserver.observe(container)
    applyViewport()

    // =========================================================================
    // 10. MAIN RENDER & ASYNCHRONOUS LED PACKET-BURST ANIMATION LOOP
    // =========================================================================
    const clock = new THREE.Clock()
    let simTime = 0
let accentPoll = 0
    let rafId = 0

    function animate() {
      rafId = requestAnimationFrame(animate)
      const delta = clock.getDelta()

      // Green online strip on the UPS: a steady pulse that never fully goes dark
      if (upsStatusBarMat) {
        upsBlinkTime += delta
        upsStatusBarMat.opacity = 0.30 + 0.70 * (0.5 + 0.5 * Math.sin(upsBlinkTime * 3.2))
      }

      // Smoothly animate front glass door hinge rotation
      doorPivot.rotation.y += (targetDoorAngle - doorPivot.rotation.y) * Math.min(1, delta * 7.5)

      // Smooth camera preset interpolation
      if (camTween) {
        camTween.progress = Math.min(1, camTween.progress + delta * 2.2)
        const ease = 1 - Math.pow(1 - camTween.progress, 3)
        camera.position.lerpVectors(camTween.startPos, camTween.endPos, ease)
        controls.target.lerpVectors(camTween.startTarget, camTween.endTarget, ease)
        if (camTween.progress >= 1) camTween = null
      }

      // Update asynchronous blinking LEDs
      if (isTrafficRunning && ledInstancedMesh && ledStates.length > 0) {
        simTime += delta * (0.4 + trafficIntensity * 1.4)

        for (let i = 0; i < ledStates.length; i++) {
          const state = ledStates[i]
          if (!state.active) continue

          if (state.type === 'system') {
            if (state.speed > 2.0) {
              const pulse = Math.sin(simTime * state.speed + state.phase) > 0
              ledInstancedMesh.setColorAt(i, pulse ? COLOR_CYAN_ON : COLOR_CYAN_DIM)
            }
          } else if (state.type === 'sfp') {
            const burst = Math.sin(simTime * state.burstFreq + state.burstOffset) > -0.3
            const flicker = Math.sin(simTime * state.speed + state.phase) > 0
            ledInstancedMesh.setColorAt(i, (burst && flicker) ? COLOR_CYAN_ON : COLOR_CYAN_DIM)
          } else {
            const burstWindow = Math.sin(simTime * state.burstFreq + state.burstOffset)
            const threshold = 0.75 - trafficIntensity * 1.35
            const isOn = burstWindow > threshold && Math.sin(simTime * state.speed + state.phase) > -0.15
            // Each time the LED lights up it picks green or amber again, so the blink pattern is random
            // in colour as well as in rhythm.
            if (isOn && !state.isOn) state.isAmber = Math.random() < 0.45
            state.isOn = isOn
            if (isOn) {
              ledInstancedMesh.setColorAt(i, state.isAmber ? COLOR_AMBER_ON : COLOR_GREEN_ON)
            } else {
              ledInstancedMesh.setColorAt(i, state.isAmber ? COLOR_AMBER_DIM : COLOR_GREEN_DIM)
            }
          }
        }
        if (ledInstancedMesh.instanceColor) {
          ledInstancedMesh.instanceColor.needsUpdate = true
        }
      }

      // Floor halo animation: advanced per frame so it never runs while the tab is throttled.
      floorLightUniforms.uTime.value += delta

      accentPoll += delta
      if (accentPoll > 0.5) {
        accentPoll = 0
        refreshAccent()
      }

      controls.update()
      renderer.render(scene, camera)
      if (!frameRendered) {
        frameRendered = true
        if (fitPending) fitViewport()
      }
    }

    // Wait for the vendored fonts so the canvas badges render Inter / JetBrains Mono
    try {
      await document.fonts.ready
    } catch {
      /* fonts are cosmetic; never block the scene on them */
    }

    // Build Initial Rack Equipment
    rebuildRackEquipment()
    // the rack's bounding box only exists once it is built, so fit the viewport again here
    applyViewport()
    animate()

    // Live data: Add / Edit / Delete in the register rebuilds the rack in place
    applySwitches = (list: NetworkSwitch[]) => {
      const prevHost = switchesData.find(sw => sw.id === selectedSwitchId)?.hostname
      switchesData = seedSwitches(list)
      if (!switchesData.some(sw => sw.id === selectedSwitchId)) {
        // A switch added here is persisted by the page and comes back with a register id, so the
        // negative local id disappears. Follow the hostname instead of snapping the editor back to
        // the first device in the rack.
        const sameHost = prevHost ? switchesData.find(sw => sw.hostname === prevHost) : undefined
        selectedSwitchId = sameHost?.id ?? switchesData[0]?.id ?? null
      }
      rebuildRackEquipment()
      applyViewport()
      syncHudUI()
    }

    // Fade out the loading overlay once the first frames are on screen
    const loader = $('loading-screen')
    if (loader) {
      loader.style.opacity = '0'
      setTimeout(() => loader.remove(), 700)
    }

    // Full teardown when the modal closes so repeated open/close never leaks GPU memory
    disposeScene = () => {
      cancelAnimationFrame(rafId)
      resizeObserver.disconnect()
      renderer.domElement.removeEventListener('wheel', onWheel)
      container.removeEventListener('pointermove', onPointerMove)
      container.removeEventListener('pointerdown', onPointerDown)
      container.removeEventListener('pointerup', onPointerUp)
      controls.dispose()
      scene.traverse(obj => {
        const mesh = obj
        if (mesh.geometry) mesh.geometry.dispose()
        const mat = mesh.material
        if (Array.isArray(mat)) mat.forEach(m => { m.map?.dispose(); m.dispose() })
        else if (mat) { mat.map?.dispose(); mat.dispose() }
      })
      renderer.dispose()
      renderer.domElement.remove()
    }
  } catch {
    container.innerHTML = '<div class="grid h-full place-items-center text-xs text-muted">3D view is not available in this browser.</div>'
  }
})
</script>

<template>
  <div ref="root" class="rack3d-host">
    <!-- 3D WebGL Viewport Container -->
    <div
      id="canvas-container"
      role="img"
      :aria-label="`3D view of a ${totalU}U network rack with ${switches.length} switch${switches.length === 1 ? '' : 'es'}`"
    ></div>

    <!-- Loading Overlay -->
    <div id="loading-screen" class="absolute inset-0 z-50 flex flex-col items-center justify-center bg-[#06090f] transition-opacity duration-700">
      <div class="w-12 h-12 border-4 border-primary/30 border-t-primary rounded-full animate-spin mb-4"></div>
      <div class="text-sm font-mono tracking-widest uppercase text-primary">Initializing {{ totalU }}U Rack &amp; Switch Telemetry...</div>
    </div>

    <!-- Hover Inspection Tooltip -->
    <div id="hover-tooltip" class="absolute z-30 pointer-events-none opacity-0 transition-opacity duration-150 transform -translate-x-1/2 -translate-y-full pb-3">
      <div class="glass-panel rounded-xl px-3.5 py-2.5 border border-primary/40 shadow-2xl min-w-[265px]">
        <div class="flex items-center justify-between gap-2">
          <span id="tt-slot" class="text-[11px] font-mono font-bold text-primary bg-primary/15 px-1.5 py-0.5 rounded">U23</span>
          <span id="tt-ip" class="text-[11px] font-mono font-semibold text-amber-300">10.10.1.11</span>
          <span id="tt-status" class="text-[10px] font-mono text-emerald-400 flex items-center gap-1">
            <span class="w-1.5 h-1.5 rounded-full bg-emerald-400"></span> ONLINE
          </span>
        </div>
        <div id="tt-name" class="text-xs font-semibold text-white mt-1">SW-CORE-01 — Catalyst 9300-48P</div>
        <div class="mt-1 flex items-center gap-1.5 text-[10px] font-mono text-slate-300 bg-slate-900/90 px-2 py-0.5 rounded border border-slate-700/80">
          <span class="text-slate-500">S/N:</span>
          <span id="tt-sn" class="text-primary font-semibold tracking-wider">FCW2419L08A</span>
        </div>
        <div id="tt-meta" class="text-[11px] font-mono text-slate-400 mt-1">48x RJ45 PoE+ | 4x 10G SFP+ | Click to Edit</div>
      </div>
    </div>

    <!-- Collapsible Toggle Button for Mobile / Compact View -->
    <button id="toggle-hud-btn" class="absolute top-3 right-3 z-30 glass-panel p-2.5 rounded-xl text-slate-200 hover:text-primary transition sm:hidden" title="Toggle Controls">
      <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 6V4m0 2a2 2 0 100 4m0-4a2 2 0 110 4m-6 8a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4m6 6v10m6-2a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4" />
      </svg>
    </button>

    <!-- LEFT DRAWER: switch asset, bottom UPS and enclosure telemetry -->
    <!-- Collapsing hides the drawer outright: an absolutely positioned box here keeps its laid-out
         width no matter what width utility is applied (only display changes take effect), so folding
         it into a narrow rail is not possible and it is folded away completely instead. -->
    <button v-if="leftCollapsed" type="button" @click="leftCollapsed = false" title="Show rack hardware panel"
      class="absolute left-2 top-1/2 -translate-y-1/2 z-30 glass-panel rounded-r-xl rounded-l-md py-3 px-1.5 text-slate-300 hover:text-primary transition hidden sm:block">
      <svg xmlns="http://www.w3.org/2000/svg" class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 5l7 7-7 7M5 5l7 7-7 7" />
      </svg>
    </button>

    <div id="control-panel" v-show="!leftCollapsed" class="absolute top-3 bottom-3 left-3 z-20 w-80 max-w-[calc(100%-1.5rem)] flex flex-col glass-panel rounded-2xl p-4 transition-all duration-300 hidden sm:flex">
      <!-- Panel header -->
      <div class="flex items-start justify-between gap-2 mb-2.5 shrink-0">
        <div class="min-w-0">
          <div class="text-[10px] uppercase tracking-widest text-slate-500">Rack hardware</div>
          <div class="text-sm font-semibold text-slate-100 leading-tight">Controls &amp; telemetry</div>
        </div>
        <button type="button" class="shrink-0 p-1.5 rounded-lg text-slate-400 hover:text-primary hover:bg-slate-800/70 transition"
          title="Collapse panel" @click="leftCollapsed = true">
          <svg xmlns="http://www.w3.org/2000/svg" class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 19l-7-7 7-7m8 14l-7-7 7-7" />
          </svg>
        </button>
      </div>

      <div class="flex-1 min-h-0 flex flex-col">
      <!-- Drawer Tabs -->
      <div class="flex items-center gap-1 p-1 bg-slate-900/90 rounded-xl border border-slate-800 mb-3 shrink-0">
        <button id="tab-hardware" class="flex-1 py-1.5 text-xs font-semibold rounded-lg bg-primary/20 text-primary border border-primary/30 transition">
          Hardware
        </button>
        <button id="tab-rack" class="flex-1 py-1.5 text-xs font-medium rounded-lg text-slate-400 hover:text-slate-200 transition">
          Telemetry
        </button>
      </div>

      <!-- Notification Banner (for auto-shifts / slot warnings) -->
      <div id="hud-toast" class="hidden mb-2.5 px-3 py-2 rounded-xl bg-amber-500/15 border border-amber-500/35 text-[11px] text-amber-200 shrink-0"></div>

      <!-- TAB 1: HARDWARE, SWITCH CONFIG & UPS MANAGEMENT -->
      <div id="view-hardware" class="flex-1 overflow-y-auto custom-scroll pr-1 space-y-3">
        <!-- Selected Switch Editor Card -->
        <div id="switch-editor-card" class="relative overflow-hidden p-3.5 rounded-2xl bg-gradient-to-b from-slate-900/95 to-slate-950/95 border border-primary/35 shadow-lg shadow-primary/10 space-y-2.5">
          <div class="absolute top-0 inset-x-0 h-0.5 bg-gradient-to-r from-primary via-primary to-amber-400"></div>
          <div class="flex items-center justify-between">
            <span class="text-[11px] font-mono font-bold uppercase tracking-wider text-primary flex items-center gap-1.5">
              <span class="w-2 h-2 rounded-full bg-primary animate-pulse"></span>
              <span id="editor-title">Switch Asset (U23)</span>
            </span>
            <button id="btn-delete-selected" class="text-[11px] px-2 py-0.5 rounded-lg bg-rose-500/15 text-rose-300 border border-rose-500/30 hover:bg-rose-500/30 transition">
              Remove
            </button>
          </div>

          <div class="grid grid-cols-1 gap-2">
            <div>
              <label class="block text-[10px] uppercase tracking-wider text-slate-400 mb-1">Switch Hostname</label>
              <input id="input-hostname" type="text" maxlength="18" placeholder="SW-CORE-01"
                class="w-full px-2.5 py-1.5 rounded-lg bg-slate-950/90 border border-slate-700/90 text-xs font-mono text-white focus:outline-none focus:border-primary transition" />
            </div>
            <div class="grid grid-cols-3 gap-2">
              <div class="col-span-2">
                <label class="block text-[10px] uppercase tracking-wider text-slate-400 mb-1">Management IP</label>
                <input id="input-ip" type="text" maxlength="18" placeholder="10.10.1.11"
                  class="w-full px-2.5 py-1.5 rounded-lg bg-slate-950/90 border border-slate-700/90 text-xs font-mono text-amber-300 focus:outline-none focus:border-primary transition" />
              </div>
              <div>
                <label class="block text-[10px] uppercase tracking-wider text-slate-400 mb-1">Rack Slot</label>
                <select id="select-slot" class="w-full px-1.5 py-1.5 rounded-lg bg-slate-950/90 border border-slate-700/90 text-xs font-mono text-primary focus:outline-none focus:border-primary transition"></select>
              </div>
            </div>
            <div>
              <div class="flex items-center justify-between mb-1">
                <label class="text-[10px] uppercase tracking-wider text-slate-400 flex items-center gap-1">
                  <svg xmlns="http://www.w3.org/2000/svg" class="w-3 h-3 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 7h.01M7 3h5c.512 0 1.024.195 1.414.586l7 7a2 2 0 010 2.828l-7 7a2 2 0 01-2.828 0l-7-7A1.994 1.994 0 013 12V7a4 4 0 014-4z" />
                  </svg>
                  Switch Serial Number (S/N)
                </label>
                <button id="btn-copy-sw-sn" type="button" title="Copy Serial Number" class="text-[10px] font-mono text-primary hover:text-primary flex items-center gap-1 transition">
                  Copy S/N
                </button>
              </div>
              <div class="relative">
                <input id="input-switch-sn" type="text" maxlength="24" placeholder="FCW2419L08A"
                  class="w-full pl-2.5 pr-8 py-1.5 rounded-lg bg-slate-950/90 border border-slate-700/90 text-xs font-mono uppercase tracking-wider text-emerald-300 focus:outline-none focus:border-emerald-400 transition" />
                <span class="absolute right-2.5 top-1/2 -translate-y-1/2 text-[9px] font-mono px-1 rounded bg-emerald-500/15 text-emerald-400 border border-emerald-500/30">ASSET</span>
              </div>
            </div>
          </div>
        </div>

        <!-- Add Switch Action -->
        <div class="flex items-center justify-between gap-2">
          <button id="btn-add-switch" class="w-full py-2 px-3 rounded-xl text-xs font-semibold bg-primary/20 text-primary border border-primary/40 hover:bg-primary/30 transition flex items-center justify-center gap-1.5">
            <svg xmlns="http://www.w3.org/2000/svg" class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
            </svg>
            Add Cisco 1U Switch
          </button>
        </div>

        <!-- Bottom UPS Selector & Serial Number (Always Bottom U's) -->
        <div class="p-3.5 rounded-2xl bg-gradient-to-b from-slate-900/90 to-slate-950/90 border border-emerald-500/25 space-y-2.5">
          <div class="flex items-center justify-between">
            <span class="text-[11px] font-semibold text-slate-200 flex items-center gap-1.5">
              <svg xmlns="http://www.w3.org/2000/svg" class="w-3.5 h-3.5 text-emerald-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z" />
              </svg>
              Bottom UPS (APC Smart-UPS)
            </span>
            <span id="ups-slot-badge" class="text-[10px] font-mono px-1.5 py-0.5 rounded bg-emerald-500/15 text-emerald-300 border border-emerald-500/30">
              U01–U02
            </span>
          </div>
          <div class="grid grid-cols-3 gap-1.5">
            <button data-ups="none" class="ups-btn py-1.5 px-2 rounded-lg text-[11px] font-medium bg-slate-800 text-slate-400 border border-slate-700 hover:text-white transition">
              None
            </button>
            <button data-ups="3000" class="ups-btn py-1.5 px-2 rounded-lg text-[11px] font-semibold bg-primary/25 text-primary border border-primary/50 transition">
              3000VA (2U)
            </button>
            <button data-ups="5000" class="ups-btn py-1.5 px-2 rounded-lg text-[11px] font-medium bg-slate-800 text-slate-400 border border-slate-700 hover:text-white transition">
              5000VA (3U)
            </button>
          </div>

          <!-- UPS Serial Number Input (Shown when UPS is installed) -->
          <div id="ups-sn-wrapper" class="pt-1">
            <div class="flex items-center justify-between mb-1">
              <label class="text-[10px] uppercase tracking-wider text-slate-400">UPS Serial Number (S/N)</label>
              <button id="btn-copy-ups-sn" type="button" class="text-[10px] font-mono text-emerald-400 hover:text-emerald-300 transition">
                Copy S/N
              </button>
            </div>
            <div class="relative">
              <input id="input-ups-sn" type="text" maxlength="24" placeholder="AS2408190442"
                class="w-full pl-2.5 pr-12 py-1.5 rounded-lg bg-slate-950/90 border border-emerald-500/30 text-xs font-mono uppercase tracking-wider text-emerald-300 focus:outline-none focus:border-emerald-400 transition" />
              <span class="absolute right-2 top-1/2 -translate-y-1/2 text-[9px] font-mono px-1 rounded bg-emerald-500/15 text-emerald-400 border border-emerald-500/30">UPS S/N</span>
            </div>
          </div>
        </div>

      </div>

      <!-- TAB 2: RACK ENCLOSURE & TELEMETRY CONTROLS -->
      <div id="view-rack" class="hidden flex-1 overflow-y-auto custom-scroll pr-1 space-y-3">
        <div class="space-y-2.5">
          <div class="flex items-center justify-between">
            <span class="text-xs text-slate-300">Front Glass Door</span>
            <button id="btn-door" class="px-3 py-1.5 rounded-xl text-xs font-medium bg-primary/20 text-primary border border-primary/40 hover:bg-primary/30 transition">
              Closed (Click to Open)
            </button>
          </div>

          <div class="flex items-center justify-between">
            <span class="text-xs text-slate-300">Steel Side Panels</span>
            <button id="btn-sides" class="px-3 py-1.5 rounded-xl text-xs font-medium bg-slate-800 text-slate-300 border border-slate-700 hover:bg-slate-700 transition">
              Installed
            </button>
          </div>

          <div class="flex items-center justify-between">
            <span class="text-xs text-slate-300">Packet LED Simulation</span>
            <button id="btn-traffic" class="px-3 py-1.5 rounded-xl text-xs font-medium bg-emerald-500/20 text-emerald-300 border border-emerald-500/40 hover:bg-emerald-500/30 transition">
              Active
            </button>
          </div>

          <div class="flex items-center justify-between">
            <span class="text-xs text-slate-300">Auto-Orbit Turntable</span>
            <button id="btn-autorotate" class="px-3 py-1.5 rounded-xl text-xs font-medium bg-slate-800 text-slate-300 border border-slate-700 hover:bg-slate-700 transition">
              Off
            </button>
          </div>
        </div>

        <!-- Traffic Intensity Slider -->
        <div class="pt-3 border-t border-slate-800">
          <div class="flex justify-between items-center mb-1.5">
            <label for="slider-intensity" class="text-xs text-slate-300">Traffic Burst Intensity</label>
            <span id="intensity-val" class="text-xs font-mono text-primary">75%</span>
          </div>
          <input id="slider-intensity" type="range" min="5" max="100" value="75" class="w-full" />
        </div>

        <!-- Helper Hint -->
        <div class="pt-2.5 border-t border-slate-800/80 text-[11px] text-slate-400 space-y-1.5">
          <p>• <strong class="text-slate-300">Pan camera:</strong> Left click + drag to slide the camera across the rack.</p>
          <p>• <strong class="text-slate-300">Orbit:</strong> Right click + drag (or Shift + drag) to rotate the viewpoint.</p>
          <p>• <strong class="text-slate-300">Cursor zoom:</strong> Scroll wheel zooms straight toward the pointer.</p>
          <p>• <strong class="text-slate-300">Edit &amp; glass door:</strong> Click a switch or the glass door in the 3D view to interact.</p>
        </div>
      </div>
      </div>
    </div>

    <!-- RIGHT DRAWER: installed equipment, kept apart from the editing controls -->
    <button v-if="rightCollapsed" type="button" @click="rightCollapsed = false" title="Show rack inventory panel"
      class="absolute right-2 top-1/2 -translate-y-1/2 z-30 glass-panel rounded-l-xl rounded-r-md py-3 px-1.5 text-slate-300 hover:text-primary transition hidden sm:block">
      <svg xmlns="http://www.w3.org/2000/svg" class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 19l-7-7 7-7m8 14l-7-7 7-7" />
      </svg>
    </button>

    <aside id="inventory-panel" v-show="!rightCollapsed" class="absolute top-3 bottom-3 right-3 z-20 w-72 max-w-[calc(100%-1.5rem)] flex-col glass-panel rounded-2xl p-4 hidden sm:flex">
      <div class="flex items-start justify-between gap-2 mb-1 shrink-0">
        <div class="min-w-0 flex-1">
          <div class="text-[10px] uppercase tracking-widest text-slate-500">Installed equipment</div>
          <div class="text-sm font-semibold text-slate-100 leading-tight truncate">Rack Inventory</div>
        </div>
        <span id="free-u-count" class="shrink-0 text-[10px] font-mono px-1.5 py-0.5 rounded bg-primary/10 text-primary border border-primary/30">36U Free</span>
        <button type="button" class="shrink-0 p-1.5 rounded-lg text-slate-400 hover:text-primary hover:bg-slate-800/70 transition"
          title="Collapse panel" @click="rightCollapsed = true">
          <svg xmlns="http://www.w3.org/2000/svg" class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 5l7 7-7 7M5 5l7 7-7 7" />
          </svg>
        </button>
      </div>
      <p class="text-[10px] text-slate-500 mb-2.5 shrink-0">Drag a card onto another to swap its U slot.</p>
      <div id="inventory-list" class="flex-1 overflow-y-auto custom-scroll pr-1 space-y-1.5"></div>
    </aside>

    <!-- Bottom Camera Presets Dock - centred in the band the two drawers leave free -->
    <div id="cam-dock" class="absolute bottom-3 left-0 right-0 z-20 flex justify-center px-3 sm:px-0 transition-all duration-300"
      :class="[leftCollapsed ? 'sm:left-0' : 'sm:left-[336px]', rightCollapsed ? 'sm:right-0' : 'sm:right-[304px]']">
      <div class="glass-panel rounded-2xl p-1.5 flex items-center justify-center gap-1.5 overflow-x-auto max-w-full">
        <button data-cam="angled" class="cam-btn active px-3.5 py-2 rounded-xl text-xs font-medium bg-primary text-slate-950 font-semibold shadow-lg shadow-primary/25 transition whitespace-nowrap">
          Angled View
        </button>
        <button data-cam="front" class="cam-btn px-3.5 py-2 rounded-xl text-xs font-medium text-slate-300 hover:bg-slate-800/80 transition whitespace-nowrap">
          Front View
        </button>
        <button data-cam="ports" class="cam-btn px-3.5 py-2 rounded-xl text-xs font-medium text-slate-300 hover:bg-slate-800/80 transition whitespace-nowrap">
          Close-Up Ports
        </button>
        <button data-cam="ups" class="cam-btn px-3.5 py-2 rounded-xl text-xs font-medium text-slate-300 hover:bg-slate-800/80 transition whitespace-nowrap">
          Bottom UPS
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.rack3d-host {
  position: relative;
  overflow: hidden;
  background-color: #0c121c;
  color: #e2e8f0;
  user-select: none;
  font-family: 'Inter', sans-serif;
}
.font-mono {
  font-family: 'JetBrains Mono', monospace;
}
#canvas-container {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  z-index: 1;
}
/* Custom glassmorphism panels */
.glass-panel {
  background: rgba(11, 17, 29, 0.82);
  backdrop-filter: blur(14px);
  -webkit-backdrop-filter: blur(14px);
  border: 1px solid color-mix(in oklab, var(--ui-primary) 22%, transparent);
  box-shadow: 0 20px 40px -15px rgba(0, 0, 0, 0.78);
}
/* Custom thin scrollbar for equipment inventory list */
.custom-scroll::-webkit-scrollbar {
  width: 5px;
}
.custom-scroll::-webkit-scrollbar-track {
  background: rgba(15, 23, 42, 0.5);
  border-radius: 999px;
}
.custom-scroll::-webkit-scrollbar-thumb {
  background: color-mix(in oklab, var(--ui-primary) 45%, transparent);
  border-radius: 999px;
}
/* Custom range slider */
input[type=range] {
  -webkit-appearance: none;
  background: transparent;
}
input[type=range]::-webkit-slider-thumb {
  -webkit-appearance: none;
  height: 14px;
  width: 14px;
  border-radius: 50%;
  background: #06b6d4;
  cursor: pointer;
  margin-top: -5px;
  box-shadow: 0 0 10px rgba(6, 182, 212, 0.8);
}
input[type=range]::-webkit-slider-runnable-track {
  width: 100%;
  height: 4px;
  cursor: pointer;
  background: #1e293b;
  border-radius: 999px;
}
</style>
