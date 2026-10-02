<script setup lang="ts">
import type { Role, User } from '~/types'

const toast = useToast()
const { user: me, isAdmin } = useAuth()

const users = ref<User[]>([])
const roles = ref<Role[]>([])
const loading = ref(false)

const modalOpen = ref(false)
const editing = ref<User | null>(null)
const saving = ref(false)
const formErrors = reactive<Record<string, string>>({})

const form = reactive({
  username: '',
  password: '',
  email: '',
  fullName: '',
  isActive: true,
  roleIds: [] as number[]
})

async function load() {
  loading.value = true
  try {
    const [u, r] = await Promise.all([apiListUsers(), apiListRoles()])
    users.value = u
    roles.value = r
  } catch (e: any) {
    toast.add({ title: 'Gagal memuat user', description: e?.data?.message || e?.message, color: 'error' })
  } finally {
    loading.value = false
  }
}

function openCreate() {
  editing.value = null
  Object.assign(form, { username: '', password: '', email: '', fullName: '', isActive: true, roleIds: [] })
  Object.keys(formErrors).forEach(k => delete formErrors[k])
  modalOpen.value = true
}

function openEdit(u: User) {
  editing.value = u
  Object.assign(form, {
    username: u.username,
    password: '',
    email: u.email,
    fullName: u.fullName,
    isActive: u.isActive,
    roleIds: u.roles.map(r => r.id)
  })
  Object.keys(formErrors).forEach(k => delete formErrors[k])
  modalOpen.value = true
}

async function save() {
  saving.value = true
  Object.keys(formErrors).forEach(k => delete formErrors[k])

  // A user with no role can log in but sees nothing useful, which reads as a broken
  // account. Require at least one role up front instead of failing confusingly later.
  if (!form.roleIds.length) {
    formErrors.roleIds = 'Pilih minimal satu role'
    saving.value = false
    return
  }

  try {
    if (editing.value) {
      await apiUpdateUser(editing.value.id, {
        email: form.email,
        fullName: form.fullName,
        isActive: form.isActive,
        // Only send password when the admin actually typed a new one.
        password: form.password || null,
        roleIds: form.roleIds
      })
      toast.add({ title: 'User updated', color: 'success' })
    } else {
      await apiCreateUser({
        username: form.username,
        password: form.password,
        email: form.email,
        fullName: form.fullName,
        isActive: form.isActive,
        roleIds: form.roleIds
      })
      toast.add({ title: 'User created', color: 'success' })
    }
    modalOpen.value = false
    await load()
  } catch (e: any) {
    if (e?.data?.errors) {
      Object.assign(formErrors, e.data.errors)
    }
    // A silent catch here made a broken submit look like a no-op, so the reason is
    // always surfaced. Server validation errors arrive as a {field: message} map.
    const serverErrors = e?.data?.errors as Record<string, string> | undefined
    const description = e?.data?.message
      ?? (serverErrors ? Object.values(serverErrors).join('; ') : null)
      ?? e?.message
      ?? 'Unknown error'

    toast.add({ title: 'Gagal menyimpan user', description, color: 'error' })
  } finally {
    saving.value = false
  }
}

async function removeUser(u: User) {
  if (!confirm(`Hapus user "${u.username}"?`)) return
  try {
    await apiDeleteUser(u.id)
    toast.add({ title: 'User deleted', color: 'success' })
    await load()
  } catch (e: any) {
    toast.add({ title: 'Delete failed', description: e?.data?.message || e?.message, color: 'error' })
  }
}

function toggleActive(u: User) {
  return u.isActive
}

const roleItems = computed(() => roles.value.map(r => ({ label: r.name, value: r.id })))

onMounted(() => {
  if (isAdmin.value) load()
})
</script>

