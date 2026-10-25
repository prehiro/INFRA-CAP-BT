<script setup lang="ts">
definePageMeta({ layout: 'auth' })

const { login } = useAuth()
const route = useRoute()

const username = ref('')
const password = ref('')
const showPassword = ref(false)
const loading = ref(false)
const error = ref('')

async function submit() {
  error.value = ''
  loading.value = true
  try {
    await login(username.value, password.value)
    const redirect = (route.query.redirect as string) || '/'
    await navigateTo(redirect)
  } catch (e: any) {
    // Surface API validation messages, but never a raw fetch/CORS/network error to the user.
    const status = e?.status ?? e?.response?.status
    if (status === 401) {
      error.value = 'Incorrect username or password.'
    } else if (status === 400 && e?.data?.message) {
      error.value = e.data.message
    } else if (status === 403) {
      error.value = 'Your account does not have access to this application.'
    } else {
      error.value = 'Cannot reach the server. Check your network connection or contact the administrator.'
      console.error('[login]', e)
    }
  } finally {
    loading.value = false
  }
}

/* ---------- Styling for Nuxt UI components ---------- */
const inputUi = {
  base: 'h-10 bg-[#080e1c]/60 text-slate-100 placeholder:text-[#4d628a] ring-1 ring-inset ring-[#2a3a5e] ' +
    'focus-visible:ring-2 focus-visible:ring-primary transition-shadow',
  leadingIcon: 'text-[#7b8fb5]'
}

/* ---------- Animated background (particle network) ---------- */
const canvasRef = ref<HTMLCanvasElement | null>(null)
let raf = 0
let cleanup: (() => void) | null = null

onMounted(() => {
  const canvas = canvasRef.value!
  const ctx = canvas.getContext('2d')!
  const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches
  const dpr = Math.min(window.devicePixelRatio || 1, 2)

  let w = 0
  let h = 0
  const mouse = { x: -9999, y: -9999 }

  type P = { x: number; y: number; vx: number; vy: number; r: number }
  let particles: P[] = []

  const resize = () => {
    w = window.innerWidth
    h = window.innerHeight
    canvas.width = w * dpr
    canvas.height = h * dpr
    canvas.style.width = w + 'px'
    canvas.style.height = h + 'px'
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0)
    const count = Math.min(Math.floor((w * h) / 16000), 110)
    particles = Array.from({ length: count }, () => ({
      x: Math.random() * w,
      y: Math.random() * h,
      vx: (Math.random() - 0.5) * 0.35,
      vy: (Math.random() - 0.5) * 0.35,
      r: Math.random() * 1.6 + 0.6
    }))
  }

  const LINK = 140
  // The canvas cannot resolve `var(--ui-primary)` or `color-mix()` in a fillStyle, so the
  // accent is read from the CSS custom property and used as a literal. The token is
  // re-read on every draw, so a theme change is picked up on the next frame without a
  // reload.
  const accentProbe = document.createElement('span')
  accentProbe.style.color = 'var(--ui-primary)'
  accentProbe.style.position = 'absolute'
  accentProbe.style.pointerEvents = 'none'
  accentProbe.style.visibility = 'hidden'
  document.body.appendChild(accentProbe)
  const readAccent = () => getComputedStyle(accentProbe).color
  const withAlpha = (alpha: number) => {
    const base = readAccent()
    const match = base.match(/[\d.]+/g)
    if (!match) return base
    if (base.startsWith('oklch')) {
      const [l, c, h] = match.map(Number)
      return `oklch(${l} ${c} ${h} / ${alpha})`
    }
    const [r, g, b] = match.map(Number)
    return `rgba(${r}, ${g}, ${b}, ${alpha})`
  }
  const draw = () => {
    ctx.clearRect(0, 0, w, h)

    for (const p of particles) {
      if (!reduce) {
        p.x += p.vx
        p.y += p.vy
        if (p.x < 0 || p.x > w) p.vx *= -1
        if (p.y < 0 || p.y > h) p.vy *= -1

        const dx = mouse.x - p.x
        const dy = mouse.y - p.y
        if (Math.hypot(dx, dy) < 160) {
          p.x += dx * 0.004
          p.y += dy * 0.004
        }
      }
      ctx.beginPath()
      ctx.arc(p.x, p.y, p.r, 0, Math.PI * 2)
      // The accent is a semantic token, so the particle network follows the user's colour.
      ctx.fillStyle = withAlpha(0.75)
      ctx.fill()
    }

    for (let i = 0; i < particles.length; i++) {
      for (let j = i + 1; j < particles.length; j++) {
        const a = particles[i]
        const b = particles[j]
        const d = Math.hypot(a.x - b.x, a.y - b.y)
        if (d < LINK) {
          ctx.strokeStyle = withAlpha((1 - d / LINK) * 0.22)
          ctx.lineWidth = 1
          ctx.beginPath()
          ctx.moveTo(a.x, a.y)
          ctx.lineTo(b.x, b.y)
          ctx.stroke()
        }
      }
      const m = Math.hypot(particles[i].x - mouse.x, particles[i].y - mouse.y)
      if (m < 170) {
        ctx.strokeStyle = `rgba(110, 160, 255, ${(1 - m / 170) * 0.4})`
        ctx.beginPath()
        ctx.moveTo(particles[i].x, particles[i].y)
        ctx.lineTo(mouse.x, mouse.y)
        ctx.stroke()
      }
    }

    if (!reduce) raf = requestAnimationFrame(draw)
  }

  const onMove = (e: MouseEvent) => { mouse.x = e.clientX; mouse.y = e.clientY }
  const onLeave = () => { mouse.x = -9999; mouse.y = -9999 }

  resize()
  draw()
  window.addEventListener('resize', resize)
  window.addEventListener('mousemove', onMove)
  window.addEventListener('mouseleave', onLeave)

  cleanup = () => {
    accentProbe.remove()
    cancelAnimationFrame(raf)
    window.removeEventListener('resize', resize)
    window.removeEventListener('mousemove', onMove)
    window.removeEventListener('mouseleave', onLeave)
  }
})

