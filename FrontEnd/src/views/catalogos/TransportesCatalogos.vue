<script setup>
import { onMounted, reactive, ref } from "vue";
import { Plus, RefreshCw } from "lucide-vue-next";
import PageHeader from "@/components/common/PageHeader.vue";
import BaseCard from "@/components/common/BaseCard.vue";
import { catalogoService } from "@/services/catalogoService";
import { useUiStore } from "@/stores/uiStore";
import { isInternalTransport } from "@/utils/transport";
import { confirmAction } from "@/utils/confirm";

const ui = useUiStore();
const loading = ref(true);
const savingType = ref(false);
const savingDriver = ref(false);
const types = ref([]);
const drivers = ref([]);
const typeForm = reactive({ codigo: "", nombre: "", estrategia: 1 });
const driverForm = reactive({ nombreCompleto: "", numeroEmpleado: "" });
async function load() {
  loading.value = true;
  try {
    const [t, d] = await Promise.all([
      catalogoService.transportTypes({ soloActivos: false }),
      catalogoService.internalDrivers({ soloActivos: false }),
    ]);
    types.value = t.data || [];
    drivers.value = d.data || [];
  } catch (e) {
    ui.notify(e.userMessage || "No fue posible cargar los catálogos.", "error");
  } finally {
    loading.value = false;
  }
}
async function createType() {
  if (!typeForm.codigo.trim() || !typeForm.nombre.trim())
    return ui.notify("Completa el código y nombre del tipo.", "warning");
  if (!(await confirmAction({ title: "Crear tipo de transporte", text: `¿Deseas crear el tipo ${typeForm.nombre}?`, confirmText: "Crear tipo" }))) return;
  savingType.value = true;
  try {
    await catalogoService.createTransportType({
      ...typeForm,
      codigo: typeForm.codigo.trim().toUpperCase(),
      nombre: typeForm.nombre.trim(),
      estrategia: Number(typeForm.estrategia),
    });
    Object.assign(typeForm, { codigo: "", nombre: "", estrategia: 1 });
    await load();
    ui.notify("Tipo de transporte creado.");
  } catch (e) {
    ui.notify(e.userMessage || "No fue posible crear el tipo.", "error");
  } finally {
    savingType.value = false;
  }
}
async function createDriver() {
  if (!driverForm.nombreCompleto.trim() || !driverForm.numeroEmpleado.trim())
    return ui.notify("Completa el nombre y número de empleado.", "warning");
  if (!(await confirmAction({ title: "Crear chofer interno", text: `¿Deseas registrar a ${driverForm.nombreCompleto}?`, confirmText: "Crear chofer" }))) return;
  savingDriver.value = true;
  try {
    await catalogoService.createInternalDriver({
      nombreCompleto: driverForm.nombreCompleto.trim(),
      numeroEmpleado: driverForm.numeroEmpleado.trim(),
    });
    Object.assign(driverForm, { nombreCompleto: "", numeroEmpleado: "" });
    await load();
    ui.notify("Chofer interno creado.");
  } catch (e) {
    ui.notify(e.userMessage || "No fue posible crear el chofer.", "error");
  } finally {
    savingDriver.value = false;
  }
}
async function toggleType(x) {
  if (!(await confirmAction({ title: `${x.activo ? "Deshabilitar" : "Habilitar"} tipo`, text: `¿Deseas cambiar el estado de ${x.nombre}?`, confirmText: x.activo ? "Deshabilitar" : "Habilitar" }))) return;
  try {
    await catalogoService.toggleTransportType(x.tipoTransporteId, !x.activo);
    await load();
  } catch (e) {
    ui.notify(e.userMessage || "No fue posible cambiar el estado.", "error");
  }
}
async function toggleDriver(x) {
  if (!(await confirmAction({ title: `${x.activo ? "Deshabilitar" : "Habilitar"} chofer`, text: `¿Deseas cambiar el estado de ${x.nombreCompleto}?`, confirmText: x.activo ? "Deshabilitar" : "Habilitar" }))) return;
  try {
    await catalogoService.toggleInternalDriver(x.choferInternoId, !x.activo);
    await load();
  } catch (e) {
    ui.notify(e.userMessage || "No fue posible cambiar el estado.", "error");
  }
}
onMounted(load);
</script>
<template>
  <div>
    <PageHeader
      title="Transportes y choferes"
      subtitle="Catálogos operativos con conservación del historial"
      ><button class="btn btn-secondary" @click="load">
        <RefreshCw :size="16" /> Actualizar
      </button></PageHeader
    >
    <div v-if="loading" class="loading-state">Cargando…</div>
    <div v-else class="detail-grid">
      <BaseCard title="Tipos de transporte"
        ><form class="form-grid" @submit.prevent="createType">
          <label
            >Código *<input
              v-model="typeForm.codigo"
              class="form-control"
              maxlength="30" /></label
          ><label
            >Nombre *<input
              v-model="typeForm.nombre"
              class="form-control"
              maxlength="100" /></label
          ><label
            >Estrategia *<select
              v-model="typeForm.estrategia"
              class="form-control"
            >
              <option :value="1">Transportación institucional</option>
              <option :value="2">Entrega directa a Tecnología</option>
            </select></label
          ><button class="btn btn-primary" :disabled="savingType">
            <Plus :size="16" /> Agregar tipo
          </button>
        </form>
        <div class="catalog-list">
          <article v-for="item in types" :key="item.tipoTransporteId">
            <span
              ><strong>{{ item.nombre }}</strong
              ><small
                >{{ item.codigo }} ·
                {{
                  isInternalTransport(item.estrategia)
                    ? "Institucional"
                    : "Directo a Tecnología"
                }}</small
              ></span
            ><button class="btn btn-secondary" @click="toggleType(item)">
              {{ item.activo ? "Deshabilitar" : "Habilitar" }}
            </button>
          </article>
        </div></BaseCard
      ><BaseCard title="Choferes internos"
        ><form class="form-grid" @submit.prevent="createDriver">
          <label
            >Nombre completo *<input
              v-model="driverForm.nombreCompleto"
              class="form-control"
              maxlength="150" /></label
          ><label
            >Número de empleado *<input
              v-model="driverForm.numeroEmpleado"
              class="form-control"
              maxlength="30" /></label
          ><button class="btn btn-primary" :disabled="savingDriver">
            <Plus :size="16" /> Agregar chofer
          </button>
        </form>
        <div class="catalog-list">
          <article v-for="item in drivers" :key="item.choferInternoId">
            <span
              ><strong>{{ item.nombreCompleto }}</strong
              ><small
                >{{ item.numeroEmpleado }} ·
                {{ item.activo ? "Activo" : "Inactivo" }}</small
              ></span
            ><button class="btn btn-secondary" @click="toggleDriver(item)">
              {{ item.activo ? "Deshabilitar" : "Habilitar" }}
            </button>
          </article>
        </div></BaseCard
      >
    </div>
  </div>
</template>
<style scoped>
.form-grid {
  display: grid;
  gap: 1rem;
  margin-bottom: 1.25rem;
}
.form-grid label {
  display: grid;
  gap: 0.35rem;
}
.form-grid .btn {
  justify-self: start;
}
.catalog-list {
  display: grid;
  gap: 0.65rem;
}
.catalog-list article {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  padding: 0.8rem;
  border: 1px solid var(--border-color, #dfe5ec);
  border-radius: 0.65rem;
}
.catalog-list span {
  display: grid;
  gap: 0.2rem;
}
.catalog-list small {
  color: var(--text-muted, #64748b);
}
</style>
