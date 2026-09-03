<script setup>
import { onMounted, ref, watch } from 'vue'
import { Paperclip, RefreshCw } from 'lucide-vue-next'
import PageHeader from '@/components/common/PageHeader.vue'
import BaseCard from '@/components/common/BaseCard.vue'
import Pagination from '@/components/common/Pagination.vue'
import StatusBadge from '@/components/common/StatusBadge.vue'
import { incidenciaService } from '@/services/incidenciaService'
import { aPagina, filas } from '@/services/paginacion'
import { useUiStore } from '@/stores/uiStore'

const ui = useUiStore()
const filter = ref('')
const loading = ref(true)
const rows = ref([])
const page = ref(1)
const pageSize = 10
const totalItems = ref(0)

// Una petición por página. Antes esta pantalla pedía todos los envíos y luego una consulta de
// incidencias por cada uno: con 300 envíos eran 301 llamadas para dibujar una tabla.
async function load() {
  loading.value = true
  try {
    const pagina = await incidenciaService.list({
      page: page.value,
      pageSize,
      search: filter.value.trim() || undefined,
    })
    rows.value = filas(pagina)
    totalItems.value = aPagina(pagina).totalItems
  } catch (error) {
    ui.notify(error.userMessage || 'No fue posible cargar las incidencias.', 'error')
  } finally {
    loading.value = false
  }
}

// La búsqueda vuelve a la primera página: quedarse en la página 7 de un resultado nuevo
// muestra una tabla vacía que parece "no hay incidencias".
function buscar() {
  page.value = 1
  load()
}

onMounted(load)
watch(page, load)
</script>

<template>
  <div>
    <PageHeader title="Incidencias" subtitle="Registro y seguimiento de novedades en envíos y equipos">
      <button class="btn btn-ghost" :disabled="loading" @click="load"><RefreshCw :size="16" /> Actualizar</button>
    </PageHeader>
    <div class="simple-toolbar">
      <input
        v-model="filter"
        class="form-control"
        placeholder="Buscar por descripción o número de envío"
        @keyup.enter="buscar"
      />
      <button class="btn btn-primary" :disabled="loading" @click="buscar">Buscar</button>
    </div>
    <div v-if="loading" class="page-loading">Cargando incidencias…</div>
    <div v-else-if="!rows.length" class="empty-state">No hay incidencias registradas.</div>
    <div v-else class="incident-list">
      <BaseCard v-for="item in rows" :key="item.incidenciaId">
        <div class="incident-head">
          <div>
            <strong>INC-{{ item.incidenciaId }} · Envío {{ item.numeroEnvio || item.envioId }}</strong>
            <span>Equipo asociado: {{ item.envioEquipoId || 'General' }}</span>
          </div>
          <StatusBadge status="open" />
        </div>
        <p>{{ item.descripcion }}</p>
        <div class="incident-meta">
          <span>{{ new Date(item.fechaCreacion).toLocaleString('es-DO') }}</span>
          <span v-if="item.usuarioCreacionId">Usuario: {{ item.usuarioCreacionId }}</span>
          <span><Paperclip :size="14" /> Registro API</span>
        </div>
      </BaseCard>
    </div>
    <Pagination :page="page" :total="totalItems" :page-size="pageSize" @update:page="page = $event" />
  </div>
</template>
