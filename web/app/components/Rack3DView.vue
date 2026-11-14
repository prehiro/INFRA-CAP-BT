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
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js'
import type { NetworkSwitch } from '~/types'

// The rack's real switches are the only source of hardware in this scene.
const props = defineProps<{ switches: NetworkSwitch[] }>()

const root = ref<HTMLElement | null>(null)
const $ = (id: string) => root.value?.querySelector(`#${id}`)

// Teardown and live-data hooks are registered synchronously HERE, at setup level. The scene is
// built inside an async onMounted that awaits the vendored fonts first, and a lifecycle hook
// registered after that await is no longer bound to the component instance, so it would be
// silently dropped and the WebGL context would leak on every modal open/close.
let disposeScene: (() => void) | null = null
let applySwitches: ((list: NetworkSwitch[]) => void) | null = null
onUnmounted(() => disposeScene?.())
watch(() => props.switches, list => applySwitches?.(list ?? []), { deep: true })

// "U6", "U6-U7", "u 6 - u 7" -> { start, end }; anything unparseable returns null.
function parseUnitRange(raw: string): { start: number; end: number } | null {
  const m = /u\s*(\d+)\s*(?:[-\u2013]\s*(?:u\s*)?(\d+))?/i.exec((raw || '').trim())
  if (!m) return null
  const a = Number(m[1])
  const b = m[2] ? Number(m[2]) : a
  if (!Number.isFinite(a) || !Number.isFinite(b)) return null
  const lo = Math.min(a, b)
  const hi = Math.max(a, b)
  if (lo < 1 || hi > 24) return null
  return { start: lo, end: hi }
}

