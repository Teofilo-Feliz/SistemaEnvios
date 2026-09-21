<script setup>
/**
 * Edita un equipo ya agregado al envío, sin sacarlo de la tabla.
 *
 * Antes el lápiz volcaba la fila al formulario de "agregar" y la quitaba de la tabla. La fila
 * desaparecía, así que parecía borrada; si el usuario no volvía a pulsar "Agregar" el equipo se
 * perdía sin avisar, y si estaba escribiendo otro equipo ese borrador se sobrescribía.
 *
 * Aquí el borrador es una copia: cancelar no toca la fila, guardar la reemplaza entera.
 */
import { computed, reactive, ref, watch } from "vue";
import ModalCard from "@/components/common/ModalCard.vue";
import { glpiService } from "@/services/glpiService";
import { filtrarCodigoActivo } from "@/utils/documento";
import { avisar, escapeHtml } from "@/utils/confirm";

const props = defineProps({
  open: { type: Boolean, default: false },
  equipment: { type: Object, default: null },
  equipments: { type: Array, default: () => [] },
  types: { type: Array, default: () => [] },
  /** Sin equipos.gestionar el backend rechaza el PUT, asi que esos campos se muestran bloqueados. */
  puedeEditarEquipo: { type: Boolean, default: true },
});
const emit = defineEmits(["close", "save"]);

const borrador = reactive({
  typeId: "",
  brand: "",
  model: "",
  serial: "",
  assetCode: "",
  ticket: "",
  notes: "",
});
const errors = reactive({
  typeId: "",
  brand: "",
  model: "",
  serial: "",
  assetCode: "",
  ticket: "",
});
const ticketOriginal = ref("");
// El ticket sobre el que se pulsó "Buscar equipo". Si no coincide con el que hay escrito, los
// datos que se ven son de otro ticket y guardar dejaría la fila mintiendo.
const ticketBuscado = ref("");
// Campos que llegó a llenar la mesa de ayuda. Se bloquean: cambiarlos contradiría el inventario
// de GLPI. Los que vengan vacíos siguen abiertos para completarlos a mano.
const deGlpi = reactive({
  typeId: false,
  brand: false,
  model: false,
  serial: false,
  assetCode: false,
});

function olvidarLoDeGlpi() {
  Object.keys(deGlpi).forEach((campo) => (deGlpi[campo] = false));
}

// Un campo se edita si el permiso lo consiente y GLPI no lo dictó.
const editable = (campo) => props.puedeEditarEquipo && !deGlpi[campo];

const heredado = computed(() => Boolean(props.equipment?.ticketHeredado));

/** Las demás filas del envío. El equipo que se edita no compite consigo mismo. */
const otrasFilas = computed(() =>
  props.equipments.filter((fila) => fila.id !== props.equipment?.id),
);

watch(
  () => [props.open, props.equipment],
  () => {
    if (!props.open || !props.equipment) return;
    borrador.typeId = props.equipment.typeId ?? "";
    borrador.brand = props.equipment.brand ?? "";
    borrador.model = props.equipment.model ?? "";
    borrador.serial = props.equipment.serial ?? "";
    borrador.assetCode = props.equipment.assetCode ?? "";
    borrador.ticket = props.equipment.ticket ?? "";
    borrador.notes = props.equipment.notes ?? "";
    ticketOriginal.value = props.equipment.ticket ?? "";
    ticketBuscado.value = props.equipment.ticket ?? "";
    olvidarLoDeGlpi();
    Object.keys(errors).forEach((campo) => (errors[campo] = ""));
  },
  { immediate: true },
);

/**
 * El código de activo es numérico: se filtra al teclear en vez de rechazarlo al guardar.
 *
 * El input no va en v-model a propósito. Si fuera, Vue escribiría el valor crudo antes de
 * filtrarlo; y como aquí se asigna a mano, hay que devolverlo también al elemento: cuando lo
 * filtrado coincide con lo que ya había, Vue no vuelve a pintar y la letra se queda en pantalla.
 */
