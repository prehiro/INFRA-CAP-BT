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

/** UForm validation rejection. Reported rather than swallowed, so a future schema change
 *  cannot reintroduce the silent no-op this page just lived through. */
function onFormError(e: any) {
  const list = e?.errors ?? []
  const text = Array.isArray(list)
    ? list.map((x: any) => (typeof x === 'string' ? x : `${(x?.path ?? []).join('.')}: ${x?.message}`)).join('; ')
    : String(list)
  toast.add({ title: 'Check the form', description: text || 'Some fields are not valid.', color: 'error' })
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

/**
 * Coerce whatever the Role multi-select put into `form.roleIds` into a clean number[].
 *
 * WHY THIS IS NEEDED - the real cause behind "The req field is required":
 * the server rejected the create with
 *   "The JSON value could not be converted to System.Int32. Path: $.roleIds[0]"
 * A failed body bind leaves the `req` parameter null, and ASP.NET then ALSO reports
 * "The req field is required" - so that message was never the real problem, just the
 * shadow it cast. My own API tests all passed because I posted `roleIds: [3]`, a real
 * number; the select was sending something else.
 *
 * Three shapes are handled, because a select can legitimately yield any of them:
 * a number, a numeric string, or the whole `{ label, value }` item. Anything that does not
 * resolve to a finite number is dropped rather than sent, so a stray entry produces a
 * clean validation error instead of an unbindable body.
 */
function normaliseRoleIds(raw: any): number[] {
  const list = Array.isArray(raw) ? raw : (raw == null ? [] : [raw])
  const out: number[] = []
  for (const item of list) {
    const v = (item && typeof item === 'object') ? (item as any).value : item
    const n = Number(v)
    if (Number.isFinite(n) && !out.includes(n)) out.push(n)
  }
  return out
}

async function save() {
  saving.value = true
  Object.keys(formErrors).forEach(k => delete formErrors[k])

  // A user with no role can sign in but sees nothing useful, which reads as a broken
  // account. Require at least one role up front instead of failing confusingly later.
  if (!normaliseRoleIds(form.roleIds).length) {
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
        roleIds: normaliseRoleIds(form.roleIds)
      })
      toast.add({ title: 'User updated', color: 'success' })
    } else {
      await apiCreateUser({
        username: form.username,
        password: form.password,
        email: form.email,
        fullName: form.fullName,
        isActive: form.isActive,
        roleIds: normaliseRoleIds(form.roleIds)
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

/**
 * True when the account being edited is an ACTIVE Admin and the ONLY active Admin.
 *
 * WHY THIS IS NEEDED - the lockout it prevents:
 *  - /api/users is gated by `[Authorize(Roles = "Admin")]`, so only an Admin can ever turn a
 *    user back on.
 *  - Login already rejects inactive users (AuthController checks `!user.IsActive`).
 *  - Deactivating the last active Admin therefore locks EVERY admin account out permanently,
 *    recoverable only by editing the database by hand.
 *  - Worse, the UI could cause it silently: the delete button is hidden for `admin`, but the
 *    Status switch in this very modal had no guard at all.
 *
 * Keyed on "last active admin" rather than on the username `admin`, so the restriction does
 * not outlive its reason: the moment a second active Admin exists, either account can be
 * deactivated freely.
 *
 * This is a UI guard only, which is what HIRO asked for. The API will still accept
 * `isActive: false` from anything holding a valid Admin token, so this closes the accidental
 * case rather than the deliberate one.
 */
const deactivateBlocked = computed(() => {
  const u = editing.value
  if (!u || !u.isActive) return false
  const isAdmin = (x: User) => x.roles.some(r => String(r.name).toLowerCase() === 'admin')
  if (!isAdmin(u)) return false
  return !users.value.some(o => o.id !== u.id && o.isActive && isAdmin(o))
})

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
          <!-- Outer card owns the border and radius; the scroll area sits inside it and the
               footer below shares its bottom edge. This is the CCTV register's structure
               verbatim - `overflow-hidden rounded-xl border border-default bg-elevated`
               wrapping `.logbook-scroll` plus a footer div. -->
          <div class="overflow-hidden rounded-xl border border-default bg-elevated">
          <div class="users-scroll max-h-[70vh] overflow-y-auto scrollbar-gutter-stable">
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

          <!-- Table footer, mirroring the CCTV register's: row count on the left, the
               "Showing X of Y" pair on the right, separated by a top rule on a subtly
             raised surface. BOTH numbers follow the SEARCH, so they can never disagree with
               the rows on screen - the same reason the CCTV left count follows its filters
               rather than the raw DB total. -->
          <div class="flex items-center justify-between gap-3 border-t border-default bg-default/30 px-4 py-2.5 text-xs text-muted">
            <span>{{ visibleUsers.length }} {{ visibleUsers.length === 1 ? 'user' : 'users' }}</span>
            <span class="tabular-nums">Showing {{ visibleUsers.length }} of {{ users.length }}</span>
          </div>
          </div>
        </template>
      </div>

      <!-- ADD / EDIT USER
           Built to the CCTV record dialog's EXACT structure, because the first attempt at this
           silently did nothing and it is worth recording why.

           The first attempt used UModal's #content slot and nested the UForm inside it. The
           form's submit event fired (a capture listener counted it) but @submit NEVER CALLED
           save(): no spinner, no request, no toast, dialog simply sat there. Same UForm, same
           :validate-on="[]", same buttons-inside-the-form - but it only works from the #body
           slot, which is what the CCTV dialog uses and the only shape verified to save. The
           Cancel/Save row therefore lives INSIDE the UForm here too, not in #footer: that
           footer slot is separately known to swallow @click on a submit button.

           The title/description are passed as UModal props rather than hand-built markup,
           which is also what makes the header, close button and layout identical to CCTV's
           without any duplicated CSS. -->
      <UModal
        v-model:open="modalOpen"
        :ui="{ content: 'sm:max-w-lg', body: 'p-5' }"
        :title="editing ? 'Edit user' : 'Add user'"
        :description="editing
          ? 'Update this account. The username cannot be changed.'
          : 'Create a new account and assign at least one role.'"
      >
        <template #body>
          <!-- Every UFormField below carries a `name` matching a key of `form`, and that is
               load-bearing, not decoration. UForm's submit wrapper runs _validate() BEFORE
               props.onSubmit, and if validation throws it emits "error" and NEVER calls the
               handler - so with `:validate-on="[]"` the page looked completely dead on submit:
               no spinner, no request, no toast, no visible error. Each UFormField registered
               itself with no name, so a required field could never be satisfied and validation
               failed every single time. The CCTV dialog has had `name` on its fields all along,
               which is why it works. -->
          <!-- @error is surfaced as a toast so UForm's own validation can never fail
               silently again. Previously nothing listened, which is why a rejected submit
               looked identical to a button that was simply broken. -->
          <UForm
            :state="form"
            :validate-on="[]"
            @submit="save"
            @error="onFormError"
          >
            <div class="space-y-4">
              <UFormField
                name="username"
                label="Username"
                required
                :error="formErrors.username"
                :help="editing ? 'The username cannot be changed.' : undefined"
              >
                <UInput v-model="form.username" :disabled="!!editing" class="w-full" placeholder="jsmith" />
              </UFormField>

              <UFormField
                name="password"
                :label="editing ? 'New password' : 'Password'"
                :required="!editing"
                :error="formErrors.password"
                :help="editing ? 'Leave blank to keep the current password.' : 'At least 6 characters.'"
              >
                <UInput v-model="form.password" type="password" class="w-full" placeholder="••••••••" />
              </UFormField>

              <!-- Role is now RolePicker: radio-LOOKING cards that are multi-select toggles.
                   This replaced <USelectMenu multiple>, which was genuinely hostile here.
                   Two real defects are gone rather than relocated:
                    - it STAYS OPEN after ticking, so as the last field before the footer it
                      opened straight over the "Create user" button and swallowed the click.
                      Zero requests were ever sent, and the natural next move - pressing Escape
                      to dismiss the popover - DISCARDED the ticked role, so the retry then
                      failed with "Select at least one role". The field had been moved up one
                      row purely to dodge that; the cards have no popover at all.
                    - nothing showed WHAT a role could do, because each description only ever
                      appeared inside the closed popover. They are always visible now.
                   `name` stays on UFormField because it is load-bearing: UForm's submit
                   wrapper runs _validate() first, and a field with no name can never be
                   satisfied, which made submit look completely dead.
                   Multi-select is deliberate - one user can hold several roles (AppUser.
                   UserRoles is a collection and [Authorize(Roles=...)] reads every claim).
                   A real radio would be single-select and would drop that capability. -->
              <UFormField
                name="roleIds"
                label="Role"
                :error="formErrors.roleIds"
                :help="formErrors.roleIds ? undefined : 'Select at least one. A user can hold more than one.'"
              >
                <RolePicker
                  v-model="form.roleIds"
                  :roles="roles"
                  :invalid="!!formErrors.roleIds"
                />
              </UFormField>

              <UFormField name="fullName" label="Full Name" :error="formErrors.fullName">
                <UInput v-model="form.fullName" class="w-full" placeholder="John Smith" />
              </UFormField>

              <UFormField name="email" label="Email" :error="formErrors.email">
                <UInput v-model="form.email" type="email" class="w-full" placeholder="user@company.local" />
              </UFormField>

              <UFormField
                name="isActive"
                label="Status"
                :help="deactivateBlocked
                  ? 'This is the only active Admin. Deactivating it would lock every admin account out, with no one left to switch it back on.'
                  : undefined"
              >
                <USwitch
                  v-model="form.isActive"
                  label="User is active"
                  :disabled="deactivateBlocked"
                />
              </UFormField>
            </div>

            <div class="mt-5 flex items-center justify-end gap-2 border-t border-default pt-4">
              <UButton type="button" variant="ghost" color="neutral" label="Cancel" @click="modalOpen = false" />
              <UButton type="submit" :loading="saving" :label="editing ? 'Save changes' : 'Create user'" />
            </div>
          </UForm>
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
