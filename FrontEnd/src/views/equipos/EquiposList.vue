<script setup>
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { Plus, RefreshCw } from 'lucide-vue-next'
import PageHeader from '@/components/common/PageHeader.vue'
import BaseCard from '@/components/common/BaseCard.vue'
import BaseTable from '@/components/common/BaseTable.vue'
import ModalCard from '@/components/common/ModalCard.vue'
import EquipoInfoCard from '@/components/equipos/EquipoInfoCard.vue'
import AdvancedFilter from '@/components/filters/AdvancedFilter.vue'
import { equipmentFilterFields } from '@/config/equipmentFilterFields'
import { equipoService } from '@/services/equipoService'
import { catalogoService } from '@/services/catalogoService'
import { useUiStore } from '@/stores/uiStore'
const router=useRouter(),ui=useUiStore(),loading=ref(true),equipment=ref([]),locations=ref([]),types=ref([]),filters=ref({logic:'AND',rules:[]})
const columns=[{key:'type',label:'Equipo'},{key:'brandModel',label:'Marca / Modelo'},{key:'serial',label:'Serial'},{key:'asset',label:'Código activo'},{key:'location',label:'Ubicación'}]
const sources=computed(()=>({locations:locations.value.map(x=>({value:String(x.ubicacionId),label:x.nombre})),types:types.value.map(x=>({value:String(x.tipoEquipoId),label:x.nombre}))}))
const rows=computed(()=>equipment.value.map(x=>({...x,id:x.equipoId,type:types.value.find(t=>t.tipoEquipoId===x.tipoEquipoId)?.nombre||`Tipo #${x.tipoEquipoId}`,brandModel:`${x.marca} ${x.modelo}`,serial:x.numeroSerie||'—',asset:x.codigoActivo||'—',location:locations.value.find(l=>l.ubicacionId===x.ubicacionActualId)?.nombre||`Ubicación #${x.ubicacionActualId}`})))
function compare(v,op,e){const a=String(v??'').toLowerCase(),b=String(e??'').toLowerCase();if(op==='equals')return a===b;if(op==='notEquals')return a!==b;if(op==='contains')return a.includes(b);if(op==='notContains')return !a.includes(b);if(op==='startsWith')return a.startsWith(b);if(op==='endsWith')return a.endsWith(b);if(op==='empty')return !a;if(op==='notEmpty')return !!a;return op==='greaterThan'?Number(v)>Number(e):op==='greaterOrEqual'?Number(v)>=Number(e):op==='lessThan'?Number(v)<Number(e):op==='lessOrEqual'?Number(v)<=Number(e):true}
function matches(row,g){const result=g.rules.map(r=>r.type==='group'?matches(row,r):r.type==='global'?compare(Object.values(row).join(' '),r.operator,r.value):(!r.value&&!['empty','notEmpty'].includes(r.operator))||compare(row[r.field],r.operator,r.value));return g.logic==='OR'?result.some(Boolean):result.every(Boolean)}
const filtered=computed(()=>filters.value.rules?.length?rows.value.filter(r=>matches(r,filters.value)):rows.value)
async function load(){loading.value=true;try{const [e,l,t]=await Promise.all([equipoService.list(),catalogoService.locations(),catalogoService.types()]);equipment.value=e.data||[];locations.value=l.data||[];types.value=t.data||[]}catch(error){ui.notify(error.userMessage||'No fue posible cargar los equipos.','error')}finally{loading.value=false}}
function search(value){filters.value=value} function clear(){filters.value={logic:'AND',rules:[]};load()} onMounted(load)
// El listado ya trae todos los campos del equipo, así que el popup no necesita otra consulta.
const selectedId=ref(null)
const selected=computed(()=>equipment.value.find(x=>x.equipoId===selectedId.value)||null)
const selectedType=computed(()=>types.value.find(x=>x.tipoEquipoId===selected.value?.tipoEquipoId)?.nombre||'Equipo')
</script>
<template><div><PageHeader title="Equipos" subtitle="Equipos registrados y asociados a envíos"><RouterLink class="btn btn-primary" to="/equipos/nuevo"><Plus :size="16"/> Nuevo equipo</RouterLink><button class="btn btn-ghost" @click="load"><RefreshCw :size="16"/> Actualizar</button></PageHeader><AdvancedFilter :fields="equipmentFilterFields" :sources="sources" :loading="loading" @search="search" @clear="clear"/><BaseCard :padded="false"><div v-if="loading" class="page-loading">Cargando equipos…</div><BaseTable v-else :columns="columns" :rows="filtered" @view="row => selectedId = row.id"/></BaseCard><ModalCard :open="Boolean(selected)" :eyebrow="selectedType" :title="selected ? `${selected.marca} ${selected.modelo}` : ''" @close="selectedId = null"><EquipoInfoCard v-if="selected" :equipo="selected" :types="types" :locations="locations"/><template #footer><button class="btn btn-ghost" type="button" @click="router.push(`/equipos/${selected.equipoId}`)">Abrir ficha completa</button></template></ModalCard></div></template>
