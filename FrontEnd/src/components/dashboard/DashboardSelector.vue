<script setup>
defineProps({ modelValue: { type: String, default: 'general' } }); const emit = defineEmits(['update:modelValue'])
</script>
<template><select class="dashboard-selector" :value="modelValue" @change="emit('update:modelValue', $event.target.value)"><option value="general">Dashboard general</option><option value="envios">Envíos</option><option value="equipos">Equipos</option><option value="transportes">Transportación</option><option value="incidencias">Incidencias</option><option value="tecnologia">Tecnología</option></select></template>