onBeforeUnmount(() => cleanup?.())
</script>

<template>
  <div class="login-root min-h-screen flex items-center justify-center p-6">
    <!-- Background layers -->
    <canvas ref="canvasRef" class="bg-canvas" aria-hidden="true" />
    <div class="bg-grid" aria-hidden="true" />
    <div class="orb orb-1" aria-hidden="true" />
    <div class="orb orb-2" aria-hidden="true" />
    <div class="orb orb-3" aria-hidden="true" />
    <div class="bg-vignette" aria-hidden="true" />

    <!-- Card -->
    <div class="card-wrap w-full max-w-sm">
      <UCard
        variant="outline"
        :ui="{
          root: 'rounded-xl bg-[#0f172a]/80 backdrop-blur-xl ring-1 ring-white/10 shadow-[0_30px_80px_-20px_rgba(0,0,0,0.7)] text-slate-100',
          header: 'border-b border-[#1e2a44] px-6 py-0',
          body: 'p-6'
        }"
      >
        <template #header>
          <div class="login-header text-center">
            <div class="logo-wrap mx-auto" aria-hidden="true">
              <span class="logo-circuit">
                <AiCircuit />
              </span>
              <div class="logo mx-auto">
                <span class="logo-ring" />
                <span class="logo-ring logo-ring--2" />
                <span class="logo-icon">
                  <LoginNetworkIcon />
                </span>
              </div>
            </div>
            <h1 class="text-xl font-bold tracking-wide text-slate-100"><AnimatedBrandText intro :tracking="0.025" :line-height="1.4" :scramble-weight="600" /></h1>
            <p class="text-sm text-[#7b8fb5] mt-1">Internal App ISD Capacitor</p>
          </div>
        </template>

        <UForm :state="{ username, password }" class="space-y-4" @submit="submit">
          <UFormField label="Username" required :ui="{ label: 'text-slate-100 font-semibold' }">
            <UInput
              v-model="username"
              placeholder="admin"
              autocomplete="username"
              icon="i-lucide-user"
              :disabled="loading"
              :ui="inputUi"
              class="w-full"
            />
          </UFormField>

          <UFormField label="Password" required :ui="{ label: 'text-slate-100 font-semibold' }">
            <UInput
              v-model="password"
              :type="showPassword ? 'text' : 'password'"
              placeholder="••••••••"
              autocomplete="current-password"
              icon="i-lucide-lock"
              :disabled="loading"
              :ui="{ ...inputUi, trailing: 'pe-1' }"
              class="w-full"
            >
              <template #trailing>
                <UButton
                  color="neutral"
                  variant="link"
                  size="sm"
                  :icon="showPassword ? 'i-lucide-eye-off' : 'i-lucide-eye'"
                  :aria-label="showPassword ? 'Hide password' : 'Show password'"
                  :aria-pressed="showPassword"
                  class="text-[#7b8fb5] hover:text-slate-100"
                  @click="showPassword = !showPassword"
                />
              </template>
            </UInput>
          </UFormField>

          <Transition name="fade">
            <UAlert
              v-if="error"
              color="error"
              variant="soft"
              icon="i-lucide-circle-alert"
              :title="error"
            />
          </Transition>

          <UButton
            type="submit"
            block
            size="lg"
            :loading="loading"
            label="Sign In"
            :disabled="loading"
            class="sign-btn bg-primary hover:bg-primary/90 text-[#04130c] font-semibold"
          >
            <template #trailing>
              <svg class="sign-arrow size-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="M4 12h15m-6-6 6 6-6 6" />
              </svg>
            </template>
          </UButton>
        </UForm>
      </UCard>
    </div>
  </div>
