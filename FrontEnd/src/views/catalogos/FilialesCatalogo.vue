<script setup>
import { computed, onMounted, reactive, ref, watch } from "vue";
import { Plus, RefreshCw } from "lucide-vue-next";
import PageHeader from "@/components/common/PageHeader.vue";
import BaseCard from "@/components/common/BaseCard.vue";
import Pagination from "@/components/common/Pagination.vue";
import { catalogoService } from "@/services/catalogoService";
import { aPagina, filas } from "@/services/paginacion";
import { useUiStore } from "@/stores/uiStore";
import { confirmAction } from "@/utils/confirm";

// En la base la tabla se llama Ubicaciones y aqui se les dice filiales: son lo mismo. La
// pantalla lista todos los centros, incluido Tecnologia, que es el otro extremo de cada envio.
const TIPO_FILIAL = 1;
const TIPO_TECNOLOGIA = 2;
const TIPOS = [
  { valor: TIPO_FILIAL, etiqueta: "Filial" },
  { valor: TIPO_TECNOLOGIA, etiqueta: "Tecnología" },
];
const nombreTipo = (t) => TIPOS.find((x) => x.valor === t)?.etiqueta || "—";

const ui = useUiStore();
const formulario = ref(null);
const loading = ref(true);
const guardando = ref(false);
const ubicaciones = ref([]);
const total = ref(0);
const page = ref(1);
const pageSize = 10;

// El formulario sirve para crear y para editar: con id, actualiza.
const form = reactive({
  ubicacionId: 0,
  nombre: "",
  codigoCentro: "",
  tipo: TIPO_FILIAL,
  filialExternaId: "",
});
const editando = computed(() => form.ubicacionId > 0);
const esFilial = computed(() => Number(form.tipo) === TIPO_FILIAL);

// Solo la filial lleva id de AuthManager; a Tecnologia darselo la meteria dentro del alcance de
// esa filial. Al cambiar de tipo se limpia para no enviar un valor que el backend rechazaria.
watch(esFilial, (ahora) => {
  if (!ahora) form.filialExternaId = "";
});

async function load() {
  loading.value = true;
  try {
    // soloActivos en false: deshabilitar un centro no puede hacerlo desaparecer de la pantalla
    // donde se vuelve a habilitar.
    const pagina = await catalogoService.locations({
      soloActivos: false,
      page: page.value,
      pageSize,
    });
    ubicaciones.value = filas(pagina);
    total.value = aPagina(pagina).totalItems;
  } catch (e) {
    ui.notify(e.userMessage || "No fue posible cargar los centros.", "error");
  } finally {
    loading.value = false;
  }
}

function limpiar() {
  form.ubicacionId = 0;
  form.nombre = "";
  form.codigoCentro = "";
  form.tipo = TIPO_FILIAL;
  form.filialExternaId = "";
}

function editar(centro) {
  form.ubicacionId = centro.ubicacionId;
  form.nombre = centro.nombre;
  form.codigoCentro = centro.codigoCentro;
  form.tipo = centro.tipo;
  form.filialExternaId = centro.filialExternaId ?? "";
  // El formulario está arriba del listado: al editar una fila del final se rellenaba fuera de
  // pantalla y parecía que el botón no hacía nada.
  formulario.value?.scrollIntoView({ behavior: "smooth", block: "start" });
}

async function guardar() {
  const nombre = form.nombre.trim();
  const codigo = form.codigoCentro.trim();
  const externa = Number.parseInt(form.filialExternaId, 10);

  if (!nombre || !codigo) return ui.notify("Completa el nombre y el código de centro.", "warning");
  // El id de AuthManager es lo que ata la filial con sus usuarios: sin el, nadie la alcanza y el
  // error aparece el dia que alguien de esa filial intenta entrar. Tecnologia no lo lleva.
  if (esFilial.value && (!Number.isInteger(externa) || externa <= 0))
    return ui.notify(
      "El id de AuthManager es obligatorio para una filial y debe ser mayor que cero.",
      "warning",
    );

  guardando.value = true;
  try {
    const datos = {
      nombre,
      codigoCentro: codigo,
      tipo: Number(form.tipo),
      filialExternaId: esFilial.value ? externa : null,
    };
    if (editando.value) {
      await catalogoService.updateLocation(form.ubicacionId, {
        ...datos,
        ubicacionId: form.ubicacionId,
      });
      ui.notify("Centro actualizado.", "success");
    } else {
      await catalogoService.createLocation(datos);
      ui.notify("Centro creado.", "success");
    }
    limpiar();
    await load();
  } catch (e) {
    ui.notify(e.userMessage || "No fue posible guardar el centro.", "error");
  } finally {
    guardando.value = false;
  }
}

