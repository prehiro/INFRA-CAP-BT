<template>
  <UDashboardPanel id="noa-form" :ui="{ body: 'p-4 sm:p-6' }">
    <template #header><PageHeader title="NOA" /></template>
    <template #body>
      <div class="mx-auto w-full max-w-6xl">
        <UCard>
          <div class="flex items-start gap-3">
            <UIcon name="i-lucide-file-text" class="mt-0.5 size-5 shrink-0 text-primary" aria-hidden="true" />
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
