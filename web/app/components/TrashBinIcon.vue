<script setup lang="ts">
// Geometry: Iconify lucide:trash-2 (an alias of lucide:trash), split into its parts so the
// lid can move on its own. The three parts together are exactly the vendor path:
//   body    M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6
//   content M10 11v6  and  M14 11v6
//   lid     M3 6h18  and the handle M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2
// Nothing is added or redrawn, so the resting icon is pixel-for-pixel the vendor shape.
//
// On hover the lid lifts and the contents drop into the bin. The motion is driven entirely
// by CSS from the parent button's .trash-bin-hover class, so this component stays reusable.
</script>

<template>
  <svg class="trash-bin" viewBox="0 0 24 24" fill="none" stroke="currentColor"
       stroke-width="2" stroke-linecap="round" stroke-linejoin="round"
       aria-hidden="true" focusable="false">
    <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6" />
    <g class="trash-bin-content">
      <path d="M10 11v6" />
      <path d="M14 11v6" />
    </g>
    <g class="trash-bin-lid">
      <path d="M3 6h18" />
      <path d="M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
    </g>
  </svg>
</template>

<style scoped>
.trash-bin {
  display: block;
  overflow: visible;
}
/* The lid hinges on its back end, so it swings up and away from the viewer. */
.trash-bin-lid {
  transform-box: view-box;
  transform-origin: 3px 6px;
}
.trash-bin-content {
  transform-box: view-box;
  transform-origin: 50% 11px;
}
</style>
