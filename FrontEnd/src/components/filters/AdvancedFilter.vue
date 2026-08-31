<script setup>
import { ref, watch } from 'vue'
import { RotateCcw, Search, Bookmark } from 'lucide-vue-next'
import FilterGroup from './FilterGroup.vue'
import { createRule } from '@/config/filterFields'
const props = defineProps({ fields: { type: Array, required: true }, sources: { type: Object, default: () => ({}) }, modelValue: { type: Object, default: null }, loading: Boolean })
const emit = defineEmits(['search', 'clear', 'change', 'update:modelValue'])
const tree = ref(props.modelValue || { logic: 'AND', rules: [createRule()] })
watch(() => props.modelValue, (value) => { if (value) tree.value = value }, { deep: true })
function update(group) { tree.value = group; emit('update:modelValue', group); emit('change', group) }
function clear() { tree.value = { logic: 'AND', rules: [createRule()] }; emit('update:modelValue', tree.value); emit('clear') }
function search() { emit('search', normalize(tree.value)) }
function normalize(group) { return { logic: group.logic, rules: group.rules.map((rule) => rule.type === 'group' ? { type: 'group', logic: rule.logic, rules: normalize(rule).rules } : rule.type === 'global' ? { type: 'global', operator: rule.operator, value: rule.value } : { type: 'rule', field: rule.field, operator: rule.operator, value: rule.value }) } }
</script>
<template><div class="advanced-filter"><FilterGroup :group="tree" :fields="fields" :sources="sources" root @update="update" /><div class="advanced-filter-footer"><button class="filter-link" type="button" @click="emit('change', tree)"><Bookmark :size="13" /> Guardar búsqueda</button><button class="filter-link" type="button" @click="clear"><RotateCcw :size="13" /> Limpiar</button><button class="btn filter-search-button" type="button" :disabled="loading" @click="search"><Search :size="14" /> {{ loading ? 'Buscando…' : 'Buscar' }}</button></div></div></template>