function alEscribirActivo(evento) {
  const limpio = filtrarCodigoActivo(evento.target.value);
  borrador.assetCode = limpio;
  evento.target.value = limpio;
  errors.assetCode = "";
}

function alEscribirTicket(evento) {
  const limpio = filtrarCodigoActivo(evento.target.value);
  borrador.ticket = limpio;
  evento.target.value = limpio;
  errors.ticket = "";
}

const iguales = (uno, otro) => String(uno ?? "").toLowerCase() === String(otro ?? "").toLowerCase();

/**
 * Las mismas reglas del formulario de agregar. Lo que no necesita preguntarle al servidor.
 *
 * Dos detalles del ticket. Un ticket heredado pertenece al caso y no a este viaje, así que ni se
 * escribe ni se valida. Y "0" o "000" son dígitos y del largo pedido, pero en la mesa de ayuda no
 * existe el caso cero: se mira dígito a dígito porque el campo admite hasta 50 y ese número
 * desborda cualquier entero. Los ceros a la izquierda sí valen, "007" es el ticket 7.
 */
function validarEnLocal() {
  errors.typeId = borrador.typeId ? "" : "El tipo de equipo es obligatorio.";
  errors.brand = borrador.brand ? "" : "La marca es obligatoria.";
  errors.model = borrador.model ? "" : "El modelo es obligatorio.";
  errors.serial = borrador.serial ? "" : "El número de serie es obligatorio.";

  errors.serial ||= otrasFilas.value.some((fila) => iguales(fila.serial, borrador.serial))
    ? "Este serial ya está en otro equipo del envío."
    : "";
  errors.assetCode =
    borrador.assetCode &&
    otrasFilas.value.some((fila) => iguales(fila.assetCode, borrador.assetCode))
      ? "Este código de activo ya está en otro equipo del envío."
      : "";

  errors.ticket = heredado.value
    ? ""
    : !borrador.ticket
      ? "El número de ticket es obligatorio."
      : borrador.ticket.length < 3
        ? "El número de ticket debe tener al menos 3 dígitos."
        : !/[1-9]/.test(borrador.ticket)
          ? "El número de ticket debe ser mayor que cero."
          : otrasFilas.value.some((fila) => fila.ticket === borrador.ticket)
            ? "Este número de ticket ya fue agregado."
            : "";

  return !Object.values(errors).some(Boolean);
}

const buscando = ref(false);
const avisoTicket = ref("");

const ticketCambio = computed(
  () => !heredado.value && borrador.ticket.trim() !== ticketOriginal.value.trim(),
);
const puedeBuscar = computed(
  () => ticketCambio.value && !buscando.value && borrador.ticket.trim().length >= 3,
);

/**
 * Trae el equipo del ticket nuevo y lo pone en el borrador.
 *
 * El equipo sigue al ticket: si el número cambia, los datos del anterior dejan de valer. Sin
 * equipo asociado los campos se vacían para que la persona los escriba, que es el caso de quien
 * abre el ticket antes de colgarle el activo.
 */
