<script setup>
import { onMounted, reactive, ref } from "vue";
import { useRouter } from "vue-router";
import { ArrowLeft, Save, X } from "lucide-vue-next";
import PageHeader from "@/components/common/PageHeader.vue";
import ShipmentSection from "@/components/shipments/ShipmentSection.vue";
import { catalogoService } from "@/services/catalogoService";
import { equipoService } from "@/services/equipoService";
import { useUiStore } from "@/stores/uiStore";
import { confirmAction } from "@/utils/confirm";
import { filtrarSoloDigitos } from "@/utils/documento";
import "@/assets/styles/shipment-create.css";

// El código de activo es numérico. Se filtra al escribir en vez de rechazarlo al guardar: dejar
// teclear una letra para después devolver un error es hacerle perder el tiempo a quien la escribe.
function alEscribirActivo(evento) {
  const limpio = filtrarSoloDigitos(evento.target.value);
  form.assetCode = limpio;
  evento.target.value = limpio;
}
const router = useRouter(),
  ui = useUiStore(),
  loading = ref(true),
  saving = ref(false),
  confirmOpen = ref(false),
  types = ref([]),
  locations = ref([]),
  errors = reactive({ type: "", serial: "", location: "" }),
  form = reactive({
    typeId: "",
    brand: "",
    model: "",
    serial: "",
    assetCode: "",
    locationId: "",
    notes: "",
  });
async function load() {
  try {
    const [t, l] = await Promise.all([catalogoService.allTypes(), catalogoService.allLocations()]);
    types.value = t;
    locations.value = l;
  } catch (e) {
    ui.notify(e.userMessage || "No fue posible cargar los catálogos.", "error");
  } finally {
    loading.value = false;
  }
}
async function submit() {
  errors.type = !form.typeId ? "Selecciona el tipo de equipo." : "";
  errors.serial = !form.serial ? "Indica el serial." : "";
  errors.location = !form.locationId ? "Selecciona la ubicación actual." : "";
  if (errors.type || errors.serial || errors.location) return;
  const accepted = await confirmAction({
    title: "Confirmar registro",
    text: "¿Deseas registrar este equipo en el inventario?",
    confirmText: "Confirmar y guardar",
  });
  if (accepted) await save();
}
async function save() {
  confirmOpen.value = false;
  saving.value = true;
  try {
    await equipoService.create({
      tipoEquipoId: Number(form.typeId),
      ubicacionActualId: Number(form.locationId),
      codigoActivo: form.assetCode || null,
      numeroSerie: form.serial,
      marca: form.brand || "Sin marca",
      modelo: form.model || "Sin modelo",
      observaciones: form.notes || null,
    });
    ui.notify("Equipo guardado correctamente.", "success");
    router.push("/equipos");
  } catch (e) {
    ui.notify(e.userMessage || "No fue posible guardar el equipo.", "error");
  } finally {
    saving.value = false;
  }
}
onMounted(load);
</script>
<template>
  <div class="shipment-create-page">
    <PageHeader
      title="Registrar equipo"
      subtitle="Crea un equipo disponible para asociarlo a un envío"
      ><button class="btn btn-secondary" type="button" @click="router.push('/equipos')">
        <ArrowLeft :size="16" /> Cancelar</button
      ><button class="btn btn-primary" type="button" :disabled="saving || loading" @click="submit">
        <Save :size="16" /> {{ saving ? "Guardando…" : "Guardar equipo" }}
      </button></PageHeader
    >
    <div v-if="loading" class="loading-state">Cargando catálogos…</div>
    <form v-else class="equipment-create-layout" @submit.prevent="submit">
      <main class="form-main">
        <ShipmentSection title="Identificación del equipo"
          ><div class="form-grid">
            <label
              >Tipo de equipo *<select v-model="form.typeId" class="form-control">
                <option value="">Seleccionar tipo</option>
                <option v-for="type in types" :key="type.tipoEquipoId" :value="type.tipoEquipoId">
                  {{ type.nombre }}
                </option></select
              ><small v-if="errors.type" class="field-error">{{ errors.type }}</small></label
            ><label
              >Serial *<input
                v-model.trim="form.serial"
                class="form-control"
                placeholder="Número de serie único"
              /><small v-if="errors.serial" class="field-error">{{ errors.serial }}</small></label
            ><label>Marca<input v-model.trim="form.brand" class="form-control" /></label
            ><label>Modelo<input v-model.trim="form.model" class="form-control" /></label
            ><label
              >Código de activo<input
                :value="form.assetCode"
                inputmode="numeric"
                class="form-control"
                @input="alEscribirActivo" /></label
            ><label
              >Ubicación actual *<select v-model="form.locationId" class="form-control">
                <option value="">Seleccionar ubicación</option>
                <option
                  v-for="location in locations"
                  :key="location.ubicacionId"
                  :value="location.ubicacionId"
                >
                  {{ location.nombre }}
                </option></select
              ><small v-if="errors.location" class="field-error">{{
                errors.location
              }}</small></label
            ><label class="span-2"
              >Observaciones<textarea
                v-model.trim="form.notes"
                class="form-control textarea"
                rows="3"
              />
            </label></div
        ></ShipmentSection>
      </main>
      <aside class="shipment-summary">
        <header>Antes de guardar</header>
        <div class="shipment-summary-body">
          <p class="form-hint">
            El serial y el código de activo deben ser únicos. El equipo quedará disponible para
            asociarlo posteriormente a un envío.
          </p>
        </div>
      </aside>
    </form>
    <div v-if="confirmOpen" class="confirm-overlay" role="dialog" aria-modal="true">
      <div class="confirm-dialog">
        <button
          class="confirm-close"
          type="button"
          aria-label="Cerrar"
          @click="confirmOpen = false"
        >
          <X :size="17" />
        </button>
        <h2>Confirmar registro</h2>
        <p>¿Deseas registrar este equipo en el inventario?</p>
        <div class="confirm-actions">
          <button class="btn btn-secondary" type="button" @click="confirmOpen = false">
            Cancelar</button
          ><button class="btn btn-primary" type="button" @click="save">
            <Save :size="15" /> Confirmar y guardar
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
