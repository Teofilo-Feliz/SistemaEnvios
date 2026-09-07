<script setup>
import { computed, watch } from "vue";
import { X } from "lucide-vue-next";
import { operatorsByType } from "@/config/filterFields";
const props = defineProps({
  rule: { type: Object, required: true },
  fields: { type: Array, required: true },
  sources: { type: Object, default: () => ({}) },
  nested: Boolean,
});
const emit = defineEmits(["update", "remove"]);
const field = computed(
  () => props.fields.find((item) => item.key === props.rule.field) || props.fields[0],
);
const operators = computed(
  () => operatorsByType[field.value?.type || "text"] || operatorsByType.text,
);
const options = computed(() => props.sources[field.value?.source] || []);
watch(
  () => props.rule.field,
  () =>
    emit("update", { ...props.rule, operator: operators.value[0]?.value || "equals", value: "" }),
);
function patch(values) {
  emit("update", { ...props.rule, ...values });
}
</script>
<template>
  <div class="advanced-filter-row">
    <select
      v-if="!nested"
      class="filter-control filter-logic"
      :value="rule.logic"
      @change="patch({ logic: $event.target.value })"
    >
      <option value="AND">Y</option>
      <option value="OR">O</option></select
    ><span v-else class="filter-indent-logic">{{ rule.logic === "OR" ? "O" : "Y" }}</span>
    <select
      class="filter-control filter-field"
      :value="rule.field"
      @change="patch({ field: $event.target.value })"
    >
      <option v-for="item in fields" :key="item.key" :value="item.key">{{ item.label }}</option>
    </select>
    <select
      class="filter-control filter-operator"
      :value="rule.operator"
      @change="patch({ operator: $event.target.value })"
    >
      <option v-for="operator in operators" :key="operator.value" :value="operator.value">
        {{ operator.label }}
      </option>
    </select>
    <select
      v-if="field.type === 'select'"
      class="filter-control filter-value"
      :value="rule.value"
      @change="patch({ value: $event.target.value })"
    >
      <option value="">Seleccionar</option>
      <option v-for="option in options" :key="option.value" :value="option.value">
        {{ option.label }}
      </option>
    </select>
    <input
      v-else-if="field.type === 'number'"
      class="filter-control filter-value"
      type="number"
      :value="rule.value"
      @input="patch({ value: $event.target.value })"
    />
    <input
      v-else
      class="filter-control filter-value"
      :type="field.type === 'date' ? 'date' : 'text'"
      :disabled="['empty', 'notEmpty'].includes(rule.operator)"
      :value="rule.value"
      @input="patch({ value: $event.target.value })"
    />
    <button class="filter-remove" title="Eliminar regla" type="button" @click="emit('remove')">
      <X :size="15" />
    </button>
  </div>
</template>
