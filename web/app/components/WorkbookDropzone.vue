<script setup lang="ts">
const props = withDefaults(defineProps<{ busy?: boolean; fileName?: string; label: string }>(), { busy: false, fileName: '' })
const emit = defineEmits<{ select: [file: File] }>()
const input = ref<HTMLInputElement | null>(null)
const depth = ref(0)
const error = ref('')
const active = computed(() => depth.value > 0 && !props.busy)
function accept(files: File[]) {
  depth.value = 0
  if (props.busy || !files.length) return
  if (files.length !== 1) { error.value = 'Please choose one workbook at a time.'; return }
  const file = files[0]!
  if (!/\.(xlsx|xlsm)$/i.test(file.name)) { error.value = 'Unsupported file. Choose an .xlsx or .xlsm workbook.'; return }
  if (!file.size) { error.value = 'This file is empty. Choose a valid workbook.'; return }
  error.value = ''
  emit('select', file)
}
function picked(event: Event) {
  const target = event.target as HTMLInputElement
  accept(Array.from(target.files ?? []))
  target.value = ''
}
function enter(event: DragEvent) {
  if (event.dataTransfer?.types.includes('Files')) { event.preventDefault(); depth.value++ }
}
function over(event: DragEvent) {
  event.preventDefault()
  if (event.dataTransfer) event.dataTransfer.dropEffect = props.busy ? 'none' : 'copy'
}
function drop(event: DragEvent) {
  event.preventDefault()
  const transfer = event.dataTransfer
  // Ignore folders rather than treating a directory as a corrupt workbook.
  if (Array.from(transfer?.items ?? []).some(item => item.webkitGetAsEntry?.()?.isDirectory)) {
    depth.value = 0
    if (!props.busy) error.value = 'Folders are not supported. Drop a single workbook.'
    return
  }
  accept(Array.from(transfer?.files ?? []))
}
watch(() => props.busy, () => { depth.value = 0 })
</script>

<template>
  <div class="workbook-upload">
    <div class="workbook-dropzone relative flex min-h-40 flex-col items-center justify-center gap-3 rounded-xl border-2 border-dashed p-5 text-center sm:flex-row sm:gap-5 sm:text-left"
      :class="[active ? 'bg-primary/10' : 'bg-default/40', busy ? 'opacity-60' : '']"
      :data-drag-active="active" :aria-busy="busy" @dragenter="enter" @dragover="over" @dragleave.prevent="depth = Math.max(0, depth - 1)" @drop="drop">
      <span class="grid size-12 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary ring-1 ring-inset ring-primary/20" aria-hidden="true">
        <svg class="workbook-upload-icon size-6" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path class="workbook-upload-tray" d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
          <g class="workbook-upload-arrow"><path d="m17 8-5-5-5 5M12 3v12" /></g>
        </svg>
      </span>
      <div class="min-w-0 flex-1">
        <p class="text-sm font-semibold">{{ busy ? 'Processing workbook...' : active ? 'Release to preview your workbook' : `Drop your ${label} workbook here` }}</p>
        <p class="mt-1 text-xs text-muted">One .xlsx or .xlsm file. Preview first, then confirm import.</p>
        <p class="mt-2 truncate text-xs font-medium text-primary" :title="fileName || undefined">{{ fileName || 'No file selected' }}</p>
      </div>
      <input ref="input" type="file" accept=".xlsx,.xlsm" class="hidden" :disabled="busy" :aria-label="`Choose ${label} workbook`" @change="picked">
      <UButton icon="i-lucide-upload" :label="fileName ? 'Choose another file' : 'Choose Excel file'" class="record-save-hover shrink-0" :disabled="busy" @click="input?.click()" />
    </div>
    <p class="min-h-5 pt-1 text-xs text-error" role="status" aria-live="polite">{{ error }}</p>
  </div>
</template>

<style scoped>
.workbook-dropzone {
  border-color: color-mix(in oklab, var(--ui-primary) 50%, var(--ui-border-accented));
  transition: background-color 160ms ease, border-color 160ms ease;
}
.workbook-dropzone[data-drag-active="true"] { border-color: var(--ui-primary); }
.workbook-upload-arrow { animation: workbook-upload-lift 2.8s ease-in-out infinite; }
.workbook-upload-tray { animation: workbook-tray-pulse 2.8s ease-in-out infinite; }
@keyframes workbook-upload-lift {
  0%, 12%, 58%, 100% { transform: translateY(0); }
  30%, 36% { transform: translateY(-3px); }
}
@keyframes workbook-tray-pulse {
  0%, 12%, 58%, 100% { opacity: .65; }
  30%, 36% { opacity: 1; }
}
.workbook-dropzone[aria-busy="true"] .workbook-upload-arrow,
.workbook-dropzone[aria-busy="true"] .workbook-upload-tray { animation: none; }
@media (prefers-reduced-motion: reduce), print {
  .workbook-dropzone { transition: none; }
  .workbook-upload-arrow, .workbook-upload-tray { animation: none; }
}
</style>
