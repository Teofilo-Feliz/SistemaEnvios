<script setup>
import DashboardChart from './DashboardChart.vue'
defineProps({ type: String, data: Object })
</script>
<template><DashboardChart :type="type" :data="data" /></template>
