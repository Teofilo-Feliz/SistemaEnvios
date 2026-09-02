<script setup>
import { ArrowDownUp, CheckCircle2, Eye, Pencil } from 'lucide-vue-next'
import StatusBadge from './StatusBadge.vue'
import EmptyState from './EmptyState.vue'
import LoadingSpinner from './LoadingSpinner.vue'
defineProps({ columns: Array, rows: Array, loading: Boolean, rowKey: { type: String, default: 'id' }, editable: Boolean })
defineEmits(['view', 'edit', 'sort', 'confirm'])
</script>
<template>
  <div class="table-responsive"><LoadingSpinner v-if="loading" /><EmptyState v-else-if="!rows.length" /><table v-else class="data-table"><thead><tr><th v-for="column in columns" :key="column.key"><button v-if="column.sortable" @click="$emit('sort', column.key)">{{ column.label }} <ArrowDownUp :size="13" /></button><span v-else>{{ column.label }}</span></th><th class="actions-cell">Acciones</th></tr></thead><tbody><tr v-for="row in rows" :key="row[rowKey]"><td v-for="column in columns" :key="column.key" :data-label="column.label"><StatusBadge v-if="column.key === 'status'" :status="row[column.key]" /><span v-else :class="{ 'cell-strong': column.key === 'number' }">{{ row[column.key] ?? '—' }}</span></td><td class="actions-cell" data-label="Acciones"><button v-if="row.canConfirm" class="table-action table-action-confirm" :title="row.confirmTitle || 'Confirmar envío'" @click="$emit('confirm', row)"><CheckCircle2 :size="16" /></button><button class="table-action" title="Ver detalle" @click="$emit('view', row)"><Eye :size="16" /></button><button v-if="editable && row.canEdit !== false" class="table-action" title="Editar" @click="$emit('edit', row)"><Pencil :size="16" /></button></td></tr></tbody></table></div>
</template>
