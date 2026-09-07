<script setup>
defineProps({
  title: String,
  value: [String, Number],
  icon: [Object, Function],
  tone: { type: String, default: "blue" },
  large: Boolean,
});
const emit = defineEmits(["click"]);
</script>
<template>
  <button
    class="dashboard-kpi"
    :class="[`dashboard-kpi-${tone}`, { large }]"
    type="button"
    @click="emit('click')"
  >
    <span
      ><strong>{{ value ?? "—" }}</strong
      ><small>{{ title }}</small></span
    ><component :is="icon" :size="large ? 27 : 19" />
  </button>
</template>