async function alternar(centro) {
  const deshabilitando = centro.activo;
  const confirmado = await confirmAction({
    title: `${deshabilitando ? "Deshabilitar" : "Habilitar"} ${centro.nombre}`,
    html: deshabilitando
      ? "<p>Dejará de ofrecerse como origen o destino en envíos nuevos. Los envíos que ya lo referencian no se tocan.</p>"
      : "<p>Volverá a estar disponible para envíos nuevos.</p>",
    confirmText: deshabilitando ? "Deshabilitar" : "Habilitar",
  });
  if (!confirmado) return;
  try {
    await catalogoService.toggleLocation(centro.ubicacionId, !centro.activo);
    ui.notify(deshabilitando ? "Centro deshabilitado." : "Centro habilitado.", "success");
    await load();
  } catch (e) {
    ui.notify(e.userMessage || "No fue posible cambiar el estado del centro.", "error");
  }
}

onMounted(load);
watch(page, load);
</script>

<template>
  <div>
    <PageHeader title="Filiales" subtitle="Centros del sistema: alta, edición y baja">
      <button class="btn btn-ghost" :disabled="loading" @click="load">
        <RefreshCw :size="15" /> Actualizar
      </button>
    </PageHeader>

    <div ref="formulario">
      <BaseCard :title="editando ? `Editar ${form.nombre || 'centro'}` : 'Nuevo centro'">
        <div class="filial-form">
          <label>
            Nombre *
            <input v-model.trim="form.nombre" class="form-control" placeholder="Santo Domingo" />
          </label>
          <label>
            Código de centro *
            <input v-model.trim="form.codigoCentro" class="form-control" placeholder="SDQ" />
          </label>
          <label>
            Tipo *
            <select v-model="form.tipo" class="form-control">
              <option v-for="t in TIPOS" :key="t.valor" :value="t.valor">{{ t.etiqueta }}</option>
            </select>
          </label>
          <label v-if="esFilial">
            Id en AuthManager *
            <input
              v-model.trim="form.filialExternaId"
              class="form-control"
              :class="{ 'input-heredado': editando }"
              :readonly="editando"
              inputmode="numeric"
              placeholder="30"
              @input="form.filialExternaId = form.filialExternaId.replace(/\D/g, '')"
            />
            <small class="field-hint">
              <template v-if="editando">
                No se cambia desde aquí: decide qué envíos ve esa filial. Lo ajusta Tecnología
                directamente en la base.
              </template>
              <template v-else>
                El número que AuthManager envía en el claim <strong>affiliate</strong>. Es lo que
                permite que los usuarios de esa filial vean sus envíos.
              </template>
            </small>
          </label>
          <div class="filial-acciones">
            <button class="btn btn-primary" type="button" :disabled="guardando" @click="guardar">
              <Plus v-if="!editando" :size="15" />
              {{ guardando ? "Guardando…" : editando ? "Guardar cambios" : "Crear centro" }}
            </button>
            <button v-if="editando" class="btn btn-ghost" type="button" @click="limpiar">
              Cancelar
            </button>
          </div>
        </div>
      </BaseCard>
    </div>

    <BaseCard title="Centros registrados" :padded="false">
      <div class="table-responsive">
        <table class="data-table">
          <thead>
            <tr>
              <th>Nombre</th>
              <th>Código</th>
              <th>Tipo</th>
              <th>Id AuthManager</th>
              <th>Estado</th>
              <th class="actions-cell">Acciones</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="loading">
              <td colspan="6">Cargando…</td>
            </tr>
            <tr v-else-if="!ubicaciones.length">
              <td colspan="6">No hay centros registrados.</td>
            </tr>
            <tr v-for="filial in ubicaciones" v-else :key="filial.ubicacionId">
              <td>
                <span class="cell-strong">{{ filial.nombre }}</span>
              </td>
              <td>{{ filial.codigoCentro }}</td>
              <td>{{ nombreTipo(filial.tipo) }}</td>
              <td>
                <span v-if="filial.filialExternaId">{{ filial.filialExternaId }}</span>
                <span
                  v-else-if="filial.tipo === TIPO_FILIAL"
                  class="filial-sin-mapear"
                  title="Sus usuarios no podrán entrar"
                >
                  sin asignar
                </span>
                <span v-else>—</span>
              </td>
              <td>
                <span class="status-badge" :class="filial.activo ? 'tone-success' : 'tone-neutral'">
                  <i />{{ filial.activo ? "Activo" : "Deshabilitado" }}
                </span>
              </td>
              <td class="actions-cell">
                <button class="btn btn-ghost" type="button" @click="editar(filial)">Editar</button>
                <button class="btn btn-secondary" type="button" @click="alternar(filial)">
                  {{ filial.activo ? "Deshabilitar" : "Habilitar" }}
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </BaseCard>
    <Pagination :page="page" :total="total" :page-size="pageSize" @update:page="page = $event" />
  </div>
</template>

<style scoped>
.filial-form {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 14px;
  align-items: start;
}
.filial-form label {
  display: flex;
  flex-direction: column;
  gap: 5px;
  font-size: 11px;
  color: #68758b;
}
.filial-acciones {
  display: flex;
  gap: 8px;
  align-items: center;
  padding-top: 17px;
}
.filial-sin-mapear {
  color: #c7535b;
  font-size: 10px;
  font-weight: 700;
}
</style>
