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
import { envioService } from "@/services/envioService";
import { glpiService } from "@/services/glpiService";
import { filtrarSoloDigitos } from "@/utils/documento";

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
const validando = ref(false);
const ticketOriginal = ref("");

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
  const limpio = filtrarSoloDigitos(evento.target.value);
  borrador.assetCode = limpio;
  evento.target.value = limpio;
  errors.assetCode = "";
}

function alEscribirTicket(evento) {
  const limpio = filtrarSoloDigitos(evento.target.value);
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

/**
 * Las comprobaciones contra el servidor, solo si el ticket cambió: volver a preguntar por el que
 * ya tenía daría "ya está registrado", porque lo está, en este mismo equipo.
 *
 * Son dos preguntas distintas. Que el ticket esté libre lo dice nuestra base; que exista lo dice
 * la mesa de ayuda. Si GLPI está caído no se bloquea: el servidor aplica la misma política al
 * guardar, y frenar aquí dejaría a las filiales sin poder editar.
 */
async function validarTicketNuevo() {
  if (heredado.value || borrador.ticket === ticketOriginal.value) return true;

  try {
    const { data } = await envioService.ticketAvailable(borrador.ticket);
    if (data !== true) {
      errors.ticket = "Este número de ticket ya está registrado en otro equipo o envío.";
      return false;
    }
  } catch (error) {
    errors.ticket = error.userMessage || "No se pudo validar el número de ticket.";
    return false;
  }

  try {
    const { data } = await glpiService.ticketExiste(borrador.ticket);
    if (data?.existe === false) {
      errors.ticket = `El ticket ${borrador.ticket} no existe en la mesa de ayuda.`;
      return false;
    }
  } catch {
    /* GLPI caído no bloquea. */
  }

  return true;
}

async function guardar() {
  if (!validarEnLocal()) return;
  validando.value = true;
  try {
    if (!(await validarTicketNuevo())) return;
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
  } finally {
    validando.value = false;
  }
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
          :disabled="!puedeEditarEquipo"
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
          :readonly="!puedeEditarEquipo"
          :class="{ 'input-heredado': !puedeEditarEquipo }"
          placeholder="Marca"
          @input="errors.brand = ''"
        /><small v-if="errors.brand" class="field-error">{{ errors.brand }}</small></label
      >

      <label
        >Modelo *<input
          v-model.trim="borrador.model"
          class="form-control"
          :readonly="!puedeEditarEquipo"
          :class="{ 'input-heredado': !puedeEditarEquipo }"
          placeholder="Modelo"
          @input="errors.model = ''"
        /><small v-if="errors.model" class="field-error">{{ errors.model }}</small></label
      >

      <label
        >Serial *<input
          v-model.trim="borrador.serial"
          class="form-control"
          :readonly="!puedeEditarEquipo"
          :class="{ 'input-heredado': !puedeEditarEquipo }"
          placeholder="Serial"
          @input="errors.serial = ''"
        /><small v-if="errors.serial" class="field-error">{{ errors.serial }}</small></label
      >

      <label
        >Código activo<input
          :value="borrador.assetCode"
          class="form-control"
          :readonly="!puedeEditarEquipo"
          :class="{ 'input-heredado': !puedeEditarEquipo }"
          inputmode="numeric"
          placeholder="Opcional, solo números"
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
        ><small v-else-if="errors.ticket" class="field-error">{{ errors.ticket }}</small></label
      >

      <label class="equipo-editar-ancho"
        >Observación<input
          v-model.trim="borrador.notes"
          class="form-control"
          placeholder="Observación"
      /></label>
    </div>

    <template #footer>
      <button class="btn btn-ghost" type="button" :disabled="validando" @click="emit('close')">
        Cancelar
      </button>
      <button class="btn btn-primary" type="button" :disabled="validando" @click="guardar">
        {{ validando ? "Validando…" : "Guardar cambios" }}
      </button>
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
