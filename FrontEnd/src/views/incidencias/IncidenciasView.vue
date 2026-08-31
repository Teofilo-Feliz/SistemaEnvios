<script setup>
import { computed, onMounted, ref } from 'vue'
import { Paperclip, RefreshCw } from 'lucide-vue-next'
import PageHeader from '@/components/common/PageHeader.vue'
import BaseCard from '@/components/common/BaseCard.vue'
import StatusBadge from '@/components/common/StatusBadge.vue'
import { envioService } from '@/services/envioService'
import { useUiStore } from '@/stores/uiStore'
const ui = useUiStore(); const filter = ref(''); const loading = ref(true); const incidents = ref([])
const rows = computed(() => incidents.value.filter((x) => !filter.value || `${x.descripcion} ${x.envioId}`.toLowerCase().includes(filter.value.toLowerCase())))
async function load() { loading.value = true; try { const shipments = (await envioService.list()).data || []; const results = await Promise.all(shipments.map((shipment) => envioService.incidents(shipment.envioId).catch(() => ({ data: [] })))); incidents.value = results.flatMap((result, index) => (result.data || []).map((item) => ({ ...item, envioNumero: shipments[index].numeroEnvio }))) } catch (error) { ui.showToast(error.userMessage || 'No fue posible cargar las incidencias.', 'error') } finally { loading.value = false } }
onMounted(load)
</script>
<template><div><PageHeader title="Incidencias" subtitle="Registro y seguimiento de novedades en envíos y equipos"><button class="btn btn-ghost" @click="load"><RefreshCw :size="16" /> Actualizar</button></PageHeader><div class="simple-toolbar"><input v-model="filter" class="form-control" placeholder="Buscar incidencia, envío o descripción" /></div><div v-if="loading" class="page-loading">Cargando incidencias…</div><div v-else-if="!rows.length" class="empty-state">No hay incidencias registradas.</div><div v-else class="incident-list"><BaseCard v-for="item in rows" :key="item.incidenciaId"><div class="incident-head"><div><strong>INC-{{ item.incidenciaId }} · Envío {{ item.envioNumero || item.envioId }}</strong><span>Equipo asociado: {{ item.envioEquipoId || 'General' }}</span></div><StatusBadge status="open" /></div><p>{{ item.descripcion }}</p><div class="incident-meta"><span>{{ new Date(item.fechaCreacion).toLocaleString('es-DO') }}</span><span v-if="item.usuarioCreacionId">Usuario: {{ item.usuarioCreacionId }}</span><span><Paperclip :size="14" /> Registro API</span></div></BaseCard></div></div></template>
