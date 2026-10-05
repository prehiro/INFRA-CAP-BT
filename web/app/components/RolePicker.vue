<script setup lang="ts">
/**
 * Role picker: radio-LOOKING cards that are actually multi-select toggles.
 *
 * WHY IT IS NOT A PLAIN RADIO GROUP:
 * AppUser.UserRoles is a collection, CreateUserRequest takes List<int> RoleIds, and
 * [Authorize(Roles = ...)] reads every claim - so one user CAN legitimately hold several
 * roles at once (verified live: a throwaway user was created holding Admin + Staff). A real
 * <input type=radio> is single-select by definition, so it would silently remove that
 * capability the first time somebody needed it. HIRO asked for "radio button" styling on
 * 2026-10-05 and chose to keep multi-select, so these cards use role="checkbox"
 * semantics with a radio-shaped indicator. Keyboard and screen-reader behaviour is
 * therefore correct for what the control actually does.
 *
 * WHY A COMPONENT AT ALL:
 * The old control was <USelectMenu multiple>, and that one was genuinely hostile:
 *  - it STAYS OPEN after ticking, so as the last field before the footer it opened
 *    straight over the "Create user" button and swallowed the click. Zero requests were
 *    ever sent, and pressing Escape to dismiss the popover discarded the ticked role, so
 *    the retry failed with "Select at least one role". Moving the field up one row was
 *    the workaround; these cards have no popover at all, so the whole failure mode is
 *    gone rather than relocated.
 *  - nothing showed WHAT each role could do, because the descriptions only ever appeared
 *    inside the closed popover. Here the description is always visible, which is the
 *    actual reason to pick a card over a select.
 */
import type { Role } from '~/types'

const props = withDefaults(defineProps<{
  modelValue?: number[]
  roles: Role[]
  disabled?: boolean
  /** Highlights every card and shows the error text, used for the "pick one" validation. */
  invalid?: boolean
}>(), {
  modelValue: () => [],
  disabled: false,
  invalid: false
})

const emit = defineEmits<{ 'update:modelValue': [number[]] }>()

/**
 * An icon per role, chosen from the name. This is presentation only - the control must
 * keep working for a role added later whose name matches none of these cases.
 *
 * The `i-lucide:NAME` COLON form is required. Iconify only builds a mask-image for a class
 * it can parse; written as `i-lucide-shield-check` the span renders 14x14 with
 * mask-image:none, i.e. completely invisible, while every icon elsewhere in this app
 * (sidebar, buttons, table rows) uses the colon form and does render.
 */
function iconFor(name: string): string {
  const n = name.toLowerCase()
  if (/(admin|super|root|owner)/.test(n)) return 'i-lucide:shield-check'
  if (/(manager|supervisor|lead)/.test(n)) return 'i-lucide:briefcase'
  if (/(staff|operator|employee|user)/.test(n)) return 'i-lucide:user-round'
  if (/(viewer|read|audit)/.test(n)) return 'i-lucide:eye'
  return 'i-lucide:key-round'
}

function isSelected(id: number): boolean {
  return props.modelValue.includes(id)
}

/**
 * Toggle rather than assign, so several roles can be held at once. Numbers only - the
 * USelectMenu that used to sit here once sent `{label, value}` objects and numeric
 * strings to the API, which failed the body bind with
 * "The JSON value could not be converted to System.Int32. Path: $.roleIds[0]" and, as a
 * shadow, "The req field is required". Keeping the model strictly number[] makes that
 * class of bug impossible to reintroduce from here.
 */
function toggle(id: number) {
  if (props.disabled) return
  const next = isSelected(id)
    ? props.modelValue.filter(x => x !== id)
    : [...props.modelValue, id]
  emit('update:modelValue', next)
}
</script>

<template>
  <div class="grid gap-1.5" role="group" aria-label="Roles">
    <button
      v-for="r in roles"
      :key="r.id"
      type="button"
      role="checkbox"
      :aria-checked="isSelected(r.id)"
      :disabled="disabled"
      class="group flex w-full items-center gap-2.5 rounded-lg border px-2.5 py-1.5 text-left transition-all duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2 focus-visible:ring-offset-default disabled:cursor-not-allowed disabled:opacity-60"
      :class="isSelected(r.id)
        ? 'border-primary bg-primary/8 shadow-sm shadow-primary/10'
        : (invalid
            ? 'border-error/60 hover:border-error hover:bg-error/5'
            : 'border-default hover:border-primary/50 hover:bg-elevated/50')"
      @click="toggle(r.id)"
    >
      <!-- The radio-shaped indicator. A ring with a filled centre when selected, so it
           reads as a radio group at a glance while behaving as the checkbox it is. -->
      <span
        class="flex size-4 shrink-0 items-center justify-center rounded-full border-2 transition-colors duration-150"
        :class="isSelected(r.id) ? 'border-primary bg-primary' : 'border-default group-hover:border-primary/60'"
        aria-hidden="true"
      >
        <span
          class="size-1 rounded-full bg-white transition-transform duration-150"
          :class="isSelected(r.id) ? 'scale-100' : 'scale-0'"
        />
      </span>

      <!-- UIcon, NOT a raw <span class="iconify i-lucide:...">. The raw span renders at the
           correct size but stays COMPLETELY INVISIBLE (mask-image: none): Nuxt UI resolves an
           icon's SVG and injects the matching mask-image CSS from
           .nuxt/nuxt-icon-client-bundle at RENDER TIME, and only the Icon component triggers
           that. The class name on its own does nothing, and the icon IS in the bundle - which
           is why this looks like a bundling failure when it is not. -->
      <UIcon
        :name="iconFor(r.name)"
        class="size-3.5 shrink-0 transition-colors duration-150"
        :class="isSelected(r.id) ? 'text-primary' : 'text-dimmed group-hover:text-muted'"
      />

      <span class="min-w-0 flex-1 truncate text-sm font-medium text-default">{{ r.name }}</span>

      <!-- One line, truncated, with the full text on hover. The descriptions are long
           sentences, and letting them wrap is what pushed this dialog past the viewport on a
           1366x768 laptop (whose usable viewport is only ~640px). The full text is still
           reachable via the native tooltip, and the delete dialog shows roles in full. -->
      <span
        v-if="r.description"
        class="hidden min-w-0 flex-1 truncate text-xs text-dimmed sm:block"
        :title="r.description"
      >
        {{ r.description }}
      </span>
    </button>
  </div>
</template>