async function buscarEquipo() {
  const ticket = borrador.ticket.trim();
  errors.ticket = "";
  avisoTicket.value = "";
  buscando.value = true;

  try {
    const { data } = await glpiService.equipoDeTicket(ticket);
    const equipo = data?.equipo;

    ticketBuscado.value = ticket;

    if (!equipo) {
      vaciarDatosDelEquipo();
      avisoTicket.value = "El ticket no tiene equipo en la mesa de ayuda. Escriba los datos.";
      return;
    }

    olvidarLoDeGlpi();
    const traidos = {
      typeId: equipo.tipoEquipoId,
      brand: equipo.marca,
      model: equipo.modelo,
      serial: equipo.serial,
      assetCode: equipo.codigoActivo,
    };
    for (const [campo, valor] of Object.entries(traidos)) {
      borrador[campo] = valor ?? "";
      if (valor) deGlpi[campo] = true;
    }
    Object.keys(errors).forEach((campo) => (errors[campo] = ""));

    avisoTicket.value = equipo.yaEstaEnOtroEnvio
      ? `${equipo.nombre || "El equipo"} ya viaja en otro envío activo.`
      : `Datos de la mesa de ayuda${equipo.nombre ? ` (${equipo.nombre})` : ""}: no se editan.`;
  } catch (error) {
    // GLPI caído no bloquea: se deja escribir a mano, igual que en el resto del sistema.
    if (error.response?.status === 502) {
      // Con GLPI caído no se puede traer nada, pero tampoco se le puede cerrar el paso a quien
      // necesita corregir un ticket. Se da por buscado y el aviso pide escribir los datos.
      ticketBuscado.value = ticket;
      avisoTicket.value = "La mesa de ayuda no responde. Escriba los datos del equipo.";
      return;
    }
    errors.ticket = error.userMessage || "No se pudo consultar el ticket en la mesa de ayuda.";
  } finally {
    buscando.value = false;
  }
}

function vaciarDatosDelEquipo() {
  olvidarLoDeGlpi();
  borrador.typeId = "";
  borrador.brand = "";
  borrador.model = "";
  borrador.serial = "";
  borrador.assetCode = "";
}

async function guardar() {
  // El ticket cambió pero nadie pulsó "Buscar equipo": lo que se ve en pantalla sigue siendo el
  // equipo del ticket anterior. Guardar así dejaría la fila diciendo que el caso de esta máquina
  // es un número que pertenece a otra.
  if (!heredado.value && borrador.ticket.trim() !== ticketBuscado.value.trim()) {
    await avisar({
      title: "Falta buscar el equipo",
      html:
        `Cambió el ticket a <b>${escapeHtml(borrador.ticket.trim() || "(vacío)")}</b> pero los ` +
        "datos que se ven siguen siendo los del ticket anterior.<br><br>" +
        "Pulse <b>Buscar equipo del ticket</b> para traer el equipo que le corresponde.",
    });
    return;
  }

  if (!validarEnLocal()) return;

  emit("save", {
    ...props.equipment,
    typeId: borrador.typeId,
    brand: borrador.brand,
    model: borrador.model,
    serial: borrador.serial,
    assetCode: borrador.assetCode,
    ticket: heredado.value ? props.equipment.ticket : borrador.ticket,
    notes: borrador.notes,
    typeName:
      props.types.find((tipo) => tipo.tipoEquipoId === Number(borrador.typeId))?.nombre ||
      props.equipment.typeName,
  });
}
</script>

