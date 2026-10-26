<template>
  <UDashboardPanel id="noa-form" :ui="{ body: 'p-4 sm:p-6' }">
    <template #header><PageHeader title="NOA" /></template>
    <template #body>
      <div class="mx-auto w-full max-w-6xl">
        <UCard>
          <div class="flex items-start gap-3">
            <span class="noa-title-icon grid size-12 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary ring-1 ring-inset ring-primary/20" aria-hidden="true">
              <RecordAddIcon name="i-lucide-file-text" random-halo />
            </span>
            <div>
              <h2 class="font-semibold text-highlighted">NOA</h2>
              <p class="mt-1 text-sm text-muted">The NOA form is not available yet.</p>
            </div>
          </div>
        </UCard>
      </div>
    </template>
  </UDashboardPanel>
</template>
