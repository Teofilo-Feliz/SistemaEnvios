<script setup>
import { computed, reactive, ref, watch } from "vue";
import { Plus, Search } from "lucide-vue-next";
import ShipmentEquipmentTable from "./ShipmentEquipmentTable.vue";
import { glpiService } from "@/services/glpiService";
import { filtrarSoloDigitos } from "@/utils/documento";

const props = defineProps({
  item: Object,
  equipments: Array,
  types: Array,
  registeredEquipment: Array,
  registering: Boolean,
});
const emit = defineEmits(["add", "remove", "edit", "new"]);
const errors = reactive({ typeId: "", brand: "", model: "", serial: "", ticket: "" });

// El código de activo es numérico. Se filtra al teclear en vez de rechazarlo al guardar: dejar
// escribir una letra para después devolver un error es hacerle perder el tiempo a quien la escribe.
function alEscribirActivo(evento) {
  const limpio = filtrarSoloDigitos(evento.target.value);
  props.item.assetCode = limpio;
  // El input no está en v-model, así que si el valor filtrado coincide con el anterior Vue no
  // vuelve a pintarlo y la letra se quedaría en pantalla. Se fuerza.
  evento.target.value = limpio;
}

const buscando = ref(false);
const avisoTicket = ref("");
// El formulario del equipo no existe hasta que el ticket se busca. Es lo que hace imposible que
// queden datos de un ticket pegados a otro: si el número cambia, esto vuelve a false y no hay
// campos que limpiar.
const camposVisibles = ref(false);
// Campos que llegó a llenar la mesa de ayuda. Se bloquean: cambiarlos aquí contradiría el
// inventario de GLPI. Los que GLPI dejó vacíos siguen abiertos, porque si no un equipo sin serial
// allá impediría crear el envío aquí.
const deGlpi = reactive({
  typeId: false,
  brand: false,
  model: false,
  serial: false,
  assetCode: false,
});

// El ticket ya decidió qué equipo es, así que elegir otro del inventario lo contradiría.
const equipoLoDecideGlpi = computed(() => Object.values(deGlpi).some(Boolean));

function olvidarLoDeGlpi() {
  Object.keys(deGlpi).forEach((campo) => (deGlpi[campo] = false));
}

const puedeBuscar = computed(
  () => !buscando.value && String(props.item.ticket || "").trim().length >= 3,
);

// El ticket heredado viene de un caso abierto y no se busca: su equipo ya está decidido.
watch(
  () => props.item.ticketHeredado,
  (heredado) => {
    if (heredado) camposVisibles.value = true;
  },
  { immediate: true },
);

watch(
  () => props.item.ticket,
  () => {
    if (props.item.ticketHeredado) return;
    camposVisibles.value = false;
    avisoTicket.value = "";
    errors.ticket = "";
    olvidarLoDeGlpi();
  },
);

function alEscribirTicket(evento) {
  const limpio = filtrarSoloDigitos(evento.target.value);
  props.item.ticket = limpio;
  evento.target.value = limpio;
}

/**
 * Busca el equipo del ticket y abre el formulario.
 *
 * Los campos se despliegan aunque la mesa de ayuda no devuelva nada: un ticket sin activo asociado
 * es un caso legítimo y la persona escribe los datos. Lo que no abre el formulario es un ticket
 * rechazado —inexistente, o con varios equipos— porque entonces no hay envío posible con él y
 * llenar la forma sería trabajo perdido.
 */
