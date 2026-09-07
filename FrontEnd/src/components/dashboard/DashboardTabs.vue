<script setup>
defineProps({ modelValue: { type: String, default: "dashboard" } });
const emit = defineEmits(["update:modelValue"]);
const tabs = [
  { key: "dashboard", label: "Tablero" },
  { key: "personal", label: "Vista personal" },
  { key: "group", label: "Vista de grupo" },
  { key: "global", label: "Vista global" },
];
</script>
<template>
  <nav class="dashboard-tabs">
    <button
      v-for="tab in tabs"
      :key="tab.key"
      :class="{ active: modelValue === tab.key }"
      type="button"
      @click="emit('update:modelValue', tab.key)"
    >
      {{ tab.label }}
    </button>
  </nav>
</template>
