<script setup>
import { Plus, X } from 'lucide-vue-next'
import FilterRule from './FilterRule.vue'
import GlobalFilterRule from './GlobalFilterRule.vue'
import { createRule, createGroup, createGlobalRule } from '@/config/filterFields'
const props = defineProps({ group: { type: Object, required: true }, fields: { type: Array, required: true }, sources: { type: Object, default: () => ({}) }, root: Boolean })
const emit = defineEmits(['update', 'remove'])
function updateRule(index, value) { const rules = [...props.group.rules]; rules[index] = value; emit('update', { ...props.group, rules }) }
function removeRule(index) { const rules = props.group.rules.filter((_, current) => current !== index); emit('update', { ...props.group, rules: rules.length ? rules : [createRule()] }) }
function add(value) { emit('update', { ...props.group, rules: [...props.group.rules, value] }) }
function patchLogic(value) { emit('update', { ...props.group, logic: value }) }
</script>
<template>
  <section class="filter-group" :class="{ 'filter-group-root': root }"><div v-if="!root" class="filter-group-head"><span>( Grupo</span><select class="filter-control filter-group-logic" :value="group.logic" @change="patchLogic($event.target.value)"><option value="AND">Y</option><option value="OR">O</option></select><button class="filter-remove" title="Eliminar grupo" type="button" @click="emit('remove')"><X :size="14" /></button></div><div v-for="(rule, index) in group.rules" :key="rule.id" class="filter-group-item"><FilterGroup v-if="rule.type === 'group'" :group="rule" :fields="fields" :sources="sources" @update="updateRule(index, $event)" @remove="removeRule(index)" /><GlobalFilterRule v-else-if="rule.type === 'global'" :rule="rule" @update="updateRule(index, $event)" @remove="removeRule(index)" /><FilterRule v-else :rule="rule" :fields="fields" :sources="sources" :nested="!root" @update="updateRule(index, $event)" @remove="removeRule(index)" /></div><div class="filter-group-actions"><button class="filter-link" type="button" @click="add(createRule())"><Plus :size="13" /> regla</button><button class="filter-link" type="button" @click="add(createGlobalRule())"><Plus :size="13" /> regla global</button><button class="filter-link" type="button" @click="add(createGroup())"><Plus :size="13" /> grupo</button></div></section>
</template>