async function buscarEquipo() {
  const ticket = String(props.item.ticket || "").trim();
  errors.ticket = "";
  avisoTicket.value = "";

  buscando.value = true;
  try {
    const { data } = await glpiService.equipoDeTicket(ticket);
    const equipo = data?.equipo;

    if (!equipo) {
      avisoTicket.value = "El ticket no tiene equipo en la mesa de ayuda. Escriba los datos.";
      camposVisibles.value = true;
      return;
    }

    const traidos = {
      typeId: equipo.tipoEquipoId,
      brand: equipo.marca,
      model: equipo.modelo,
      serial: equipo.serial,
      assetCode: equipo.codigoActivo,
    };
    for (const [campo, valor] of Object.entries(traidos)) {
      if (!valor) continue;
      props.item[campo] = valor;
      deGlpi[campo] = true;
    }

    avisoTicket.value = equipo.yaEstaEnOtroEnvio
      ? `${equipo.nombre || "El equipo"} ya viaja en otro envío activo y no se puede agregar aquí.`
      : `Datos de la mesa de ayuda${equipo.nombre ? ` (${equipo.nombre})` : ""}: no se editan. ` +
        "Complete los que vengan vacíos.";
    camposVisibles.value = true;
  } catch (error) {
    // GLPI caído no bloquea: el servidor aplica la misma política al guardar, y frenar aquí
    // dejaría a todas las filiales sin poder crear envíos.
    if (error.response?.status === 502) {
      avisoTicket.value = "La mesa de ayuda no responde. Escriba los datos del equipo.";
      camposVisibles.value = true;
      return;
    }
    errors.ticket = error.userMessage || "No se pudo consultar el ticket en la mesa de ayuda.";
  } finally {
    buscando.value = false;
  }
}

async function validateAndAdd() {
  errors.typeId = props.item.typeId ? "" : "El tipo de equipo es obligatorio.";
  errors.brand = props.item.brand ? "" : "La marca es obligatoria.";
  errors.model = props.item.model ? "" : "El modelo es obligatorio.";
  errors.serial = props.item.serial ? "" : "El número de serie es obligatorio.";
  // Un ticket heredado no se valida ni se comprueba contra los ya usados: repetirlo es
  // exactamente lo que se busca, porque pertenece al caso y no a este viaje.
  errors.ticket = props.item.ticketHeredado
    ? ""
    : !props.item.ticket
      ? "El número de ticket es obligatorio."
      : !/^\d+$/.test(props.item.ticket)
        ? "El número de ticket solo permite caracteres numéricos."
        : props.item.ticket.length < 3
          ? "El número de ticket debe tener al menos 3 dígitos."
          : // "0" o "000" son dígitos y del largo pedido, pero en la mesa de ayuda no existe el
            // caso cero. Se mira dígito a dígito porque el campo admite hasta 50 y ese número
            // desborda cualquier entero. Los ceros a la izquierda sí valen: "007" es el ticket 7.
            !/[1-9]/.test(props.item.ticket)
            ? "El número de ticket debe ser mayor que cero."
            : props.equipments.some(
                  (equipment) =>
                    equipment.ticket === props.item.ticket && equipment.id !== props.item.id,
                )
              ? "Este número de ticket ya fue agregado."
              : "";
  // El ticket ya pasó por "Buscar equipo" —sin esa búsqueda no habría campos que llenar—, así que
  // aquí no se vuelve a consultar la mesa de ayuda. Si el número cambia, el formulario se cierra y
  // hay que buscar otra vez.
  if (Object.values(errors).some(Boolean)) return;
  emit("add");
}
</script>

