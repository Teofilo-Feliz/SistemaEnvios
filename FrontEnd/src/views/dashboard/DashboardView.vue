<script setup>
import { computed, onMounted, ref } from 'vue'
import { Maximize, PackageOpen, RefreshCw, Truck, AlertTriangle, Clock3, PackageCheck, Search, CheckCircle2 } from 'lucide-vue-next'
import { useRouter } from 'vue-router'
import PageHeader from '@/components/common/PageHeader.vue'
import BaseTable from '@/components/common/BaseTable.vue'
import DashboardChart from '@/components/dashboard/DashboardChart.vue'
import DashboardTabs from '@/components/dashboard/DashboardTabs.vue'
import DashboardSelector from '@/components/dashboard/DashboardSelector.vue'
import DashboardKpiCard from '@/components/dashboard/DashboardKpiCard.vue'
import DashboardWidget from '@/components/dashboard/DashboardWidget.vue'
import { useDashboard } from '@/composables/useDashboard'
import { envioService } from '@/services/envioService'
import { filas } from '@/services/paginacion'
import { catalogoService } from '@/services/catalogoService'

const router = useRouter(); const root = ref(null); const tab = ref('dashboard'); const selected = ref('general'); const latest = ref([]); const states = ref([]); const { data, summary, loading, error, load } = useDashboard()
const colors = ['#3568c8', '#e0a12b', '#2d9971', '#c7535b', '#7b86a3', '#6b5cc5']
const kpis = computed(() => [{ title: 'Total de envíos', value: summary.value.totalEnvios, icon: PackageOpen, tone: 'blue', path: '/envios' }, { title: 'En tránsito', value: summary.value.enTransito, icon: Truck, tone: 'indigo', path: '/envios?status=EN_TRANSITO' }, { title: 'Incidencias', value: summary.value.incidencias, icon: AlertTriangle, tone: 'red', path: '/incidencias' }])
const smallKpis = computed(() => [{ title: 'Pendientes de recepción', value: summary.value.pendientesRecepcion, icon: Clock3, tone: 'amber', path: '/envios' }, { title: 'Recibidos', value: summary.value.recibidos, icon: PackageCheck, tone: 'green', path: '/envios' }, { title: 'En revisión', value: summary.value.enRevision, icon: Search, tone: 'cyan', path: '/tecnologia' }, { title: 'Cerrados', value: summary.value.cerrados, icon: CheckCircle2, tone: 'slate', path: '/envios' }])
const evolution = computed(() => ({ labels: (data.value?.evolution || []).map((x) => x.month), datasets: [{ label: 'Total', data: (data.value?.evolution || []).map((x) => x.total), borderColor: colors[0], backgroundColor: '#3568c820', fill: true, tension: .3 }, { label: 'En tránsito', data: (data.value?.evolution || []).map((x) => x.enTransito), borderColor: colors[5], tension: .3 }, { label: 'Recibidos', data: (data.value?.evolution || []).map((x) => x.recibidos), borderColor: colors[2], tension: .3 }, { label: 'Cerrados', data: (data.value?.evolution || []).map((x) => x.cerrados), borderColor: colors[3], tension: .3 }] }))
const statusChart = computed(() => { const source = data.value?.statusByMonth || []; const labels = [...new Set(source.map((x) => x.month))]; const states = [...new Set(source.map((x) => x.estado))]; return { labels, datasets: states.map((state, index) => ({ label: state, data: labels.map((month) => source.find((x) => x.month === month && x.estado === state)?.total || 0), backgroundColor: colors[index % colors.length], stack: 'status' })) } })
const originChart = computed(() => ({ labels: (data.value?.shipmentsByOrigin || []).map((x) => x.label), datasets: [{ label: 'Envíos', data: (data.value?.shipmentsByOrigin || []).map((x) => x.total), backgroundColor: colors[0], borderRadius: 2 }] }))
const typeChart = computed(() => ({ labels: (data.value?.equipmentByType || []).map((x) => x.label), datasets: [{ label: 'Equipos', data: (data.value?.equipmentByType || []).map((x) => x.total), backgroundColor: colors, borderWidth: 0 }] }))
const columns = [{ key: 'number', label: 'Número' }, { key: 'status', label: 'Estado' }, { key: 'date', label: 'Fecha' }]
function go(path) { router.push(path) }
// El envío trae el id numérico del estado; StatusBadge rotula por código. Sin traducir, la
// tabla mostraba el id crudo ("20") en vez del estado.
function stateCode(id) { return states.value.find((x) => x.estadoEnvioId === id)?.codigo || '' }
// El listado viene ordenado por fecha descendente, así que la primera página de cinco son
// los más recientes. Antes se hacía slice(-5) sobre la lista completa, que devolvía los cinco
// más antiguos.
async function loadDashboard() { await load(); try { const [estados, pagina] = await Promise.all([catalogoService.allStates(), envioService.paged({ pageSize: 5 })]); states.value = estados; latest.value = filas(pagina).map((x) => ({ id: x.envioId, number: x.numeroEnvio, status: stateCode(x.estadoEnvioId), date: '—' })) } catch { latest.value = [] } }
function fullscreen() { if (document.fullscreenElement) document.exitFullscreen(); else root.value?.requestFullscreen?.() }
onMounted(loadDashboard)
</script>
<template><div ref="root" class="dashboard-page"><PageHeader title="Dashboard operativo" subtitle="Resumen de la operación de envíos"><template #default><button class="btn btn-secondary" :disabled="loading" @click="loadDashboard"><RefreshCw :size="15" /> Actualizar</button><button class="btn btn-secondary" @click="fullscreen"><Maximize :size="15" /> Pantalla completa</button><RouterLink class="btn btn-primary" to="/envios/nuevo">Nuevo envío</RouterLink></template></PageHeader><DashboardTabs v-model="tab" /><div class="dashboard-toolbar"><DashboardSelector v-model="selected" /><span class="dashboard-toolbar-label">Vista {{ tab === 'dashboard' ? 'general' : tab }}</span></div><div v-if="error" class="dashboard-alert">{{ error }}</div><div class="dashboard-main-grid"><div class="dashboard-kpi-column"><DashboardKpiCard v-for="kpi in kpis" :key="kpi.title" v-bind="kpi" large @click="go(kpi.path)" /></div><DashboardWidget class="dashboard-evolution-widget" title="Evolución de envíos en los últimos 12 meses" :loading="loading" :error="error" @retry="loadDashboard"><DashboardChart :data="evolution" /></DashboardWidget><DashboardWidget class="dashboard-status-widget" title="Estado de envíos por mes" :loading="loading" :error="error" @retry="loadDashboard"><DashboardChart type="bar" :data="statusChart" /></DashboardWidget></div><div class="dashboard-secondary-grid"><div class="dashboard-secondary-kpis"><DashboardKpiCard v-for="kpi in smallKpis" :key="kpi.title" v-bind="kpi" @click="go(kpi.path)" /></div><div class="dashboard-secondary-charts"><DashboardWidget title="Envíos por origen" :loading="loading" :error="error" @retry="loadDashboard"><DashboardChart type="bar" :data="originChart" /></DashboardWidget><DashboardWidget title="Equipos por tipo" :loading="loading" :error="error" @retry="loadDashboard"><DashboardChart type="doughnut" :data="typeChart" /></DashboardWidget></div></div><DashboardWidget class="dashboard-latest-widget" title="Últimos envíos" :loading="loading" :error="''"><BaseTable :columns="columns" :rows="latest" @view="(row) => router.push(`/envios/${row.id}`)" /></DashboardWidget></div></template>
