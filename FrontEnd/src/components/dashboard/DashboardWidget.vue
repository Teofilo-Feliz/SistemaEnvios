<script setup>
import LoadingSpinner from '@/components/common/LoadingSpinner.vue'
defineProps({ title: String, loading: Boolean, error: String })
</script>
<template><section class="dashboard-widget"><header><h2>{{ title }}</h2><button v-if="error" class="widget-retry" type="button" @click="$emit('retry')">Reintentar</button></header><div class="dashboard-widget-body"><LoadingSpinner v-if="loading" /><div v-else-if="error" class="widget-error">{{ error }}</div><slot v-else /></div></section></template>
