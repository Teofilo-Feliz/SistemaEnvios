<script setup>
import { onMounted } from 'vue'
import { useAuthStore } from '@/stores/authStore'
const authStore = useAuthStore()
onMounted(async () => { await authStore.handleSilentRenewCallback() })
</script>
<template><div></div></template>
