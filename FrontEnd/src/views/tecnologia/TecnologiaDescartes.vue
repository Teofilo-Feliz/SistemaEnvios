<script setup>
import { computed, onMounted, ref, watch } from "vue";
import { useRouter } from "vue-router";
import { PackageX, RefreshCw } from "lucide-vue-next";
import PageHeader from "@/components/common/PageHeader.vue";
import BaseCard from "@/components/common/BaseCard.vue";
import BaseTable from "@/components/common/BaseTable.vue";
import Pagination from "@/components/common/Pagination.vue";
import ModalCard from "@/components/common/ModalCard.vue";
import { casoService } from "@/services/casoService";
import { aPagina, filas } from "@/services/paginacion";
import { useUiStore } from "@/stores/uiStore";

const router = useRouter();
const ui = useUiStore();
const loading = ref(true);
const rows = ref([]);
const page = ref(1);
const pageSize = 10;
const totalItems = ref(0);
const filtro = ref("");

// Motivos cerrados en vez de texto libre: el descarte es la decisión de que un equipo no
// vuelve a su filial, y conviene poder contarlos por causa más adelante.
const MOTIVOS = [
  "Equipo dado de baja",
  "Retenido en Tecnología",
  "Reemplazado por otro equipo",
  "Reasignado a otra filial",
];

const seleccionado = ref(null);
const motivo = ref(MOTIVOS[0]);
const detalle = ref("");
const descartando = ref(false);

const columns = [
  { key: "ticket", label: "Ticket" },
  { key: "equipo", label: "Equipo" },
  { key: "serial", label: "Serial" },
  { key: "filial", label: "Filial dueña" },
  { key: "vueltas", label: "Movimientos" },
  { key: "antiguedad", label: "Abierto hace" },
];

const filas_ = computed(() =>
  rows.value.map((x) => ({
    id: x.equipoId,
    equipoId: x.equipoId,
    ticket: x.numeroTicket,
    equipo: `${x.marca} ${x.modelo}`,
    serial: x.numeroSerie || x.codigoActivo || "—",
    filial: x.filialNombre,
    vueltas: x.movimientos,
    antiguedad: x.diasAbierto === 0 ? "hoy" : `${x.diasAbierto} día${x.diasAbierto === 1 ? "" : "s"}`,
    filialNombre: x.filialNombre,
    numeroTicket: x.numeroTicket,
    marca: x.marca,
    modelo: x.modelo,
    canConfirm: true,
    confirmTitle: "Descartar de la filial",
  })),
);

async function load() {
  loading.value = true;
  try {
    const pagina = await casoService.list({
      page: page.value,
      pageSize,
      search: filtro.value.trim() || undefined,
    });
    rows.value = filas(pagina);
    totalItems.value = aPagina(pagina).totalItems;
  } catch (error) {
    ui.notify(error.userMessage || "No fue posible cargar los casos abiertos.", "error");
  } finally {
    loading.value = false;
  }
}

function buscar() {
  page.value = 1;
  load();
}

function abrir(row) {
  seleccionado.value = row;
  motivo.value = MOTIVOS[0];
  detalle.value = "";
}

async function descartar() {
  if (!seleccionado.value) return;
  descartando.value = true;
  try {
    await casoService.discard({
      equipoId: seleccionado.value.equipoId,
      motivo: detalle.value.trim() ? `${motivo.value} — ${detalle.value.trim()}` : motivo.value,
    });
    ui.notify(
      `Equipo descartado de ${seleccionado.value.filialNombre}. El ticket ${seleccionado.value.numeroTicket} queda cerrado.`,
      "success",
    );
    seleccionado.value = null;
    await load();
  } catch (error) {
    ui.notify(error.userMessage || "No fue posible descartar el equipo.", "error");
  } finally {
    descartando.value = false;
  }
}

onMounted(load);
watch(page, load);
</script>

<template>
  <div>
    <PageHeader
      title="Descarte de equipos"
      subtitle="Equipos en Tecnología cuyo caso sigue abierto porque aún no volvieron a su filial"
    >
      <button class="btn btn-ghost" :disabled="loading" @click="load">
        <RefreshCw :size="16" /> Actualizar
      </button>
    </PageHeader>

    <BaseCard>
      <p class="descarte-nota">
        Mientras el caso siga abierto, cada movimiento de ese equipo hereda su ticket y el equipo
        solo puede volver a la filial que lo envió. <strong>Descartarlo cierra el ticket</strong> y
        libera el equipo para asignarlo a cualquier filial con un ticket nuevo.
      </p>
    </BaseCard>

    <div class="simple-toolbar">
      <input
        v-model="filtro"
        class="form-control"
        placeholder="Buscar por ticket, serial o código de activo"
        @keyup.enter="buscar"
      />
      <button class="btn btn-primary" :disabled="loading" @click="buscar">Buscar</button>
    </div>

    <BaseCard :padded="false">
      <div v-if="loading" class="page-loading">Cargando casos abiertos…</div>
      <div v-else-if="!filas_.length" class="empty-state">
        No hay equipos en Tecnología con casos abiertos.
      </div>
      <BaseTable
        v-else
        :columns="columns"
        :rows="filas_"
        @confirm="abrir"
        @view="(row) => router.push(`/equipos/${row.equipoId}`)"
      />
      <Pagination
        :page="page"
        :total="totalItems"
        :page-size="pageSize"
        @update:page="page = $event"
      />
    </BaseCard>

    <ModalCard
      :open="Boolean(seleccionado)"
      eyebrow="Descartar equipo"
      :title="seleccionado ? `${seleccionado.marca} ${seleccionado.modelo}` : ''"
      @close="seleccionado = null"
    >
      <div v-if="seleccionado" class="descarte-form">
        <p class="descarte-aviso">
          <PackageX :size="16" />
          El ticket <strong>{{ seleccionado.numeroTicket }}</strong> se cerrará y
          <strong>{{ seleccionado.filialNombre }}</strong> dejará de ser dueña de este equipo.
          Esta acción no se revierte.
        </p>
        <label>
          Motivo del descarte
          <select v-model="motivo" class="form-control">
            <option v-for="opcion in MOTIVOS" :key="opcion" :value="opcion">{{ opcion }}</option>
          </select>
        </label>
        <label>
          Detalle (opcional)
          <textarea
            v-model="detalle"
            class="form-control"
            rows="3"
            maxlength="300"
            placeholder="Queda registrado junto al motivo en el cierre del caso"
          />
        </label>
      </div>
      <template #footer>
        <button class="btn btn-ghost" type="button" @click="seleccionado = null">Cancelar</button>
        <button class="btn btn-primary" type="button" :disabled="descartando" @click="descartar">
          {{ descartando ? "Descartando…" : "Descartar equipo" }}
        </button>
      </template>
    </ModalCard>
  </div>
</template>

<style scoped>
.descarte-nota {
  margin: 0;
  color: var(--muted);
  line-height: 1.55;
}
.descarte-form {
  display: grid;
  gap: 1rem;
}
.descarte-form label {
  display: grid;
  gap: 0.35rem;
}
.descarte-aviso {
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
  margin: 0;
  padding: 0.75rem;
  border: 1px solid var(--border);
  border-left: 3px solid #b4533f;
  border-radius: 4px;
  line-height: 1.5;
}
</style>