onMounted(async () => {
  const container = $('canvas-container')
  if (!container) return

  try {
    // =====================================================================
    // 1. SCENE, RENDERER, CAMERA & CURSOR-TARGETED ZOOM CONTROLS
    // =====================================================================
    const scene = new THREE.Scene()
    scene.background = new THREE.Color(0x06090f)
    scene.fog = new THREE.FogExp2(0x06090f, 0.055)

    const camera = new THREE.PerspectiveCamera(42, 1, 0.05, 60)
    camera.position.set(1.45, 1.15, 1.95)

    const renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' })
    renderer.setSize(container.clientWidth, container.clientHeight)
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2))
    renderer.shadowMap.enabled = true
    renderer.shadowMap.type = THREE.PCFSoftShadowMap
    renderer.toneMapping = THREE.ACESFilmicToneMapping
    renderer.toneMappingExposure = 1.18
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
    controls.target.set(0, 0.72, 0)

    // Batasi area geser (pan) agar kamera tidak terlalu jauh keluar dari area rack
    controls.addEventListener('change', () => {
      const minTarget = new THREE.Vector3(-0.85, 0.05, -0.85)
      const maxTarget = new THREE.Vector3(0.85, 1.45, 0.85)
      const prevTarget = controls.target.clone()
      controls.target.clamp(minTarget, maxTarget)
      camera.position.add(controls.target.clone().sub(prevTarget))
    })

    // Studio / Data Center Lighting
    const hemiLight = new THREE.HemisphereLight(0xdbeafe, 0x0f172a, 0.75)
    scene.add(hemiLight)

    const keyLight = new THREE.DirectionalLight(0xffffff, 1.85)
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
    const rimLight = new THREE.DirectionalLight(0x38bdf8, 0.9)
    rimLight.position.set(-2.5, 2.2, -2.5)
    scene.add(rimLight)

    const frontFillLight = new THREE.PointLight(0xe0f2fe, 0.85, 4.0)
    frontFillLight.position.set(0, 1.1, 1.45)
    scene.add(frontFillLight)

    // =====================================================================
    // 2. DATA CENTER FLOOR & SUBTLE REFLECTIVE GRID
    // =====================================================================
    const floorGeo = new THREE.PlaneGeometry(24, 24)
    const floorMat = new THREE.MeshStandardMaterial({
      color: 0x090d16,
      roughness: 0.25,
      metalness: 0.75
    })
    const floor = new THREE.Mesh(floorGeo, floorMat)
    floor.rotation.x = -Math.PI / 2
    floor.receiveShadow = true
    scene.add(floor)

    const gridHelper = new THREE.GridHelper(16, 32, 0x1e293b, 0x0f172a)
    gridHelper.position.y = 0.001
    scene.add(gridHelper)

    // Subtle cyan under-rack glow strip
    const glowPlane = new THREE.Mesh(
      new THREE.PlaneGeometry(0.68, 0.86),
      new THREE.MeshBasicMaterial({ color: 0x06b6d4, transparent: true, opacity: 0.14 })
    )
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
      canvas.height = 1536
      const ctx = canvas.getContext('2d')
      ctx.fillStyle = '#1e293b'
      ctx.fillRect(0, 0, 64, 1536)

      const step = 1536 / 24
      for (let i = 0; i < 24; i++) {
        const uNum = 24 - i
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

    function createCiscoBadgeTexture(modelLabel, hostname = 'SW-CORE-01', ipAddress = '10.10.1.11') {
      const canvas = document.createElement('canvas')
      canvas.width = 512
      canvas.height = 128
      const ctx = canvas.getContext('2d')
      ctx.clearRect(0, 0, 512, 128)

      // Subtle dark OLED label plate background
      ctx.fillStyle = 'rgba(7, 13, 24, 0.92)'
      ctx.beginPath()
      ctx.roundRect(4, 6, 504, 116, 14)
      ctx.fill()
      ctx.strokeStyle = 'rgba(56, 189, 248, 0.45)'
      ctx.lineWidth = 3
      ctx.stroke()

      // Cisco signature bridge icon bars (cyan/teal)
      ctx.fillStyle = '#049fd9'
      const bars = [16, 28, 44, 28, 16, 28, 44, 28, 16]
      bars.forEach((h, idx) => {
        ctx.fillRect(22 + idx * 11, 64 - h, 6, h)
      })

      // "CISCO" wordmark + Model
      ctx.fillStyle = '#f1f5f9'
      ctx.font = 'bold 24px "Inter", sans-serif'
      ctx.fillText('CISCO', 22, 98)

      ctx.fillStyle = '#38bdf8'
      ctx.font = '600 18px "JetBrains Mono", monospace'
      ctx.fillText(modelLabel, 112, 98)

      // Divider line
      ctx.fillStyle = 'rgba(148, 163, 184, 0.25)'
      ctx.fillRect(194, 18, 2, 92)

      // Dynamic Hostname & IP Address Readout
      ctx.fillStyle = '#ffffff'
      ctx.font = 'bold 28px "JetBrains Mono", monospace'
      ctx.fillText(hostname.slice(0, 16), 210, 56)

      ctx.fillStyle = '#fbbf24'
      ctx.font = '600 25px "JetBrains Mono", monospace'
      ctx.fillText(ipAddress.slice(0, 18), 210, 96)

      const tex = new THREE.CanvasTexture(canvas)
      tex.anisotropy = 4
      return tex
    }

    function createUpsLcdTexture(vaRating, loadPct, runtimeMins) {
      const canvas = document.createElement('canvas')
      canvas.width = 512
      canvas.height = 192
      const ctx = canvas.getContext('2d')

      // Backlit cyan-blue APC Smart-UPS LCD screen
      const grad = ctx.createLinearGradient(0, 0, 0, 192)
      grad.addColorStop(0, '#072a40')
      grad.addColorStop(1, '#041926')
      ctx.fillStyle = grad
      ctx.fillRect(0, 0, 512, 192)

      ctx.strokeStyle = '#38bdf8'
      ctx.lineWidth = 6
      ctx.strokeRect(4, 4, 504, 184)

      // Top status header
      ctx.fillStyle = '#38bdf8'
      ctx.font = 'bold 24px "JetBrains Mono", monospace'
      ctx.fillText(`APC Smart-UPS ${vaRating}VA`, 22, 40)

      ctx.fillStyle = '#34d399'
      ctx.font = 'bold 22px "JetBrains Mono", monospace'
      ctx.fillText('ONLINE // GREEN MODE', 22, 74)

      // Metrics bar
      ctx.fillStyle = '#e0f2fe'
      ctx.font = '600 23px "JetBrains Mono", monospace'
      ctx.fillText(`LOAD:${loadPct}%   BATT:100%`, 22, 118)

      ctx.fillStyle = '#93c5fd'
      ctx.font = '20px "JetBrains Mono", monospace'
      ctx.fillText(`IN:230.4V  OUT:230.0V  ~${runtimeMins}m`, 22, 158)

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
    const matSwitchChassis = new THREE.MeshStandardMaterial({
      map: brushedTex,
      color: 0x2d3540,
      roughness: 0.36,
      metalness: 0.82
    })
    const matSwitchBezel = new THREE.MeshStandardMaterial({
      color: 0x1b2129,
      roughness: 0.45,
      metalness: 0.7
    })
    const matCiscoTeal = new THREE.MeshStandardMaterial({
      color: 0x049fd9,
      roughness: 0.3,
      metalness: 0.4,
      emissive: 0x024e6b,
      emissiveIntensity: 0.25
    })
    const matPortMetal = new THREE.MeshStandardMaterial({
      color: 0x94a3b8,
      roughness: 0.25,
      metalness: 0.92
    })
    const matPortCavity = new THREE.MeshStandardMaterial({
      color: 0x05070b,
      roughness: 0.85,
      metalness: 0.1
    })
    const matScrewChrome = new THREE.MeshStandardMaterial({
      color: 0xd1d5db,
      roughness: 0.2,
      metalness: 0.95
    })

    // =====================================================================
    // 4. BUILD THE 24U SERVER / NETWORK RACK ENCLOSURE
    // =====================================================================
    // Real-world dimensions (1 unit = 1 meter):
    // Width: 0.60m, Depth: 0.80m, 1U height = 0.04445m (1.75 in)
    // 24U interior height = 24 * 0.04445 = 1.0668m
    const U_HEIGHT = 0.04445
    const TOTAL_U = 24
    const RACK_INNER_H = TOTAL_U * U_HEIGHT // ~1.067m
    const RACK_W = 0.60
    const RACK_D = 0.80
    const BASE_H = 0.085
    const TOP_H = 0.075
    const TOTAL_RACK_H = BASE_H + RACK_INNER_H + TOP_H // ~1.227m

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
      reflectivity: 0.9,
      clearcoat: 1.0,
      clearcoatRoughness: 0.06,
      depthWrite: false
    })
    const glassPane = new THREE.Mesh(
      new THREE.BoxGeometry(doorW - frameThickness * 2, doorH - frameThickness * 2, 0.006),
      glassMat
    )
    glassPane.position.set(doorW / 2, 0, 0)
    glassPane.name = 'frontGlassDoor'
    doorPivot.add(glassPane)

    // Subtle cyan vertical accent pinstripes on glass edge
    const glassStripe = new THREE.Mesh(
      new THREE.BoxGeometry(0.004, doorH - frameThickness * 2, 0.007),
      new THREE.MeshBasicMaterial({ color: 0x06b6d4, transparent: true, opacity: 0.45 })
    )
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
    let upsModel = '3000'
    let upsSerial = 'AS2408190442'
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
        if (!u) continue // rack full: 24U cannot hold another 1U switch
        occ.add(u)

        out.push({
          id: sw.id,
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
    const LED_COUNT_PER_SWITCH = 48 + 4 + 5 // 57 LEDs per 1U switch
    const ledGeo = new THREE.BoxGeometry(0.0024, 0.0018, 0.0015)
    const ledMat = new THREE.MeshBasicMaterial({ color: 0xffffff })
    let ledInstancedMesh = null
    let ledStates = []

    const dummyObj = new THREE.Object3D()
    const COLOR_GREEN_ON  = new THREE.Color(0x10ff70)
    const COLOR_GREEN_DIM = new THREE.Color(0x064e24)
    const COLOR_AMBER_ON  = new THREE.Color(0xffaa00)
    const COLOR_AMBER_DIM = new THREE.Color(0x523400)
    const COLOR_CYAN_ON   = new THREE.Color(0x00f0ff)
    const COLOR_OFF       = new THREE.Color(0x0a0e14)

    let interactiveHitboxes = [] // Switch & UPS hitboxes for Raycaster
    const switchBadgeRefs = new Map() // switchId -> { mesh, model } for live texture updates

    // Fiber & Power Cord Materials
    const matFiberAqua = new THREE.MeshStandardMaterial({ color: 0x06b6d4, roughness: 0.35, metalness: 0.1 })
    const matFiberOrange = new THREE.MeshStandardMaterial({ color: 0xf97316, roughness: 0.35, metalness: 0.1 })
    const matPowerCord = new THREE.MeshStandardMaterial({ color: 0x111827, roughness: 0.75 })
    const matUpsBlack = new THREE.MeshStandardMaterial({ color: 0x12151c, roughness: 0.42, metalness: 0.78 })
    const matApcRed = new THREE.MeshStandardMaterial({ color: 0xdc2626, roughness: 0.35, metalness: 0.3, emissive: 0x7f1d1d, emissiveIntensity: 0.25 })

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

    function getFreeSlots() {
      const occ = getOccupiedSet()
      const free = []
      for (let u = TOTAL_U; u >= 1; u--) {
        if (!occ.has(u)) free.push(u)
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

      // APC Red Brand Badge on Left
      const apcBadge = new THREE.Mesh(new THREE.BoxGeometry(0.044, 0.018, 0.004), matApcRed)
      apcBadge.position.set(-0.185, totalH / 2 - 0.016, 0.013)
      upsGroup.add(apcBadge)

      // Center Illuminated LCD Status Panel
      const loadPct = Math.min(92, Math.round((switchesData.length * 340 + 180) / (upsModel === '5000' ? 45 : 27)))
      const runtimeMins = upsModel === '5000' ? Math.max(18, 68 - switchesData.length * 3) : Math.max(11, 44 - switchesData.length * 2)
      const lcdTex = createUpsLcdTexture(upsModel, loadPct, runtimeMins)

      const lcdScreen = new THREE.Mesh(
        new THREE.PlaneGeometry(0.135, Math.min(0.056, totalH * 0.56)),
        new THREE.MeshBasicMaterial({ map: lcdTex })
      )
      lcdScreen.position.set(0, 0.004, 0.013)
      upsGroup.add(lcdScreen)

      // Green Online Status Bar below LCD
      const statusBar = new THREE.Mesh(
        new THREE.BoxGeometry(0.09, 0.004, 0.004),
        new THREE.MeshBasicMaterial({ color: 0x10ff70 })
      )
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
      switchBadgeRefs.clear()
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

        // 2. Front Faceplate Bezel
        const faceplate = new THREE.Mesh(
          new THREE.BoxGeometry(chassisWidth, chassisHeight, 0.006),
          matSwitchBezel
        )
        faceplate.position.set(0, 0, 0.001)
        switchGroup.add(faceplate)

        // Top Accent Stripe (Highlighted Amber/Cyan if currently selected for editing)
        const accentMat = isSelected
          ? new THREE.MeshBasicMaterial({ color: 0xfbbf24 })
          : matCiscoTeal
        const accentStripe = new THREE.Mesh(
          new THREE.BoxGeometry(chassisWidth - 0.004, 0.0034, 0.0072),
          accentMat
        )
        accentStripe.position.set(0, chassisHeight / 2 - 0.0022, 0.0016)
        switchGroup.add(accentStripe)

        // 3. Left & Right Rack-Mounting Ears with Chrome Screws
        const switchEarSides = [-1, 1]
        switchEarSides.forEach(side => {
          const ear = new THREE.Mesh(
            new THREE.BoxGeometry(0.021, chassisHeight, 0.004),
            matSwitchChassis
          )
          ear.position.set(side * (chassisWidth / 2 + 0.0105), 0, 0.002)
          switchGroup.add(ear)

          const earScrewOffsets = [-0.013, 0.013]
          earScrewOffsets.forEach(sy => {
            const screw = new THREE.Mesh(
              new THREE.CylinderGeometry(0.0032, 0.0032, 0.004, 10),
              matScrewChrome
            )
            screw.rotation.x = Math.PI / 2
            screw.position.set(side * (chassisWidth / 2 + 0.0105), sy, 0.0045)
            switchGroup.add(screw)
          })
        })

        // 4. Left Section: Dynamic Cisco + Hostname + IP Badge + System LEDs
        const shortModel = cfg.model.replace('Catalyst ', 'C')
        const badgeTex = createCiscoBadgeTexture(shortModel, cfg.hostname, cfg.ip)
        const badgeMesh = new THREE.Mesh(
          new THREE.PlaneGeometry(0.088, 0.023),
          new THREE.MeshBasicMaterial({ map: badgeTex, transparent: true })
        )
        badgeMesh.position.set(-0.172, 0.0055, 0.0046)
        switchGroup.add(badgeMesh)
        switchBadgeRefs.set(cfg.id, { mesh: badgeMesh, model: shortModel })

        // Mode Button
        const modeBtn = new THREE.Mesh(
          new THREE.CylinderGeometry(0.0034, 0.0034, 0.004, 12),
          matPortMetal
        )
        modeBtn.rotation.x = Math.PI / 2
        modeBtn.position.set(-0.207, -0.0115, 0.0045)
        switchGroup.add(modeBtn)

        // 5 System Status LEDs
        for (let sys = 0; sys < 5; sys++) {
          const lx = -0.194 + sys * 0.011
          const ly = -0.0115
          dummyObj.position.set(lx, yPos + ly, zFront + 0.0046)
          dummyObj.updateMatrix()
          ledInstancedMesh.setMatrixAt(ledIndex, dummyObj.matrix)
          ledInstancedMesh.setColorAt(ledIndex, sys === 4 ? COLOR_AMBER_ON : COLOR_GREEN_ON)

          ledStates.push({
            type: 'system',
            active: true,
            isAmber: sys === 4,
            phase: Math.random() * Math.PI * 2,
            speed: sys === 1 ? 8.0 : 0.8
          })
          ledIndex++
        }

        // 5. Center Section: 48 RJ45 Ports (24 Columns x 2 Rows)
        const startPortX = -0.120
        const colSpacing = 0.0109
        const blockGap = 0.005
        const rowYOffsets = [0.0068, -0.0072]

        for (let col = 0; col < 24; col++) {
          const blockIndex = Math.floor(col / 6)
          const px = startPortX + col * colSpacing + blockIndex * blockGap

          for (let row = 0; row < 2; row++) {
            const py = rowYOffsets[row]
            const portNum = col * 2 + row

            const housing = new THREE.Mesh(
              new THREE.BoxGeometry(0.0094, 0.0086, 0.003),
              matPortMetal
            )
            housing.position.set(px, py, 0.0042)
            switchGroup.add(housing)

            const cavity = new THREE.Mesh(
              new THREE.BoxGeometry(0.0072, 0.0062, 0.0034),
              matPortCavity
            )
            cavity.position.set(px, py - 0.0004, 0.0044)
            switchGroup.add(cavity)

            const ledY = row === 0 ? py + 0.0062 : py - 0.0062
            dummyObj.position.set(px, yPos + ledY, zFront + 0.0052)
            dummyObj.updateMatrix()
            ledInstancedMesh.setMatrixAt(ledIndex, dummyObj.matrix)

            const isPortActive = portNum < cfg.activePorts
            const isPoEPort = isPortActive && (portNum % 5 === 0)

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

        // 6. Right Section: 4x 10G SFP+ Uplink Cages + Fiber Patch Cords
        const sfpStartX = 0.168
        for (let sfp = 0; sfp < 4; sfp++) {
          const sx = sfpStartX + sfp * 0.0135
          const sy = -0.001

          const cage = new THREE.Mesh(new THREE.BoxGeometry(0.011, 0.013, 0.005), matPortMetal)
          cage.position.set(sx, sy, 0.0048)
          switchGroup.add(cage)

          const sfpHole = new THREE.Mesh(new THREE.BoxGeometry(0.0085, 0.0095, 0.0055), matPortCavity)
          sfpHole.position.set(sx, sy, 0.005)
          switchGroup.add(sfpHole)

          const hasTransceiver = sfp < 2
          if (hasTransceiver) {
            const transceiver = new THREE.Mesh(new THREE.BoxGeometry(0.0082, 0.0088, 0.016), matScrewChrome)
            transceiver.position.set(sx, sy, 0.011)
            switchGroup.add(transceiver)

            const latch = new THREE.Mesh(
              new THREE.BoxGeometry(0.0086, 0.0025, 0.004),
              sfp === 0 ? matFiberAqua : matFiberOrange
            )
            latch.position.set(sx, sy - 0.0035, 0.018)
            switchGroup.add(latch)

            const curve = new THREE.CatmullRomCurve3([
              new THREE.Vector3(sx, sy, 0.018),
              new THREE.Vector3(sx + 0.012, sy - 0.004, 0.042),
              new THREE.Vector3(0.245, sy - 0.012, 0.032),
              new THREE.Vector3(0.252, -0.22, -0.04)
            ])
            const tubeMesh = new THREE.Mesh(
              new THREE.TubeGeometry(curve, 16, 0.0016, 6, false),
              sfp === 0 ? matFiberAqua : matFiberOrange
            )
            switchGroup.add(tubeMesh)
          }

          dummyObj.position.set(sx, yPos + 0.0095, zFront + 0.0054)
          dummyObj.updateMatrix()
          ledInstancedMesh.setMatrixAt(ledIndex, dummyObj.matrix)
          ledInstancedMesh.setColorAt(ledIndex, hasTransceiver ? COLOR_CYAN_ON : COLOR_OFF)
          ledStates.push({
            type: 'sfp',
            active: hasTransceiver,
            isAmber: false,
            phase: Math.random() * Math.PI * 2,
            speed: 14.0 + Math.random() * 10.0,
            burstFreq: 1.8 + Math.random() * 2.0,
            burstOffset: Math.random() * 5.0
          })
          ledIndex++
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
      const ref = switchBadgeRefs.get(sw.id)
      if (ref && ref.mesh) {
        const oldTex = ref.mesh.material.map
        ref.mesh.material.map = createCiscoBadgeTexture(ref.model, sw.hostname, sw.ip)
        ref.mesh.material.needsUpdate = true
        if (oldTex) oldTex.dispose()
      }
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
          ? 'ups-btn py-1.5 px-2 rounded-lg text-[11px] font-semibold bg-cyan-500/25 text-cyan-300 border border-cyan-500/50 transition'
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
                            ? 'bg-cyan-500/15 border-cyan-400/50 text-white'
                            : 'bg-slate-900/60 border-slate-800 hover:border-slate-700 text-slate-300'
                    }">
                        <div class="text-slate-500 hover:text-cyan-400 shrink-0" title="Drag to reorder">
                            <svg xmlns="http://www.w3.org/2000/svg" class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 8h16M4 16h16" />
                            </svg>
                        </div>
                        <div class="min-w-0 flex-1">
                            <div class="flex items-center gap-1.5">
                                <span class="text-[10px] font-mono font-bold px-1.5 py-0.2 rounded bg-slate-800 text-cyan-400">U${String(sw.u).padStart(2, '0')}</span>
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
          row.classList.add('opacity-45', 'border-cyan-400')
          if (e.dataTransfer) {
            e.dataTransfer.effectAllowed = 'move'
            e.dataTransfer.setData('text/plain', String(draggedSwitchId))
          }
        })

        row.addEventListener('dragend', () => {
          draggedSwitchId = null
          inventoryList.querySelectorAll('.inv-row').forEach(el => {
            el.classList.remove('opacity-45', 'border-cyan-400', 'border-t-2', 'border-t-cyan-400', 'border-b-2', 'border-b-cyan-400')
          })
        })

        row.addEventListener('dragover', (e) => {
          e.preventDefault()
          const targetId = Number(row.getAttribute('data-select-id'))
          if (draggedSwitchId === null || draggedSwitchId === targetId) return

          const rect = row.getBoundingClientRect()
          const isTopHalf = (e.clientY - rect.top) < rect.height / 2
          row.classList.toggle('border-t-2', isTopHalf)
          row.classList.toggle('border-t-cyan-400', isTopHalf)
          row.classList.toggle('border-b-2', !isTopHalf)
          row.classList.toggle('border-b-cyan-400', !isTopHalf)
        })

        row.addEventListener('dragleave', () => {
          row.classList.remove('border-t-2', 'border-t-cyan-400', 'border-b-2', 'border-b-cyan-400')
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
          rebuildRackEquipment()
        })
      }
    }

    function removeSwitchById(id) {
      const target = switchesData.find(s => s.id === id)
      if (!target) return
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
    })

    inputIp.addEventListener('input', (e) => {
      const sw = switchesData.find(s => s.id === selectedSwitchId)
      if (!sw) return
      sw.ip = e.target.value.trim() || '0.0.0.0'
      updateSingleSwitch3DLabel(sw)
      syncHudUI()
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
      // Prefer a slot with 1U gap if available, otherwise highest free U slot
      let chosenU = freeSlots[0]
      for (const u of freeSlots) {
        if (!getOccupiedSet().has(u + 1) && !getOccupiedSet().has(u - 1)) {
          chosenU = u
          break
        }
      }

      const newId = nextSwitchSeq++
      const newSwitch = {
        id: newId,
        u: chosenU,
        model: 'Catalyst 9300-48P',
        hostname: `SW-EDGE-${String(newId).padStart(2, '0')}`,
        ip: `10.10.10.${10 + newId}`,
        sn: `FCW26${String(10 + newId).padStart(2, '0')}X${String(newId).padStart(2, '0')}Z`,
        activePorts: 36 + (newId % 11)
      }
      switchesData.push(newSwitch)
      selectedSwitchId = newId
      showToast(`Added ${newSwitch.hostname} (${newSwitch.ip}) at U${String(chosenU).padStart(2, '0')}.`)
      rebuildRackEquipment()
    })

    // UPS Model Selection Buttons (None / 3000VA / 5000VA)
    document.querySelectorAll('.ups-btn').forEach(btn => {
      btn.addEventListener('click', () => {
        const chosen = btn.getAttribute('data-ups')
        if (chosen === upsModel) return
        upsModel = chosen
        rebuildRackEquipment()
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
      tabHardware.className = 'flex-1 py-1.5 text-xs font-semibold rounded-lg bg-cyan-500/20 text-cyan-300 border border-cyan-500/30 transition'
      tabRack.className = 'flex-1 py-1.5 text-xs font-medium rounded-lg text-slate-400 hover:text-slate-200 transition'
    })

    tabRack.addEventListener('click', () => {
      viewRack.classList.remove('hidden')
      viewHardware.classList.add('hidden')
      tabRack.className = 'flex-1 py-1.5 text-xs font-semibold rounded-lg bg-cyan-500/20 text-cyan-300 border border-cyan-500/30 transition'
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
        : 'px-3 py-1.5 rounded-xl text-xs font-medium bg-cyan-500/20 text-cyan-300 border border-cyan-500/40 hover:bg-cyan-500/30 transition'
    }

    btnDoor.addEventListener('click', toggleDoor)

    btnSides.addEventListener('click', () => {
      areSidesVisible = !areSidesVisible
      sidePanelsGroup.visible = areSidesVisible
      btnSides.textContent = areSidesVisible ? 'Installed' : 'Removed (Open)'
      btnSides.className = areSidesVisible
        ? 'px-3 py-1.5 rounded-xl text-xs font-medium bg-slate-800 text-slate-300 border border-slate-700 hover:bg-slate-700 transition'
        : 'px-3 py-1.5 rounded-xl text-xs font-medium bg-cyan-500/20 text-cyan-300 border border-cyan-500/40 hover:bg-cyan-500/30 transition'
    })

    btnTraffic.addEventListener('click', () => {
      isTrafficRunning = !isTrafficRunning
      btnTraffic.textContent = isTrafficRunning ? 'Active' : 'Paused'
      btnTraffic.className = isTrafficRunning
        ? 'px-3 py-1.5 rounded-xl text-xs font-medium bg-emerald-500/20 text-emerald-300 border border-emerald-500/40 hover:bg-emerald-500/30 transition'
        : 'px-3 py-1.5 rounded-xl text-xs font-medium bg-rose-500/20 text-rose-300 border border-rose-500/40 hover:bg-rose-500/30 transition'
    })

    btnAutoRotate.addEventListener('click', () => {
      controls.autoRotate = !controls.autoRotate
      controls.autoRotateSpeed = 1.4
      btnAutoRotate.textContent = controls.autoRotate ? 'On' : 'Off'
      btnAutoRotate.className = controls.autoRotate
        ? 'px-3 py-1.5 rounded-xl text-xs font-medium bg-cyan-500/20 text-cyan-300 border border-cyan-500/40 hover:bg-cyan-500/30 transition'
        : 'px-3 py-1.5 rounded-xl text-xs font-medium bg-slate-800 text-slate-300 border border-slate-700 hover:bg-slate-700 transition'
    })

    sliderIntensity.addEventListener('input', (e) => {
      trafficIntensity = Number(e.target.value) / 100
      intensityVal.textContent = `${e.target.value}%`
    })

    // Mobile HUD toggle
    const toggleHudBtn = $('toggle-hud-btn')
    const controlPanel = $('control-panel')
    toggleHudBtn.addEventListener('click', () => {
      controlPanel.classList.toggle('hidden')
      controlPanel.classList.toggle('flex')
    })

    // Smooth Camera Transition Presets
    let camTween = null
    const presets = {
      angled: { pos: new THREE.Vector3(1.45, 1.15, 1.95), target: new THREE.Vector3(0, 0.72, 0) },
      front:  { pos: new THREE.Vector3(0, 0.78, 2.05),    target: new THREE.Vector3(0, 0.72, 0) },
      ports:  { pos: new THREE.Vector3(0.18, 0.92, 0.92), target: new THREE.Vector3(0, 0.88, 0.32) },
      ups:    { pos: new THREE.Vector3(0.45, 0.32, 1.15), target: new THREE.Vector3(0, 0.16, 0.25) },
      rear:   { pos: new THREE.Vector3(-1.35, 1.05, -1.85), target: new THREE.Vector3(0, 0.72, 0) }
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
        btn.className = 'cam-btn active px-3.5 py-2 rounded-xl text-xs font-medium bg-cyan-500 text-slate-950 font-semibold shadow-lg shadow-cyan-500/25 transition whitespace-nowrap'
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

      // Keep target bounded near the rack so user doesn't drift off into empty space
      const newTarget = controls.target.clone().add(targetDelta)
      newTarget.x = THREE.MathUtils.clamp(newTarget.x, -0.75, 0.75)
      newTarget.y = THREE.MathUtils.clamp(newTarget.y, 0.05, 1.35)
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
    const resizeObserver = new ResizeObserver(() => {
      const w = container.clientWidth
      const h = container.clientHeight
      if (!w || !h) return
      camera.aspect = w / h
      camera.updateProjectionMatrix()
      renderer.setSize(w, h)
    })
    resizeObserver.observe(container)
    camera.aspect = container.clientWidth / container.clientHeight
    camera.updateProjectionMatrix()

    // =========================================================================
    // 10. MAIN RENDER & ASYNCHRONOUS LED PACKET-BURST ANIMATION LOOP
    // =========================================================================
    const clock = new THREE.Clock()
    let simTime = 0
    let rafId = 0

    function animate() {
      rafId = requestAnimationFrame(animate)
      const delta = clock.getDelta()

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
              ledInstancedMesh.setColorAt(i, pulse ? COLOR_GREEN_ON : COLOR_GREEN_DIM)
            }
          } else if (state.type === 'sfp') {
            const burst = Math.sin(simTime * state.burstFreq + state.burstOffset) > -0.3
            const flicker = Math.sin(simTime * state.speed + state.phase) > 0
            ledInstancedMesh.setColorAt(i, (burst && flicker) ? COLOR_CYAN_ON : COLOR_GREEN_DIM)
          } else {
            const burstWindow = Math.sin(simTime * state.burstFreq + state.burstOffset)
            const threshold = 0.75 - trafficIntensity * 1.35
            if (burstWindow > threshold) {
              const frameBlink = Math.sin(simTime * state.speed + state.phase) > -0.15
              if (state.isAmber) {
                ledInstancedMesh.setColorAt(i, frameBlink ? COLOR_AMBER_ON : COLOR_AMBER_DIM)
              } else {
                ledInstancedMesh.setColorAt(i, frameBlink ? COLOR_GREEN_ON : COLOR_GREEN_DIM)
              }
            } else {
              ledInstancedMesh.setColorAt(i, state.isAmber ? COLOR_AMBER_DIM : COLOR_GREEN_DIM)
            }
          }
        }
        if (ledInstancedMesh.instanceColor) {
          ledInstancedMesh.instanceColor.needsUpdate = true
        }
      }

      controls.update()
      renderer.render(scene, camera)
    }

    // Wait for the vendored fonts so the canvas badges render Inter / JetBrains Mono
    try {
      await document.fonts.ready
    } catch {
      /* fonts are cosmetic; never block the scene on them */
    }

    // Build Initial Rack Equipment
    rebuildRackEquipment()
    animate()

    // Live data: Add / Edit / Delete in the register rebuilds the rack in place
    applySwitches = (list: NetworkSwitch[]) => {
      switchesData = seedSwitches(list)
      if (!switchesData.some(sw => sw.id === selectedSwitchId)) {
        selectedSwitchId = switchesData[0]?.id ?? null
      }
      rebuildRackEquipment()
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
      :aria-label="`3D view of a 24U network rack with ${switches.length} switch${switches.length === 1 ? '' : 'es'}`"
    ></div>

    <!-- Rack register (real switches + per-row edit/delete) drawn over the canvas, left side -->
    <div class="rack3d-register"><slot name="panel" /></div>

    <!-- Loading Overlay -->
    <div id="loading-screen" class="absolute inset-0 z-50 flex flex-col items-center justify-center bg-[#06090f] transition-opacity duration-700">
      <div class="w-12 h-12 border-4 border-cyan-500/30 border-t-cyan-400 rounded-full animate-spin mb-4"></div>
      <div class="text-sm font-mono tracking-widest uppercase text-cyan-400">Initializing 24U Rack & Switch Telemetry...</div>
    </div>

    <!-- Hover Inspection Tooltip -->
    <div id="hover-tooltip" class="absolute z-30 pointer-events-none opacity-0 transition-opacity duration-150 transform -translate-x-1/2 -translate-y-full pb-3">
      <div class="glass-panel rounded-xl px-3.5 py-2.5 border border-cyan-400/40 shadow-2xl min-w-[265px]">
        <div class="flex items-center justify-between gap-2">
          <span id="tt-slot" class="text-[11px] font-mono font-bold text-cyan-400 bg-cyan-950/80 px-1.5 py-0.5 rounded">U23</span>
          <span id="tt-ip" class="text-[11px] font-mono font-semibold text-amber-300">10.10.1.11</span>
          <span id="tt-status" class="text-[10px] font-mono text-emerald-400 flex items-center gap-1">
            <span class="w-1.5 h-1.5 rounded-full bg-emerald-400"></span> ONLINE
          </span>
        </div>
        <div id="tt-name" class="text-xs font-semibold text-white mt-1">SW-CORE-01 — Catalyst 9300-48P</div>
        <div class="mt-1 flex items-center gap-1.5 text-[10px] font-mono text-slate-300 bg-slate-900/90 px-2 py-0.5 rounded border border-slate-700/80">
          <span class="text-slate-500">S/N:</span>
          <span id="tt-sn" class="text-cyan-300 font-semibold tracking-wider">FCW2419L08A</span>
        </div>
        <div id="tt-meta" class="text-[11px] font-mono text-slate-400 mt-1">48x RJ45 PoE+ | 4x 10G SFP+ | Click to Edit</div>
      </div>
    </div>

    <!-- Collapsible Toggle Button for Mobile / Compact View -->
    <button id="toggle-hud-btn" class="absolute top-3 right-3 z-30 glass-panel p-2.5 rounded-xl text-slate-200 hover:text-cyan-400 transition sm:hidden" title="Toggle Controls">
      <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 6V4m0 2a2 2 0 100 4m0-4a2 2 0 110 4m-6 8a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4m6 6v10m6-2a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4" />
      </svg>
    </button>

    <!-- Right-Side Interactive Control & Hardware Management Drawer -->
    <div id="control-panel" class="absolute top-3 right-3 z-20 w-80 max-w-[calc(100%-1.5rem)] max-h-full flex flex-col glass-panel rounded-2xl p-4 transition-all duration-300 hidden sm:flex">
      <!-- Drawer Tabs -->
      <div class="flex items-center gap-1 p-1 bg-slate-900/90 rounded-xl border border-slate-800 mb-3 shrink-0">
        <button id="tab-hardware" class="flex-1 py-1.5 text-xs font-semibold rounded-lg bg-cyan-500/20 text-cyan-300 border border-cyan-500/30 transition">
          Hardware & Inventory
        </button>
        <button id="tab-rack" class="flex-1 py-1.5 text-xs font-medium rounded-lg text-slate-400 hover:text-slate-200 transition">
          Rack & Telemetry
        </button>
      </div>

      <!-- Notification Banner (for auto-shifts / slot warnings) -->
      <div id="hud-toast" class="hidden mb-2.5 px-3 py-2 rounded-xl bg-amber-500/15 border border-amber-500/35 text-[11px] text-amber-200 shrink-0"></div>

      <!-- TAB 1: HARDWARE, SWITCH CONFIG & UPS MANAGEMENT -->
      <div id="view-hardware" class="flex-1 overflow-y-auto custom-scroll pr-1 space-y-3">
        <!-- Selected Switch Editor Card -->
        <div id="switch-editor-card" class="relative overflow-hidden p-3.5 rounded-2xl bg-gradient-to-b from-slate-900/95 to-slate-950/95 border border-cyan-500/35 shadow-lg shadow-cyan-950/20 space-y-2.5">
          <div class="absolute top-0 inset-x-0 h-0.5 bg-gradient-to-r from-cyan-400 via-sky-400 to-amber-400"></div>
          <div class="flex items-center justify-between">
            <span class="text-[11px] font-mono font-bold uppercase tracking-wider text-cyan-400 flex items-center gap-1.5">
              <span class="w-2 h-2 rounded-full bg-cyan-400 animate-pulse"></span>
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
                class="w-full px-2.5 py-1.5 rounded-lg bg-slate-950/90 border border-slate-700/90 text-xs font-mono text-white focus:outline-none focus:border-cyan-400 transition" />
            </div>
            <div class="grid grid-cols-3 gap-2">
              <div class="col-span-2">
                <label class="block text-[10px] uppercase tracking-wider text-slate-400 mb-1">Management IP</label>
                <input id="input-ip" type="text" maxlength="18" placeholder="10.10.1.11"
                  class="w-full px-2.5 py-1.5 rounded-lg bg-slate-950/90 border border-slate-700/90 text-xs font-mono text-amber-300 focus:outline-none focus:border-cyan-400 transition" />
              </div>
              <div>
                <label class="block text-[10px] uppercase tracking-wider text-slate-400 mb-1">Rack Slot</label>
                <select id="select-slot" class="w-full px-1.5 py-1.5 rounded-lg bg-slate-950/90 border border-slate-700/90 text-xs font-mono text-cyan-300 focus:outline-none focus:border-cyan-400 transition"></select>
              </div>
            </div>
            <div>
              <div class="flex items-center justify-between mb-1">
                <label class="text-[10px] uppercase tracking-wider text-slate-400 flex items-center gap-1">
                  <svg xmlns="http://www.w3.org/2000/svg" class="w-3 h-3 text-cyan-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 7h.01M7 3h5c.512 0 1.024.195 1.414.586l7 7a2 2 0 010 2.828l-7 7a2 2 0 01-2.828 0l-7-7A1.994 1.994 0 013 12V7a4 4 0 014-4z" />
                  </svg>
                  Switch Serial Number (S/N)
                </label>
                <button id="btn-copy-sw-sn" type="button" title="Copy Serial Number" class="text-[10px] font-mono text-cyan-400 hover:text-cyan-300 flex items-center gap-1 transition">
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
          <button id="btn-add-switch" class="w-full py-2 px-3 rounded-xl text-xs font-semibold bg-cyan-500/20 text-cyan-300 border border-cyan-500/40 hover:bg-cyan-500/30 transition flex items-center justify-center gap-1.5">
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
            <button data-ups="3000" class="ups-btn py-1.5 px-2 rounded-lg text-[11px] font-semibold bg-cyan-500/25 text-cyan-300 border border-cyan-500/50 transition">
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

          <p class="text-[10px] text-slate-400 leading-tight">
            UPS always mounts at bottom U01+. Adding/upgrading automatically shifts lower switches upward.
          </p>
        </div>

        <!-- Installed Equipment Inventory List (U24 -> U01) -->
        <div>
          <div class="flex items-center justify-between mb-1.5">
            <span class="text-[11px] font-semibold uppercase tracking-wider text-slate-400">Rack Inventory (Drag to Sort)</span>
            <span id="free-u-count" class="text-[10px] font-mono text-slate-400">12U Free</span>
          </div>
          <div id="inventory-list" class="space-y-1.5"></div>
        </div>
      </div>

      <!-- TAB 2: RACK ENCLOSURE & TELEMETRY CONTROLS -->
      <div id="view-rack" class="hidden flex-1 overflow-y-auto custom-scroll pr-1 space-y-3">
        <div class="space-y-2.5">
          <div class="flex items-center justify-between">
            <span class="text-xs text-slate-300">Front Glass Door</span>
            <button id="btn-door" class="px-3 py-1.5 rounded-xl text-xs font-medium bg-cyan-500/20 text-cyan-300 border border-cyan-500/40 hover:bg-cyan-500/30 transition">
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
            <span id="intensity-val" class="text-xs font-mono text-cyan-400">75%</span>
          </div>
          <input id="slider-intensity" type="range" min="5" max="100" value="75" class="w-full" />
        </div>

        <!-- Helper Hint -->
        <div class="pt-2.5 border-t border-slate-800/80 text-[11px] text-slate-400 space-y-1.5">
          <p>• <strong class="text-slate-300">Geser Kamera (Pan):</strong> Klik kiri + drag mouse untuk menggeser posisi kamera.</p>
          <p>• <strong class="text-slate-300">Putar 3D (Orbit):</strong> Klik kanan + drag (atau Shift + drag) untuk memutar sudut pandang.</p>
          <p>• <strong class="text-slate-300">Cursor Zoom:</strong> Scroll wheel memperbesar tepat ke arah kursor mouse.</p>
          <p>• <strong class="text-slate-300">Edit & Pintu Kaca:</strong> Klik switch atau pintu kaca di 3D untuk berinteraksi.</p>
        </div>
      </div>
    </div>

    <!-- Bottom Camera Presets Dock -->
    <div class="absolute bottom-3 left-1/2 -translate-x-1/2 z-20 max-w-[calc(100%-1.5rem)]">
      <div class="glass-panel rounded-2xl p-1.5 flex items-center gap-1.5 overflow-x-auto">
        <button data-cam="angled" class="cam-btn active px-3.5 py-2 rounded-xl text-xs font-medium bg-cyan-500 text-slate-950 font-semibold shadow-lg shadow-cyan-500/25 transition whitespace-nowrap">
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
        <button data-cam="rear" class="cam-btn px-3.5 py-2 rounded-xl text-xs font-medium text-slate-300 hover:bg-slate-800/80 transition whitespace-nowrap">
          Rear & PDU
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.rack3d-register {
  position: absolute;
  top: 10px;
  bottom: 10px;
  left: 10px;
  z-index: 15;
  width: 232px;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border-radius: 10px;
  border: 1px solid rgb(148 163 184 / 0.18);
  background: rgb(2 6 23 / 0.72);
  backdrop-filter: blur(8px);
  box-shadow: 0 10px 30px rgb(0 0 0 / 0.45);
}
.rack3d-host {
  position: relative;
  overflow: hidden;
  background-color: #06090f;
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
  border: 1px solid rgba(56, 189, 248, 0.2);
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
  background: rgba(56, 189, 248, 0.35);
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
