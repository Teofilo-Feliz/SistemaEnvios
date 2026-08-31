<script setup>import { computed } from 'vue'; import { getStatus } from '@/utils/status'; const props = defineProps({ status: String }); const config = computed(() => getStatus(props.status))</script>
<template><span class="status-badge" :class="`tone-${config.tone}`"><i />{{ config.label }}</span></template>
