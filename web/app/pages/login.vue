<script setup lang="ts">
definePageMeta({ layout: 'auth' })

const { login } = useAuth()
const route = useRoute()

const username = ref('')
const password = ref('')
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
</script>

<template>
  <div class="min-h-screen flex items-center justify-center bg-default">
    <UCard class="w-full max-w-sm">
      <template #header>
        <div class="text-center py-2">
          <h1 class="text-xl font-bold">INFRA-CAP</h1>
          <p class="text-sm text-muted mt-1">Internal App</p>
        </div>
      </template>

      <UForm :state="{ username, password }" class="space-y-4" @submit="submit">
        <UFormField label="Username" required>
          <UInput v-model="username" placeholder="admin" autocomplete="username" class="w-full" />
        </UFormField>

        <UFormField label="Password" required>
          <UInput
            v-model="password"
            type="password"
            placeholder="••••••••"
            autocomplete="current-password"
            class="w-full"
          />
        </UFormField>

        <UAlert v-if="error" color="error" variant="soft" icon="i-lucide-circle-alert" :title="error" />

        <UButton type="submit" block :loading="loading" label="Sign In" />
      </UForm>
    </UCard>
  </div>
</template>