<template>
  <div class="equipment-form compact-equipment-form">
    <label class="equipment-ticket"
      >Ticket *<input
        :value="item.ticket"
        class="form-control"
        :readonly="item.ticketHeredado"
        :class="{ 'input-heredado': item.ticketHeredado }"
        inputmode="numeric"
        pattern="[0-9]*"
        placeholder="Solo números"
        @input="alEscribirTicket"
        @keyup.enter="puedeBuscar && buscarEquipo()"
      /><small v-if="item.ticketHeredado" class="field-hint"
        >Heredado del caso abierto{{ item.ticketFilial ? ` de ${item.ticketFilial}` : "" }}. No se
        modifica.</small
      ><small v-else-if="errors.ticket" class="field-error">{{ errors.ticket }}</small
      ><small v-else-if="avisoTicket" class="field-hint">{{ avisoTicket }}</small></label
    >
    <button
      v-if="!item.ticketHeredado"
      class="btn btn-secondary equipment-search-button"
      type="button"
      :disabled="!puedeBuscar"
      @click="buscarEquipo"
    >
      <Search :size="15" /> {{ buscando ? "Buscando…" : "Buscar equipo" }}
    </button>

    <template v-if="camposVisibles">
      <label class="equipment-existing"
        >Equipo existente (opcional)<select
          v-model="item.existingId"
          class="form-control"
          :disabled="equipoLoDecideGlpi"
          :class="{ 'input-heredado': equipoLoDecideGlpi }"
        >
          <option value="">Registrar equipo nuevo</option>
          <option
            v-for="equipment in registeredEquipment"
            :key="equipment.equipoId"
            :value="equipment.equipoId"
          >
            {{ equipment.numeroSerie || equipment.codigoActivo || `Equipo #${equipment.equipoId}` }}
            · {{ equipment.marca }} {{ equipment.modelo }}
          </option>
        </select></label
      >
      <button
        v-if="item.existingId"
        class="btn btn-ghost equipment-new-button"
        type="button"
        @click="emit('new')"
      >
        + Nuevo equipo
      </button>
      <label
        >Tipo *<select
          v-model="item.typeId"
          class="form-control"
          :disabled="deGlpi.typeId"
          :class="{ 'input-heredado': deGlpi.typeId }"
        >
          <option value="">Seleccionar</option>
          <option v-for="type in types" :key="type.tipoEquipoId" :value="type.tipoEquipoId">
            {{ type.nombre }}
          </option></select
        ><small v-if="errors.typeId" class="field-error">{{ errors.typeId }}</small></label
      >
      <label
        >Marca *<input
          v-model.trim="item.brand"
          class="form-control"
          :readonly="deGlpi.brand"
          :class="{ 'input-heredado': deGlpi.brand }"
          placeholder="Marca"
        /><small v-if="errors.brand" class="field-error">{{ errors.brand }}</small></label
      >
      <label
        >Modelo *<input
          v-model.trim="item.model"
          class="form-control"
          :readonly="deGlpi.model"
          :class="{ 'input-heredado': deGlpi.model }"
          placeholder="Modelo"
        /><small v-if="errors.model" class="field-error">{{ errors.model }}</small></label
      >
      <label
        >Serial *<input
          v-model.trim="item.serial"
          class="form-control"
          :readonly="deGlpi.serial"
          :class="{ 'input-heredado': deGlpi.serial }"
          placeholder="Serial"
        /><small v-if="errors.serial" class="field-error">{{ errors.serial }}</small></label
      >
      <label
        >Código activo<input
          :value="item.assetCode"
          class="form-control"
          :readonly="deGlpi.assetCode"
          :class="{ 'input-heredado': deGlpi.assetCode }"
          inputmode="numeric"
          placeholder="Opcional, solo números"
          @input="alEscribirActivo"
      /></label>
      <label class="equipment-notes"
        >Observación<input v-model.trim="item.notes" class="form-control" placeholder="Observación"
      /></label>
      <button
        class="btn btn-secondary equipment-add-button"
        type="button"
        :disabled="registering"
        @click="validateAndAdd"
      >
        <Plus :size="15" /> {{ registering ? "Registrando…" : "Agregar" }}
      </button>
    </template>
  </div>
  <ShipmentEquipmentTable
    :equipments="equipments"
    @remove="emit('remove', $event)"
    @edit="emit('edit', $event)"
  />
</template>

<style scoped>
/* El ticket abre el formulario, así que va solo en su fila con el botón al lado. */
.equipment-ticket {
  grid-column: span 2;
}
.equipment-search-button {
  align-self: end;
}
</style>
