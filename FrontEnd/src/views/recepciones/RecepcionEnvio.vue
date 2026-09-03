<script setup>
import { computed, onMounted, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { ArrowLeft, CheckCircle2, Eye, TriangleAlert } from "lucide-vue-next";
import PageHeader from "@/components/common/PageHeader.vue";
import BaseCard from "@/components/common/BaseCard.vue";
import LoadingSpinner from "@/components/common/LoadingSpinner.vue";
import { envioService } from "@/services/envioService";
import { catalogoService } from "@/services/catalogoService";
import { filas } from '@/services/paginacion'
import { equipoService } from "@/services/equipoService";
import { recepcionService } from "@/services/recepcionService";
import { useUiStore } from "@/stores/uiStore";
import { confirmAction } from "@/utils/confirm";

// Estados de RecepcionEquipo en el backend (EstadoRecepcionEquipoEnum).
const VERIFICADO = 2;
const CON_INCIDENCIA = 3;

const route = useRoute();
const router = useRouter();
const ui = useUiStore();

const loading = ref(true);
const saving = ref(false);
const error = ref("");
const shipment = ref(null);
const items = ref([]);
const locations = ref([]);
const envioId = Number(route.params.envioId);

const locationName = (id) =>
  locations.value.find((x) => x.ubicacionId === id)?.nombre || (id ? `Ubicación #${id}` : "—");
const origen = computed(() => locationName(shipment.value?.ubicacionOrigenId));
const destino = computed(() => locationName(shipment.value?.ubicacionDestinoId));
const descripcion = computed(
  () => shipment.value?.observaciones?.trim() || "El envío no trae descripción.",
);
const sinDescripcion = computed(() => !shipment.value?.observaciones?.trim());

const conIncidencia = computed(() => items.value.some((x) => x.estado === CON_INCIDENCIA));
// Misma regla que VerificarEquipoRequestValidator: sin comentario no se documenta la incidencia.
const sinComentario = computed(() =>
  items.value.filter((x) => x.estado === CON_INCIDENCIA && !x.observaciones.trim()),
);
const puedeCompletar = computed(() => items.value.length > 0 && sinComentario.value.length === 0);

const resumen = computed(() =>
  conIncidencia.value
    ? `${items.value.filter((x) => x.estado === CON_INCIDENCIA).length} de ${items.value.length} equipos con incidencia`
    : `${items.value.length} equipos conformes`,
);

async function load() {
  loading.value = true;
  error.value = "";
  try {
    const [{ data: envio }, asociacionesPagina, ubicaciones] = await Promise.all([
      envioService.get(envioId),
      envioService.equipment(envioId, { pageSize: 100 }),
      catalogoService.allLocations(),
    ]);
    shipment.value = envio;
    locations.value = ubicaciones;
    items.value = await Promise.all(
      filas(asociacionesPagina).map(async (asociacion) => {
        const { data: equipo } = await equipoService.get(asociacion.equipoId);
        return {
          envioEquipoId: asociacion.envioEquipoId,
          ticket: asociacion.numeroTicket,
          notaEnvio: asociacion.observaciones,
          marca: equipo.marca,
          modelo: equipo.modelo,
          numeroSerie: equipo.numeroSerie,
          codigoActivo: equipo.codigoActivo,
          estado: VERIFICADO,
          observaciones: "",
        };
      }),
    );
  } catch (exception) {
    error.value = exception.userMessage || "No fue posible cargar el envío.";
  } finally {
    loading.value = false;
  }
}

async function completar() {
  if (!puedeCompletar.value) return;
  const texto = conIncidencia.value
    ? "El envío quedará como <strong>Recibido por Tecnología con incidencia</strong>. Es un estado final: no admite cambios posteriores."
    : "El envío quedará como <strong>Recibido por Tecnología</strong>. Es un estado final: no admite cambios posteriores.";
  if (!(await confirmAction({ title: "Completar recepción", html: `<p>${texto}</p>`, confirmText: "Completar recepción" })))
    return;

  saving.value = true;
  try {
    let recepcionId;
    try {
      recepcionId = (await envioService.reception(envioId)).data.recepcionId;
    } catch {
      recepcionId = (await recepcionService.create({ envioId })).data;
    }

    for (const item of items.value) {
      try {
        await recepcionService.verify(recepcionId, {
          envioEquipoId: item.envioEquipoId,
          estado: item.estado,
          observaciones: item.observaciones.trim() || null,
        });
      } catch (exception) {
        // 409 aquí significa que ese equipo ya se había verificado en un intento previo.
        // El resto de los conflictos vuelve a aparecer al completar, así que no se ocultan.
        if (exception.response?.status !== 409) throw exception;
      }
    }

    await recepcionService.complete(recepcionId);
    ui.notify(
      conIncidencia.value
        ? "Recepción completada con incidencia. El envío quedó recibido por Tecnología."
        : "Recepción completada. El envío quedó recibido por Tecnología.",
      "success",
    );
    router.push(`/envios/${envioId}`);
  } catch (exception) {
    ui.notify(exception.userMessage || "No fue posible completar la recepción.", "error");
  } finally {
    saving.value = false;
  }
}

onMounted(load);
</script>

<template>
  <div>
    <PageHeader
      title="Recepción de equipos"
      :subtitle="shipment ? `Envío ${shipment.numeroEnvio}` : 'Verificación de equipos'"
    >
      <button class="btn btn-ghost" type="button" @click="router.back()">
        <ArrowLeft :size="15" /> Volver
      </button>
    </PageHeader>

    <BaseCard v-if="error" class="error-banner">{{ error }}</BaseCard>
    <LoadingSpinner v-else-if="loading" />

    <template v-else>
      <BaseCard v-if="shipment" title="Envío">
        <div class="reception-summary">
          <dl>
            <div>
              <dt>Número</dt>
              <dd>{{ shipment.numeroEnvio }}</dd>
            </div>
            <div>
              <dt>Origen</dt>
              <dd>{{ origen }}</dd>
            </div>
            <div>
              <dt>Destino</dt>
              <dd>{{ destino }}</dd>
            </div>
            <div>
              <dt>Transporte</dt>
              <dd>{{ shipment.nombreTipoTransporte || "Sin indicar" }}</dd>
            </div>
            <div>
              <dt>Equipos</dt>
              <dd>{{ items.length }}</dd>
            </div>
          </dl>
          <button
            class="btn btn-ghost"
            type="button"
            title="Ver detalle"
            @click="router.push(`/envios/${envioId}`)"
          >
            <Eye :size="15" /> Ver detalle
          </button>
        </div>
        <p class="reception-description" :class="{ empty: sinDescripcion }">
          <span>Descripción</span>{{ descripcion }}
        </p>
      </BaseCard>

      <BaseCard v-if="!items.length">
        Este envío no tiene equipos asociados, así que no puede completarse la recepción.
      </BaseCard>

      <template v-else>
        <BaseCard title="Verificación de equipos" :padded="false">
          <ul class="reception-list">
            <li v-for="item in items" :key="item.envioEquipoId" class="reception-item">
              <div class="reception-item-head">
                <div>
                  <strong>{{ item.marca }} {{ item.modelo }}</strong>
                  <small
                    >Ticket {{ item.ticket }} · Serial {{ item.numeroSerie || "—" }} · Activo
                    {{ item.codigoActivo || "—" }}</small
                  >
                  <small v-if="item.notaEnvio" class="reception-note">{{ item.notaEnvio }}</small>
                </div>
                <div class="reception-choice">
                  <label :class="{ active: item.estado === VERIFICADO }">
                    <input v-model="item.estado" type="radio" :value="VERIFICADO" />
                    <CheckCircle2 :size="15" /> Conforme
                  </label>
                  <label :class="{ active: item.estado === CON_INCIDENCIA, warn: true }">
                    <input v-model="item.estado" type="radio" :value="CON_INCIDENCIA" />
                    <TriangleAlert :size="15" /> Con incidencia
                  </label>
                </div>
              </div>
              <div v-if="item.estado === CON_INCIDENCIA" class="reception-incident">
                <label :for="`obs-${item.envioEquipoId}`">Descripción de la incidencia</label>
                <textarea
                  :id="`obs-${item.envioEquipoId}`"
                  v-model="item.observaciones"
                  rows="2"
                  maxlength="2000"
                  placeholder="Qué se encontró: daño físico, faltante, no enciende…"
                ></textarea>
                <p v-if="!item.observaciones.trim()" class="reception-required">
                  Obligatorio para registrar la incidencia.
                </p>
              </div>
            </li>
          </ul>
        </BaseCard>

        <BaseCard>
          <div class="reception-footer">
            <div>
              <strong :class="conIncidencia ? 'tone-warn' : 'tone-ok'">
                {{ conIncidencia ? "Recibido con incidencia" : "Recibido" }}
              </strong>
              <small>{{ resumen }}</small>
              <small v-if="sinComentario.length" class="reception-required">
                Falta describir {{ sinComentario.length }}
                {{ sinComentario.length === 1 ? "incidencia" : "incidencias" }}.
              </small>
            </div>
            <button
              class="btn btn-primary"
              type="button"
              :disabled="!puedeCompletar || saving"
              @click="completar"
            >
              {{ saving ? "Completando…" : "Completar recepción" }}
            </button>
          </div>
        </BaseCard>
      </template>
    </template>
  </div>
</template>

<style scoped>
.reception-summary {
  display: flex;
  flex-wrap: wrap;
  gap: 14px 24px;
  align-items: flex-start;
  justify-content: space-between;
}
.reception-summary dl {
  display: flex;
  flex-wrap: wrap;
  gap: 6px 28px;
  margin: 0;
}
.reception-summary dt {
  font-size: 0.72rem;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  font-weight: 600;
  color: var(--text-muted, #6b7c87);
}
.reception-summary dd {
  margin: 2px 0 0;
  font-size: 0.92rem;
  font-weight: 600;
}
.reception-description {
  margin: 14px 0 0;
  padding: 11px 13px;
  background: var(--surface-sunk, #f4f6f8);
  border-left: 3px solid var(--border, #d5dee2);
  border-radius: 0 6px 6px 0;
  font-size: 0.9rem;
  line-height: 1.55;
  white-space: pre-line;
}
.reception-description span {
  display: block;
  font-size: 0.72rem;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  font-weight: 600;
  color: var(--text-muted, #6b7c87);
  margin-bottom: 3px;
}
.reception-description.empty {
  color: var(--text-muted, #6b7c87);
  font-style: italic;
}
.reception-list {
  list-style: none;
  margin: 0;
  padding: 0;
}
.reception-item {
  padding: 14px 18px;
  border-bottom: 1px solid var(--border, #e1e6ee);
}
.reception-item:last-child {
  border-bottom: none;
}
.reception-item-head {
  display: flex;
  flex-wrap: wrap;
  gap: 12px 20px;
  align-items: flex-start;
  justify-content: space-between;
}
.reception-item-head small {
  display: block;
  color: var(--text-muted, #6b7c87);
  font-size: 0.8rem;
  margin-top: 2px;
}
.reception-note {
  font-style: italic;
}
.reception-choice {
  display: flex;
  gap: 8px;
  flex: none;
}
.reception-choice label {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 7px 12px;
  border: 1px solid var(--border, #d5dee2);
  border-radius: 6px;
  font-size: 0.84rem;
  cursor: pointer;
  user-select: none;
}
.reception-choice input {
  margin: 0;
}
.reception-choice label.active {
  border-color: #2c6a46;
  background: #eaf4ee;
  color: #1f4d33;
  font-weight: 600;
}
.reception-choice label.warn.active {
  border-color: #a6341d;
  background: #f8e9e4;
  color: #8a2a17;
}
.reception-incident {
  margin-top: 12px;
}
.reception-incident label {
  display: block;
  font-size: 0.78rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  font-weight: 600;
  color: var(--text-muted, #6b7c87);
  margin-bottom: 5px;
}
.reception-incident textarea {
  width: 100%;
  padding: 9px 11px;
  border: 1px solid var(--border, #d5dee2);
  border-radius: 6px;
  font: inherit;
  font-size: 0.88rem;
  resize: vertical;
}
.reception-required {
  color: #a6341d;
  font-size: 0.78rem;
  margin: 5px 0 0;
}
.reception-footer {
  display: flex;
  flex-wrap: wrap;
  gap: 14px;
  align-items: center;
  justify-content: space-between;
}
.reception-footer small {
  display: block;
  color: var(--text-muted, #6b7c87);
  font-size: 0.82rem;
  margin-top: 2px;
}
.tone-ok {
  color: #2c6a46;
}
.tone-warn {
  color: #a6341d;
}
</style>
