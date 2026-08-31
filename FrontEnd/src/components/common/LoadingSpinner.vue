<script setup>defineProps({ label: { type: String, default: 'Cargando información…' } })</script>
<template><div class="loading-state"><span class="spinner" /><span>{{ label }}</span></div></template>
