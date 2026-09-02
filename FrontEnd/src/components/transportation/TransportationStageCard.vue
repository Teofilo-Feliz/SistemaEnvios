<script setup>
defineProps({ title: String, count: { type: Number, default: 0 }, icon: Object, active: Boolean })
defineEmits(['view'])
</script>
<template><button class="transport-stage-card" :class="{ active }" @click="$emit('view')"><component :is="icon" :size="25"/><span>{{ title }}</span><strong>{{ count }}</strong><small>Ver envíos</small></button></template>
