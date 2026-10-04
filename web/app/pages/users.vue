<script setup lang="ts">
import type { Role, User } from '~/types'

const toast = useToast()
const { user: me, isAdmin } = useAuth()

const users = ref<User[]>([])
const roles = ref<Role[]>([])
const loading = ref(false)
const search = ref('')

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
    toast.add({ title: 'Could not load users', description: e?.data?.message || e?.message, color: 'error' })
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

  // A user with no role can sign in but sees nothing useful, which reads as a broken
  // account. Require at least one role up front instead of failing confusingly later.
  if (!form.roleIds.length) {
    formErrors.roleIds = 'Select at least one role'
    saving.value = false
    return
  }

  try {
    if (editing.value) {
      await apiUpdateUser(editing.value.id, {
        email: form.email,
        fullName: form.fullName,
        isActive: form.isActive,
        // Only send a password when the admin actually typed a new one.
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
    if (e?.data?.errors) Object.assign(formErrors, e.data.errors)
    // A silent catch here made a broken submit look like a no-op, so the reason is
    // always surfaced. Server validation errors arrive as a {field: message} map.
    const serverErrors = e?.data?.errors as Record<string, string> | undefined
    const description = e?.data?.message
      ?? (serverErrors ? Object.values(serverErrors).join('; ') : null)
      ?? e?.message
      ?? 'Unknown error'

    toast.add({ title: 'Could not save the user', description, color: 'error' })
  } finally {
    saving.value = false
  }
}

/* ------------------------------------------------------------------------------------------
   DELETE CONFIRMATION

   Replaces a bare confirm() - the browser's own unstyleable grey dialog - with a UModal that
   names the account being removed. Same construction as the CCTV row-delete dialog: a soft
   danger wash, a danger disc, and the target's identity repeated so an admin can see they are
   confirming the account they meant rather than the one above it in the list.
   ------------------------------------------------------------------------------------------ */
const showDelete = ref(false)
const deleteTarget = ref<User | null>(null)
const deleting = ref(false)

function askDelete(u: User) {
  deleteTarget.value = u
  showDelete.value = true
}

async function confirmDelete() {
  if (!deleteTarget.value) return
  deleting.value = true
  try {
    await apiDeleteUser(deleteTarget.value.id)
    toast.add({ title: 'User deleted', color: 'success' })
    showDelete.value = false
    deleteTarget.value = null
    await load()
  } catch (e: any) {
    toast.add({ title: 'Delete failed', description: e?.data?.message || e?.message, color: 'error' })
  } finally {
    deleting.value = false
  }
}

const roleItems = computed(() => roles.value.map(r => ({ label: r.name, value: r.id })))

/**
 * Search runs entirely on the loaded list.
 *
 * The list is every user this admin can see - there is no paging and no per-keystroke fetch,
 * so there is nothing to gain from a round trip. Unlike the CCTV register, this haystack is
 * built only from plain text fields, so there is no risk of matching a base64 blob the way the
 * signature columns did there.
 */
const visibleUsers = computed(() => {
  const q = search.value.trim().toLowerCase()
  if (!q) return users.value
  return users.value.filter(u => [u.username, u.fullName, u.email, ...u.roles.map(r => r.name)]
    .some(v => String(v ?? '').toLowerCase().includes(q)))
})

/** Initials for the avatar, with the same fallback chain used by WelcomeBanner and UserMenu:
 *  full name first, then username, so an account can never render an empty avatar. */
function initialsOf(u: User): string {
  const src = (u.fullName || u.username || '').trim()
  const parts = src.split(/\s+/).filter(Boolean)
  if (!parts.length) return '?'
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase()
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
}

function fmtDate(v: string): string {
  if (!v) return ''
  const d = new Date(v)
  if (Number.isNaN(d.getTime())) return String(v)
  return d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
}

onMounted(() => {
  if (isAdmin.value) load()
})
</script>
<template>
  <UDashboardPanel>
    <template #header>
      <!-- PageHeader carries the sidebar collapse control in the navbar's #leading slot,
           exactly as the Nuxt dashboard template does on every page. -->
      <PageHeader title="User Management" />
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

        <template v-else>
          <!-- Sheet header, built to match the CCTV register's block exactly: the same
               `rounded-lg border border-default bg-elevated p-4` card, the same
               `justify-between` split with the title on the left and the actions on the right,
               and the same `text-lg font-bold tracking-wide` h1.

               Deliberately NOT a PageHeader subtitle. The navbar is locked to a fixed
               h-(--ui-header-height) = 4rem = 64px, which is why PageHeader is title-only and
               carries no subtitle prop at all - a stacked title plus subtitle measured a 0px
               gap between the lines there and they touched. The sheet header lives in the
               panel body instead, below the navbar, which is where the CCTV page puts its own.
               That is also why the page still reads as two distinct things: "User Management"
               in the navbar names the SCREEN, "Registered User" below names the REGISTER. -->
          <div class="rounded-lg border border-default bg-elevated p-4">
            <div class="flex flex-wrap items-center justify-between gap-3">
              <div>
                <h1 class="text-lg font-bold tracking-wide">Registered User</h1>
              </div>
              <div class="flex items-center gap-2">
                <UInput
                  v-model="search"
                  icon="i-lucide-search"
                  placeholder="Search..."
                  class="w-48"
                />
                <span class="whitespace-nowrap text-sm tabular-nums text-muted">
                  {{ visibleUsers.length }} of {{ users.length }}
                </span>
                <UButton icon="i-lucide-plus" label="Add User" @click="openCreate" />
              </div>
            </div>
          </div>

          <!-- The table is capped in height and scrolls on its own, with a sticky header, so a
               long list never pushes the page into a second scrollbar.

               `overflow-y-auto`, NOT `overflow-y-scroll`. The scroll variant always paints the
               track, which is the right trade for a list that is almost always full - with three
               users it drew a permanently visible, permanently useless scrollbar. `auto` shows
               the bar only when there is something to scroll to. The stable gutter stays so the
               column widths do not jump the moment the bar appears. -->
          <div class="users-scroll max-h-[70vh] overflow-y-auto scrollbar-gutter-stable rounded-xl border border-default bg-elevated/40">
            <table class="w-full table-fixed text-sm">
              <colgroup>
                <col class="w-[26%]" />
                <col class="w-[22%]" />
                <col class="w-[16%]" />
                <col class="w-[11%]" />
                <col class="w-[15%]" />
                <col class="w-[10%]" />
              </colgroup>
              <thead class="users-sticky-head sticky top-0 z-10 bg-elevated">
                <tr class="border-b border-default text-left text-xs font-semibold uppercase tracking-wide text-muted">
                  <th class="px-4 py-3">User</th>
                  <th class="px-4 py-3">Email</th>
                  <th class="px-4 py-3">Role</th>
                  <th class="px-4 py-3">Status</th>
                  <th class="px-4 py-3">Created</th>
                  <th class="px-4 py-3 text-right">Actions</th>
                </tr>
              </thead>

              <tbody v-if="loading">
                <tr v-for="i in 4" :key="`sk-${i}`">
                  <td v-for="c in 6" :key="c" class="px-4 py-3">
                    <USkeleton class="h-4 w-full" />
                  </td>
                </tr>
              </tbody>

              <tbody v-else-if="!visibleUsers.length">
                <tr>
                  <td colspan="6" class="px-4 py-14 text-center">
                    <UIcon name="i-lucide-user-x" class="mx-auto mb-3 size-8 text-dimmed" />
                    <p class="text-sm font-medium text-default">
                      {{ search.trim() ? 'No user matches your search' : 'No users yet' }}
                    </p>
                    <p class="mt-1 text-sm text-muted">
                      {{ search.trim() ? 'Try a different name, email or role.' : 'Add the first account to get started.' }}
                    </p>
                  </td>
                </tr>
              </tbody>

              <tbody v-else>
                <tr
                  v-for="u in visibleUsers"
                  :key="u.id"
                  class="users-row border-b border-default/60 last:border-0"
                >
                  <!-- Identity as ONE block: avatar, full name, @username beneath. These were
                       two separate columns before, which split the single thing a person
                       actually recognises about a row across the table. -->
                  <td class="px-4 py-3">
                    <div class="flex min-w-0 items-center gap-3">
                      <!-- color/variant are the component's own props, so the avatar follows the
                           chosen accent automatically. An earlier attempt styled this with a
                           scoped `:global(.dark)` rule and it broke the whole page: Vue's scoped
                           transform does not survive a leading `:global()`, so the selector
                           collapsed to a bare `.dark { background: ... }` and tinted the ENTIRE
                           document, sidebar included. The component's own props do the same job
                           with no custom rule and no such risk. -->
                      <UAvatar
                        :alt="initialsOf(u)"
                        :text="initialsOf(u)"
                        size="sm"
                        color="primary"
                        variant="soft"
                        class="shrink-0"
                      />
                      <div class="min-w-0">
                        <div class="flex items-center gap-1.5">
                          <span class="truncate font-medium text-default">{{ u.fullName || u.username }}</span>
                          <UBadge v-if="u.id === me?.id" color="primary" variant="soft" size="sm">You</UBadge>
                        </div>
                        <span class="block truncate text-xs text-muted">@{{ u.username }}</span>
                      </div>
                    </div>
                  </td>
                  <td class="px-4 py-3">
                    <span class="block truncate text-muted">{{ u.email || '—' }}</span>
                  </td>
                  <td class="px-4 py-3">
                    <div class="flex flex-wrap gap-1">
                      <UBadge v-for="r in u.roles" :key="r.id" color="neutral" variant="soft" size="sm">
                        {{ r.name }}
                      </UBadge>
                      <span v-if="!u.roles.length" class="text-muted">—</span>
                    </div>
                  </td>
                  <td class="px-4 py-3">
                    <UBadge :color="u.isActive ? 'success' : 'error'" variant="soft" size="sm">
                      {{ u.isActive ? 'Active' : 'Inactive' }}
                    </UBadge>
                  </td>
                  <!-- createdBy used to sit inline beside the date with no separation, so the
                       two read as one run-on string. The author is now on its own dimmed line. -->
                  <td class="px-4 py-3">
                    <span class="block tabular-nums text-default">{{ fmtDate(u.createdAt) }}</span>
                    <span v-if="u.createdBy" class="block truncate text-xs text-dimmed">by {{ u.createdBy }}</span>
                  </td>
                  <td class="px-4 py-3">
                    <div class="flex items-center justify-end gap-1">
                      <UButton
                        icon="i-lucide-pencil"
                        size="xs"
                        color="neutral"
                        variant="ghost"
                        :aria-label="`Edit ${u.username}`"
                        @click="openEdit(u)"
                      />
                      <UButton
                        v-if="u.username !== 'admin'"
                        icon="i-lucide-trash-2"
                        size="xs"
                        color="error"
                        variant="ghost"
                        :aria-label="`Delete ${u.username}`"
                        @click="askDelete(u)"
                      />
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </template>
      </div>

      <!-- ADD / EDIT USER
           A UModal now, matching the CCTV record dialog. The Save button stays INSIDE the
           UForm rather than in #footer: UModal's #footer slot swallows @click on a submit
           button (verified - onclick was null on the rendered node), so clicks silently did
           nothing. That is exactly why this used to be an inline card - which is why the form
           appeared below the table instead of over it. -->
      <UModal
        v-model:open="modalOpen"
        :ui="{ content: 'sm:max-w-lg', body: 'p-0', footer: 'p-0' }"
      >
        <template #content>
          <div class="overflow-hidden rounded-xl">
            <div class="relative overflow-hidden px-6 pb-5 pt-6">
              <div
                aria-hidden="true"
                class="pointer-events-none absolute -right-16 -top-24 size-48 rounded-full bg-primary/15 blur-3xl"
              />
              <div class="relative flex items-start gap-4">
                <span class="grid size-11 shrink-0 place-items-center rounded-full bg-primary/10 ring-1 ring-inset ring-primary/25">
                  <UIcon :name="editing ? 'i-lucide-user-pen' : 'i-lucide-user-plus'" class="size-5 text-primary" />
                </span>
                <div class="min-w-0">
                  <h2 class="text-base font-semibold text-default">
                    {{ editing ? 'Edit user' : 'Add user' }}
                  </h2>
                  <p class="mt-1 text-sm text-muted">
                    {{ editing
                      ? 'Update this account. The username cannot be changed.'
                      : 'Create a new account and assign at least one role.' }}
                  </p>
                </div>
              </div>
            </div>

            <UForm :state="form" :validate-on="[]" @submit="save">
              <div class="space-y-4 px-6 pb-5">
                <UFormField
                  label="Username"
                  required
                  :error="formErrors.username"
                  :help="editing ? 'The username cannot be changed.' : undefined"
                >
                  <UInput v-model="form.username" :disabled="!!editing" class="w-full" placeholder="jsmith" />
                </UFormField>

                <UFormField
                  :label="editing ? 'New password' : 'Password'"
                  :required="!editing"
                  :error="formErrors.password"
                  :help="editing ? 'Leave blank to keep the current password.' : 'At least 6 characters.'"
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
              </div>

              <!-- Inside the form on purpose - see the note above. -->
              <div class="flex items-center justify-end gap-2 border-t border-default/70 bg-elevated/40 px-6 py-4">
                <UButton label="Cancel" color="neutral" variant="ghost" type="button" @click="modalOpen = false" />
                <UButton type="submit" :label="editing ? 'Save changes' : 'Create user'" :loading="saving" />
              </div>
            </UForm>
          </div>
        </template>
      </UModal>

      <!-- DELETE USER -->
      <UModal
        v-model:open="showDelete"
        :ui="{ content: 'sm:max-w-md', body: 'p-0', footer: 'p-0 border-t border-default/70' }"
      >
        <template #content>
          <div class="overflow-hidden rounded-xl">
            <div class="relative overflow-hidden px-6 pb-5 pt-6">
              <div
                aria-hidden="true"
                class="pointer-events-none absolute -right-16 -top-24 size-48 rounded-full bg-error/20 blur-3xl"
              />
              <div class="relative flex items-start gap-4">
                <span class="grid size-11 shrink-0 place-items-center rounded-full bg-error/10 ring-1 ring-inset ring-error/25 dark:bg-error/15">
                  <UIcon name="i-lucide-user-x" class="size-5 text-error" />
                </span>
                <div class="min-w-0 flex-1">
                  <h2 class="text-base font-semibold text-default">Delete this user?</h2>
                  <p class="mt-1 text-sm text-muted">
                    The account will be removed and can no longer sign in. This cannot be undone.
                  </p>
                </div>
              </div>

              <dl
                v-if="deleteTarget"
                class="relative mt-5 grid grid-cols-[7.5rem_1fr] items-baseline gap-x-5 gap-y-3 rounded-xl bg-elevated/60 px-5 py-4 ring-1 ring-inset ring-default"
              >
                <dt class="text-xs font-medium uppercase tracking-wide text-dimmed">Username</dt>
                <dd :title="deleteTarget.username" class="line-clamp-2 break-words text-sm font-medium text-default">
                  {{ deleteTarget.username }}
                </dd>

                <dt class="text-xs font-medium uppercase tracking-wide text-dimmed">Full Name</dt>
                <dd :title="deleteTarget.fullName" class="line-clamp-2 break-words text-sm font-medium text-default">
                  {{ deleteTarget.fullName || '—' }}
                </dd>

                <dt class="text-xs font-medium uppercase tracking-wide text-dimmed">Role</dt>
                <dd class="flex flex-wrap gap-1">
                  <UBadge v-for="r in deleteTarget.roles" :key="r.id" color="neutral" variant="soft" size="sm">
                    {{ r.name }}
                  </UBadge>
                  <span v-if="!deleteTarget.roles.length" class="text-sm text-default">—</span>
                </dd>
              </dl>
            </div>

            <div class="flex items-center justify-end gap-2 bg-elevated/40 px-6 py-4">
              <UButton label="Cancel" color="neutral" variant="ghost" :disabled="deleting" @click="showDelete = false" />
              <UButton label="Delete user" icon="i-lucide-trash-2" color="error" :loading="deleting" @click="confirmDelete" />
            </div>
          </div>
        </template>
      </UModal>
    </template>
  </UDashboardPanel>
</template>

<style scoped>
/* Sticky header: the table scrolls inside its own capped box, so the header has to be opaque
   or rows show through it. `bg-elevated` is a real token rather than a fixed colour, so it
   stays correct in both themes and against whatever accent is chosen. */
.users-sticky-head {
  backdrop-filter: saturate(140%) blur(6px);
}

.users-row {
  transition: background-color 140ms ease;
}

.users-row:hover {
  background-color: var(--ui-bg-accented);
}

@media (prefers-reduced-motion: reduce) {
  .users-row {
    transition: none !important;
  }
}
</style>