<template>
  <UDashboardPanel>
    <template #header>
      <!-- PageHeader carries the sidebar collapse control in the navbar's #leading slot,
           exactly as the Nuxt dashboard template does on every page. -->
      <PageHeader title="User Management">
        <template #actions>
          <UButton v-if="isAdmin" icon="i-lucide-plus" label="Add User" @click="openCreate" />
        </template>
      </PageHeader>
    </template>

    <template #body>
      <div class="space-y-4">
    <UAlert
      v-if="!isAdmin"
      color="warning"
      variant="soft"
      icon="i-lucide-lock"
      title="Restricted access"
      description="Only an Admin can view and manage users."
    />

    <UCard v-else>
      <div class="overflow-x-auto">
        <table class="w-full text-sm">
          <thead>
            <tr class="text-left border-b border-default">
              <th class="py-2 px-3 font-medium text-muted">Username</th>
              <th class="py-2 px-3 font-medium text-muted">Full Name</th>
              <th class="py-2 px-3 font-medium text-muted">Email</th>
              <th class="py-2 px-3 font-medium text-muted">Role</th>
              <th class="py-2 px-3 font-medium text-muted">Status</th>
              <th class="py-2 px-3 font-medium text-muted">Created</th>
              <th class="py-2 px-3 text-right font-medium text-muted">Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="u in users" :key="u.id" class="border-b border-default last:border-0">
              <td class="py-2 px-3 font-medium">
                {{ u.username }}
                <UBadge v-if="u.id === me?.id" color="primary" variant="soft" class="ml-1">You</UBadge>
              </td>
              <td class="py-2 px-3">{{ u.fullName || '-' }}</td>
              <td class="py-2 px-3">{{ u.email || '-' }}</td>
              <td class="py-2 px-3">
                <div class="flex gap-1 flex-wrap">
                  <UBadge v-for="r in u.roles" :key="r.id" color="neutral" variant="soft" size="sm">
                    {{ r.name }}
                  </UBadge>
                  <span v-if="!u.roles.length" class="text-muted">-</span>
                </div>
              </td>
              <td class="py-2 px-3">
                <UBadge :color="toggleActive(u) ? 'success' : 'error'" variant="soft" size="sm">
                  {{ u.isActive ? 'Active' : 'Inactive' }}
                </UBadge>
              </td>
              <td class="py-2 px-3 text-muted">
                {{ u.createdBy || '-' }}
                <span class="text-xs">{{ new Date(u.createdAt).toLocaleDateString('en-GB') }}</span>
              </td>
              <td class="py-2 px-3 text-right whitespace-nowrap">
                <UButton icon="i-lucide-pencil" size="xs" color="neutral" variant="ghost" @click="openEdit(u)" />
                <UButton
                  v-if="u.username !== 'admin'"
                  icon="i-lucide-trash-2"
                  size="xs"
                  color="error"
                  variant="ghost"
                  @click="removeUser(u)"
                />
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </UCard>

    <!--
      Inline panel rather than UModal: the UModal #footer slot swallows the @click
      handler on the submit button (verified: onclick was null on the rendered node),
      so clicks silently did nothing. UForm + type="submit" is the pattern that works.
    -->
    <UCard v-if="modalOpen" class="mt-6">
      <template #header>
        <h2 class="font-semibold">{{ editing ? 'Edit User' : 'Add User' }}</h2>
      </template>

      <UForm :state="form" class="space-y-4" @submit="save">
        <UFormField
          label="Username"
          required
          :error="formErrors.username"
          :help="editing ? 'Username tidak bisa diubah' : undefined"
        >
          <UInput v-model="form.username" :disabled="!!editing" class="w-full" placeholder="jsmith" />
        </UFormField>

        <UFormField
          :label="editing ? 'New Password' : 'Password'"
          :required="!editing"
          :error="formErrors.password"
          :help="editing ? 'Leave blank to keep the current password' : 'At least 6 characters'"
        >
          <UInput v-model="form.password" type="password" class="w-full" placeholder="••••••••" />
        </UFormField>

        <UFormField label="Full Name" :error="formErrors.fullName">
          <UInput v-model="form.fullName" class="w-full" placeholder="John Smith" />
        </UFormField>

        <UFormField label="Email" :error="formErrors.email">
          <UInput v-model="form.email" type="email" class="w-full" placeholder="user@company.local" />
        </UFormField>

        <UFormField label="Role" :error="formErrors.roleIds">
          <USelectMenu
            v-model="form.roleIds"
            :items="roleItems"
            multiple
            placeholder="Select a role"
            class="w-full"
          />
        </UFormField>

        <UFormField label="Status">
          <USwitch v-model="form.isActive" label="User is active" />
        </UFormField>

        <div class="flex justify-end gap-2 pt-2">
          <UButton label="Cancel" color="neutral" variant="ghost" type="button" @click="modalOpen = false" />
          <UButton type="submit" :label="editing ? 'Save' : 'Create User'" :loading="saving" />
        </div>
      </UForm>
    </UCard>
      </div>
    </template>
  </UDashboardPanel>
</template>