<template>
  <ModalCard
    :open="open"
    width="660px"
    eyebrow="Editar equipo del envío"
    :title="equipment ? `${equipment.brand} ${equipment.model}`.trim() || 'Equipo' : ''"
    @close="emit('close')"
  >
    <div v-if="equipment" class="equipo-editar">
      <p v-if="puedeEditarEquipo" class="equipo-editar-aviso">
        Tipo, marca, modelo, serial y código de activo son del equipo: al guardarlos cambian también
        en el inventario. El ticket y la observación son de este envío.
      </p>
      <p v-else class="equipo-editar-aviso">
        Los datos del equipo están bloqueados: no tienes el permiso para mantener equipos. Puedes
        cambiar el ticket y la observación, que son de este envío.
      </p>

      <label
        >Tipo *<select
          v-model="borrador.typeId"
          class="form-control"
          :disabled="!editable('typeId')"
        >
          <option value="">Seleccionar</option>
          <option v-for="tipo in types" :key="tipo.tipoEquipoId" :value="tipo.tipoEquipoId">
            {{ tipo.nombre }}
          </option></select
        ><small v-if="errors.typeId" class="field-error">{{ errors.typeId }}</small></label
      >

      <label
        >Marca *<input
          v-model.trim="borrador.brand"
          class="form-control"
          :readonly="!editable('brand')"
          :class="{ 'input-heredado': !editable('brand') }"
          placeholder="Marca"
          @input="errors.brand = ''"
        /><small v-if="errors.brand" class="field-error">{{ errors.brand }}</small></label
      >

      <label
        >Modelo *<input
          v-model.trim="borrador.model"
          class="form-control"
          :readonly="!editable('model')"
          :class="{ 'input-heredado': !editable('model') }"
          placeholder="Modelo"
          @input="errors.model = ''"
        /><small v-if="errors.model" class="field-error">{{ errors.model }}</small></label
      >

      <label
        >Serial *<input
          v-model.trim="borrador.serial"
          class="form-control"
          :readonly="!editable('serial')"
          :class="{ 'input-heredado': !editable('serial') }"
          placeholder="Serial"
          @input="errors.serial = ''"
        /><small v-if="errors.serial" class="field-error">{{ errors.serial }}</small></label
      >

      <label
        >Código activo<input
          :value="borrador.assetCode"
          class="form-control"
          :readonly="!editable('assetCode')"
          :class="{ 'input-heredado': !editable('assetCode') }"
          inputmode="numeric"
          maxlength="8"
          placeholder="Opcional, 8 dígitos"
          @input="alEscribirActivo"
        /><small v-if="errors.assetCode" class="field-error">{{ errors.assetCode }}</small></label
      >

      <label
        >Ticket *<input
          :value="borrador.ticket"
          class="form-control"
          :readonly="heredado"
          :class="{ 'input-heredado': heredado }"
          inputmode="numeric"
          placeholder="Solo números"
          @input="alEscribirTicket"
        /><small v-if="heredado" class="field-hint"
          >Heredado del caso abierto{{
            equipment.ticketFilial ? ` de ${equipment.ticketFilial}` : ""
          }}. No se modifica.</small
        ><small v-else-if="errors.ticket" class="field-error">{{ errors.ticket }}</small
        ><small v-else-if="avisoTicket" class="field-hint">{{ avisoTicket }}</small></label
      >

      <button
        v-if="ticketCambio"
        class="btn btn-secondary equipo-editar-buscar"
        type="button"
        :disabled="!puedeBuscar"
        @click="buscarEquipo"
      >
        {{ buscando ? "Buscando…" : "Buscar equipo del ticket" }}
      </button>

      <label class="equipo-editar-ancho"
        >Observación<input
          v-model.trim="borrador.notes"
          class="form-control"
          placeholder="Observación"
      /></label>
    </div>

    <template #footer>
      <button class="btn btn-ghost" type="button" @click="emit('close')">Cancelar</button>
      <button class="btn btn-primary" type="button" @click="guardar">Guardar cambios</button>
    </template>
  </ModalCard>
</template>

<style scoped>
.equipo-editar {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 13px 16px;
  align-items: start;
}
/* El aviso y la observación cruzan las dos columnas: uno se lee de corrido, la otra es texto largo. */
.equipo-editar-aviso,
.equipo-editar-ancho {
  grid-column: 1 / -1;
}
.equipo-editar-buscar {
  align-self: end;
}
.equipo-editar label {
  display: grid;
  gap: 5px;
  font-size: 12.5px;
  font-weight: 600;
  color: var(--muted);
}
.equipo-editar-aviso {
  margin: 0;
  padding: 10px 12px;
  border-radius: 8px;
  background: #f2f6fa;
  border: 1px solid var(--border);
  font-size: 12px;
  line-height: 1.45;
  color: var(--muted);
  font-weight: 400;
}
.equipo-editar .field-error {
  font-weight: 500;
}
.equipo-editar .field-hint {
  font-weight: 400;
}
@media (max-width: 560px) {
  .equipo-editar {
    grid-template-columns: 1fr;
  }
}
</style>
