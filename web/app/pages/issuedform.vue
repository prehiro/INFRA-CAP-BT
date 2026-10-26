<script setup lang="ts">
// Preserve existing bookmarks while the sidebar uses the new Form route.
await navigateTo('/form/issuedform', { replace: true, redirectCode: 301 })
</script>
