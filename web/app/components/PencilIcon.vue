<script setup lang="ts">
// Geometry: Iconify lucide:pencil, split into the body and the writing line so the parts
// can move independently. The body is the vendor path, unchanged:
//   body     M21.174 6.812a1 1 0 0 0-3.986-3.987L3.842 16.174a2 2 0 0 0-.5.83l-1.321 4.352
//            a.5.5 0 0 0 .623.622l4.353-1.32a2 2 0 0 0 .83-.497z
//   ferrule  M15 5l4 4   (the band near the eraser, drawn as its own path)
//
// On hover the pencil tips over into a writing angle - it pivots on its own tip, the way a
// hand pivots a pencil - and a short line is drawn under the tip where the writing would
// land. The line is the only stroke that is not part of the vendor icon, and it is what
// makes the motion read as "writing" rather than "the icon tilted".
//
// The motion is driven entirely by CSS from the parent button's .pencil-hover class, so this
// component stays reusable.
</script>

<template>
  <svg class="pencil-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor"
       stroke-width="2" stroke-linecap="round" stroke-linejoin="round"
       aria-hidden="true" focusable="false">
    <g class="pencil-body">
      <path d="M21.174 6.812a1 1 0 0 0-3.986-3.987L3.842 16.174a2 2 0 0 0-.5.83l-1.321 4.352a.5.5 0 0 0 .623.622l4.353-1.32a2 2 0 0 0 .83-.497z" />
      <path d="M15 5l4 4" />
    </g>
    <!-- The line the pencil leaves behind. It sits below the tip, so it never collides with
         the body at rest or while the pencil is tilted. -->
    <path class="pencil-stroke" d="M2.3 22H10.2" />
  </svg>
</template>

<style scoped>
.pencil-icon {
  display: block;
  overflow: visible;
}
/* The pencil pivots on its tip: that is the point the writing happens, and pivoting there
   keeps the tip (and the line under it) planted while the body swings up into writing
   position. 2.3px 21.4px is the tip corner of the vendor path. */
.pencil-body {
  transform-box: view-box;
  transform-origin: 2.3px 21.4px;
}
.pencil-stroke {
  stroke-dasharray: 8;
  stroke-dashoffset: 8;
}
</style>
