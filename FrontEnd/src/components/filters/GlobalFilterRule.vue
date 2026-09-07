<script setup>
import { X } from "lucide-vue-next";
defineProps({ rule: { type: Object, required: true } });
const emit = defineEmits(["update", "remove"]);
</script>
<template>
  <div class="advanced-filter-row global-filter-row">
    <span class="filter-global-label">Global</span
    ><select
      class="filter-control filter-operator"
      :value="rule.operator"
      @change="emit('update', { ...rule, operator: $event.target.value })"
    >
      <option value="contains">contiene</option>
      <option value="equals">es</option></select
    ><input
      class="filter-control filter-value"
      placeholder="Buscar en todos los campos"
      :value="rule.value"
      @input="emit('update', { ...rule, value: $event.target.value })"
    /><button
      class="filter-remove"
      title="Eliminar regla global"
      type="button"
      @click="emit('remove')"
    >
      <X :size="15" />
    </button>
  </div>
</template>