</template>

<style scoped>
.login-root {
  position: relative;
  overflow: hidden;
  background: radial-gradient(ellipse at 50% 0%, #15213d 0%, #0f172a 45%, #0b1220 100%);
}

/* ---------- Background ---------- */
.bg-canvas { position: absolute; inset: 0; z-index: 0; }

.bg-grid {
  position: absolute; inset: 0; z-index: 0;
  background-image:
    linear-gradient(rgba(120, 150, 210, 0.06) 1px, transparent 1px),
    linear-gradient(90deg, rgba(120, 150, 210, 0.06) 1px, transparent 1px);
  background-size: 48px 48px;
  mask-image: radial-gradient(ellipse at center, #000 20%, transparent 75%);
  animation: grid-move 24s linear infinite;
}
@keyframes grid-move { to { background-position: 48px 48px, 48px 48px; } }

.orb {
  position: absolute; z-index: 0;
  border-radius: 50%;
  filter: blur(90px);
  opacity: 0.35;
  will-change: transform;
}
.orb-1 { width: 420px; height: 420px; background: var(--ui-primary); top: -120px; left: -100px; animation: float1 18s ease-in-out infinite; }
.orb-2 { width: 480px; height: 480px; background: #3b5bdb; bottom: -160px; right: -120px; animation: float2 22s ease-in-out infinite; }
.orb-3 { width: 260px; height: 260px; background: #0ea5a4; top: 55%; left: 55%; opacity: 0.2; animation: float3 26s ease-in-out infinite; }

@keyframes float1 { 0%,100% { transform: translate(0,0) scale(1); } 50% { transform: translate(120px, 90px) scale(1.15); } }
@keyframes float2 { 0%,100% { transform: translate(0,0) scale(1); } 50% { transform: translate(-130px, -80px) scale(1.1); } }
@keyframes float3 { 0%,100% { transform: translate(0,0); } 50% { transform: translate(-160px, -120px); } }

.bg-vignette {
  position: absolute; inset: 0; z-index: 0; pointer-events: none;
  background: radial-gradient(ellipse at center, transparent 40%, rgba(5, 9, 18, 0.65) 100%);
}

/* ---------- Card wrapper ---------- */
.card-wrap {
  position: relative; z-index: 1;
  animation: rise 0.7s cubic-bezier(0.2, 0.8, 0.2, 1) both;
}
@keyframes rise {
  from { opacity: 0; transform: translateY(24px) scale(0.98); }
  to   { opacity: 1; transform: none; }
}

/* The badge sits inside a wrapper so the AI circuit artwork can live BEHIND it without
   touching the badge's own box, pulse or rings.
   The wrapper is only as big as the badge itself: the halo is a fixed-size box centred on
   it, so it no longer RESERVES vertical space. A 132px wrapper used to hold a 44px badge,
   which left 44px of empty air above and below the icon and pushed the wordmark down.
   The halo is 112px: the artwork's visible band then measures 30.6..57.1px from the centre,
   so the circuit reads as a ring AROUND the badge instead of a thin sliver. */
/* The SPACING is measured from the ARTWORK, not from the badge.
   The circuit artwork reaches 36px beyond the 44px badge on every side, so padding sized
   to the badge left the halo sitting 2px from the card edge and running 18px INTO the
   wordmark - the artwork, not the badge, is what the eye reads as the icon.
   The visible gap is therefore 16px on each side of the ARTWORK. The numbers work out
   like this: the artwork overhangs the badge by 36px, so the header needs 36+16 = 52px of
   padding above the badge and 36+16 = 52px of margin below it.
   NOTE: the card is vertically centred, so one extra pixel of top padding moves the card
   edge up by one pixel AND the artwork down by one pixel - the top gap grows by 2px per
   pixel of padding. That is why the top and bottom numbers are not symmetric even though
   the two gaps are. */
.login-header {
  padding: 51px 0 18px;
}
/* The card header carried the vendor's own 16px vertical padding, which stacked on top of
   the space the halo needs and left the icon floating. It is now zero here, and the
   header's own padding is the halo's overhang and nothing more. */
.logo-wrap {
  position: relative;
  width: 44px; height: 44px;
  display: grid; place-items: center;
  /* 36px of artwork overhang below the badge + a 16px visible gap before the wordmark. */
  margin-bottom: 49px;
}
.logo-circuit {
  position: absolute;
  left: 50%; top: 50%;
  width: 112px; height: 112px;
  translate: -50% -50%;
  pointer-events: none;
  animation: logo-circuit-fade 6s ease-in-out infinite;
}
.logo-circuit :deep(.ai-circuit) {
  width: 100%;
  height: 100%;
}
@keyframes logo-circuit-fade {
  0%, 100% { opacity: .5; }
  50% { opacity: .95; }
}

.logo {
  /* Anchor for the rings below, which are absolutely positioned against the badge. */
  position: relative;
  width: 44px; height: 44px;
  display: grid; place-items: center;
  border-radius: 12px;
  /* The accent is a semantic token, so the badge follows the colour the user picked. */
  color: var(--ui-primary);
  background: color-mix(in oklab, var(--ui-primary) 10%, transparent);
  border: 1px solid color-mix(in oklab, var(--ui-primary) 30%, transparent);
  animation: pulse 3s ease-in-out infinite;
}
@keyframes pulse {
  0%,100% { box-shadow: 0 0 18px color-mix(in oklab, var(--ui-primary) 20%, transparent); }
  50%     { box-shadow: 0 0 32px color-mix(in oklab, var(--ui-primary) 45%, transparent); }
}

/* Two rings travel out from the badge and back in again. HIRO asked for exactly that motion and
   for the badge itself to stay as it is, which rules out animating the badge's own size or glow -
   hence separate absolutely positioned children. Scaling `.logo` instead would also scale the
   glyph sitting inside it, and scaling a glyph is the one thing this app never does (it
   rasterises at the in-between size and reads soft while it moves).
   They go back to scale(1) at the end of every cycle, so the motion reads as out AND back, not as
   a one-way ping. Opacity is already near zero at the outer end of the travel, which is what keeps
   the far ring from drawing a visible line across the INFRA-BTCAP wordmark just below.
   The two rings differ in duration rather than in amplitude so they drift slowly in and out of
   phase with each other: the field keeps moving without the two ever looking like one thick ring. */
.logo-ring {
  position: absolute;
  inset: 0;
  border-radius: 12px;
  border: 1px solid color-mix(in oklab, var(--ui-primary) 60%, transparent);
  pointer-events: none;
  will-change: transform, opacity;
  animation: logo-ring 2.8s ease-in-out infinite;
}

.logo-ring--2 {
  animation-duration: 3.4s;
  animation-delay: -1.7s;
}

@keyframes logo-ring {
  0%, 100% { transform: scale(1);    opacity: 0.45; }
  50%      { transform: scale(1.6);  opacity: 0.05; }
}

.logo-icon { display: grid; place-items: center; }

/* Login-only interaction: lift, traveling highlight, and a forward arrow. */
.sign-btn {
  position: relative; overflow: hidden; isolation: isolate;
  box-shadow: 0 8px 24px -8px color-mix(in oklab, var(--ui-primary) 60%, transparent);
  transition: transform 180ms ease, box-shadow 220ms ease, background-color 180ms ease;
}
.sign-btn::after {
  content: ''; position: absolute; inset: 0; pointer-events: none;
  background: linear-gradient(110deg, transparent 25%, rgba(255,255,255,.35) 50%, transparent 75%);
  transform: translateX(-140%);
}
.sign-arrow { flex: none; }
@keyframes sign-highlight { to { transform: translateX(140%); } }
@keyframes sign-forward {
  0%,100% { transform: translateX(0); opacity: 1; }
  40% { transform: translateX(5px); opacity: 0; }
  41% { transform: translateX(-5px); opacity: 0; }
  75% { transform: translateX(0); opacity: 1; }
}
@media (hover: hover) and (pointer: fine) {
  .sign-btn:not(:disabled):hover {
    transform: translateY(-2px);
    box-shadow: 0 10px 28px -6px color-mix(in oklab, var(--ui-primary) 65%, transparent), inset 0 1px 0 rgba(255,255,255,.25);
  }
  .sign-btn:not(:disabled):hover::after { animation: sign-highlight 700ms ease-out both; }
  .sign-btn:not(:disabled):hover .sign-arrow { animation: sign-forward 650ms ease-in-out; }
}
.sign-btn:not(:disabled):focus-visible .sign-arrow { animation: sign-forward 650ms ease-in-out; }
.sign-btn:not(:disabled):active { transform: translateY(0) scale(.985); }
.sign-btn:disabled .sign-arrow { opacity: .4; }
@media (prefers-reduced-motion: reduce) {
  .sign-btn { transition: none; transform: none !important; }
  .sign-btn::after { display: none; }
  .sign-arrow { animation: none !important; }
}

.fade-enter-active, .fade-leave-active { transition: opacity 0.25s, transform 0.25s; }
.fade-enter-from, .fade-leave-to { opacity: 0; transform: translateY(-4px); }

@media (prefers-reduced-motion: reduce) {
  .orb, .bg-grid, .logo, .card-wrap, .logo-icon, .logo-icon > *, .logo-ring { animation: none; }
}
</style>