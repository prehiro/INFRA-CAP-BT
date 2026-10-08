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
    'focus-visible:ring-2 focus-visible:ring-[#00dc82] transition-shadow',
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
      ctx.fillStyle = 'rgba(0, 220, 130, 0.75)'
      ctx.fill()
    }

    for (let i = 0; i < particles.length; i++) {
      for (let j = i + 1; j < particles.length; j++) {
        const a = particles[i]
        const b = particles[j]
        const d = Math.hypot(a.x - b.x, a.y - b.y)
        if (d < LINK) {
          ctx.strokeStyle = `rgba(0, 220, 130, ${(1 - d / LINK) * 0.22})`
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
          header: 'border-b border-[#1e2a44]',
          body: 'p-6'
        }"
      >
        <template #header>
          <div class="text-center py-2">
            <div class="logo mx-auto mb-3" aria-hidden="true">
              <span class="logo-ring" />
              <span class="logo-ring logo-ring--2" />
              <span class="logo-icon">
                <UIcon name="streamline-cyber:network" class="size-8" />
              </span>
            </div>
            <h1 class="text-xl font-bold tracking-wide text-slate-100">INFRA-BTCAP</h1>
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
            class="sign-btn bg-[#00dc82] hover:bg-[#1af09a] text-[#04130c] font-semibold"
          />
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
.orb-1 { width: 420px; height: 420px; background: #00dc82; top: -120px; left: -100px; animation: float1 18s ease-in-out infinite; }
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

.logo {
  /* Anchor for the rings below, which are absolutely positioned against the badge. */
  position: relative;
  width: 44px; height: 44px;
  display: grid; place-items: center;
  border-radius: 12px;
  color: #00dc82;
  background: rgba(0, 220, 130, 0.1);
  border: 1px solid rgba(0, 220, 130, 0.3);
  animation: pulse 3s ease-in-out infinite;
}
@keyframes pulse {
  0%,100% { box-shadow: 0 0 18px rgba(0, 220, 130, 0.2); }
  50%     { box-shadow: 0 0 32px rgba(0, 220, 130, 0.45); }
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
  border: 1px solid rgba(0, 220, 130, 0.6);
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

/* The glyph itself turns slowly while its brightness breathes, so the mark reads as live rather
   than as a static sticker inside a glowing box.
   THE TWO ANIMATIONS LIVE ON TWO ELEMENTS ON PURPOSE: both would otherwise want `transform`, and
   on a single element the later `animation` shorthand silently wins per property - the rotation
   and the sway would fight and one would simply not run. So the wrapper owns the rotation and the
   icon only owns opacity, which cannot collide.
   NO SCALING: an SVG glyph rasterises at the intermediate size while it is being scaled and reads
   soft mid-motion, the same reason the app never scales text. Rotation and opacity are both
   compositor-friendly and stay crisp.
   18s per turn is slow enough to read as "steady signal" instead of a spinning logo, and the
   3.5s sway keeps it from looking like a motor. */
.logo-icon {
  display: grid;
  place-items: center;
  animation: logo-spin 18s linear infinite;
}

.logo-icon > * {
  animation: logo-sway 3.5s ease-in-out infinite;
}

@keyframes logo-spin {
  to { transform: rotate(360deg); }
}

@keyframes logo-sway {
  0%, 100% { opacity: 0.7; }
  50%      { opacity: 1; }
}

/* sign-in button shine */
.sign-btn { position: relative; overflow: hidden; box-shadow: 0 8px 24px -8px rgba(0, 220, 130, 0.6); }
.sign-btn::after {
  content: ''; position: absolute; top: 0; left: -60%;
  width: 40%; height: 100%;
  background: linear-gradient(100deg, transparent, rgba(255, 255, 255, 0.45), transparent);
  transform: skewX(-20deg);
  transition: left 0.6s;
  pointer-events: none;
}
.sign-btn:hover::after { left: 130%; }

.fade-enter-active, .fade-leave-active { transition: opacity 0.25s, transform 0.25s; }
.fade-enter-from, .fade-leave-to { opacity: 0; transform: translateY(-4px); }

@media (prefers-reduced-motion: reduce) {
  .orb, .bg-grid, .logo, .card-wrap, .logo-icon, .logo-icon > *, .logo-ring { animation: none; }
}
</style>