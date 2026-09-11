<script setup>
import { computed, onMounted, reactive, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { ArrowLeft, Save, X } from "lucide-vue-next";
import PageHeader from "@/components/common/PageHeader.vue";
import ShipmentSection from "@/components/shipments/ShipmentSection.vue";
import ShipmentGeneralSection from "@/components/shipments/ShipmentGeneralSection.vue";
import ShipmentTransportSection from "@/components/shipments/ShipmentTransportSection.vue";
import ShipmentEquipmentSection from "@/components/shipments/ShipmentEquipmentSection.vue";
import ShipmentEquipmentEditModal from "@/components/shipments/ShipmentEquipmentEditModal.vue";
import ShipmentSummary from "@/components/shipments/ShipmentSummary.vue";
import { catalogoService } from "@/services/catalogoService";
import { casoService } from "@/services/casoService";
import { filas } from "@/services/paginacion";
import { equipoService } from "@/services/equipoService";
import { envioService } from "@/services/envioService";
import { transporteService } from "@/services/transporteService";
import { isInternalTransport, isPrivateTransport } from "@/utils/transport";
import { TIPO_CEDULA, errorDocumento } from "@/utils/documento";
import { useUiStore } from "@/stores/uiStore";
import { useAuthStore } from "@/stores/authStore";
import { confirmAction, escapeHtml } from "@/utils/confirm";
import "@/assets/styles/shipment-create.css";

const router = useRouter(),
  route = useRoute(),
  ui = useUiStore(),
  auth = useAuthStore(),
  loading = ref(true),
  saving = ref(false),
  registering = ref(false),
  confirmOpen = ref(false),
  locations = ref([]),
  types = ref([]),
  transportTypes = ref([]),
  drivers = ref([]),
  registeredEquipment = ref([]),
  allEquipment = ref([]),
  errors = reactive({
    origin: "",
    destination: "",
    transportType: "",
    internalDriver: "",
    privateName: "",
    relationship: "",
    documentType: TIPO_CEDULA,
    privateId: "",
    vehiclePlate: "",
  });
const form = reactive({
  originId: "",
  destinationId: "",
  notes: "",
  transportTypeId: "",
  internalDriverId: "",
  privateName: "",
  relationship: "",
  privateId: "",
  vehiclePlate: "",
  equipment: [],
});
// Copia de los equipos tal como estaban al abrir la edición, para calcular la diferencia.
const equiposOriginales = ref([]);
// Transporte ya asignado al envío, si lo hay: define si al guardar se crea o se actualiza.
const transporteExistente = ref(null);
/**
 * El equipo que se está escribiendo para agregar. Solo eso: editar uno ya agregado va por
 * ShipmentEquipmentEditModal y no pasa por aquí, así que escribir un equipo nuevo y corregir otro
 * ya no se pisan.
 */
const item = reactive({
  envioEquipoId: null,
  existingId: "",
  typeId: "",
  brand: "",
  model: "",
  serial: "",
  assetCode: "",
  ticket: "",
  // Cuando el equipo arrastra un caso abierto el ticket viene dado y no se escribe.
  ticketHeredado: false,
  ticketFilial: "",
  casoFilialId: null,
  notes: "",
});

// Cubre tanto elegir del combo como escribir un serial que coincide con un equipo existente.
watch(
  () => item.existingId,
  (equipoId) => heredarTicket(equipoId ? Number(equipoId) : null),
);
const itemErrors = reactive({
  typeId: "",
  brand: "",
  model: "",
  serial: "",
  ticket: "",
});
const editing = computed(() => Boolean(route.params.id));
const technologyMode = computed(() => route.name === "technology-shipment-create");
const origin = computed(() => locations.value.find((x) => x.ubicacionId === Number(form.originId)));
const destination = computed(() =>
  locations.value.find((x) => x.ubicacionId === Number(form.destinationId)),
);
const selectedTransportType = computed(() =>
  transportTypes.value.find((x) => x.tipoTransporteId === Number(form.transportTypeId)),
);
const selectedDriver = computed(() =>
  drivers.value.find((x) => x.choferInternoId === Number(form.internalDriverId)),
);
const isTechnology = (location) =>
  location?.tipo === 2 || String(location?.tipo).toLowerCase() === "tecnologia";
const destinationLocations = computed(() =>
  origin.value
    ? isTechnology(origin.value)
      ? locations.value.filter((x) => !isTechnology(x))
      : locations.value.filter(isTechnology)
    : locations.value,
);
// Doble red: el servidor ya devuelve solo los del origen, y esto descarta cualquiera que
// quede en memoria de una selección anterior.
const availableEquipment = computed(() =>
  form.originId
    ? registeredEquipment.value.filter((x) => Number(x.ubicacionActualId) === Number(form.originId))
    : [],
);

/** Recarga el inventario disponible cuando cambia el origen del envío. */
async function cargarEquiposDelOrigen(ubicacionId) {
  if (!ubicacionId) {
    registeredEquipment.value = [];
    return;
  }
  try {
    registeredEquipment.value = filas(
      await equipoService.list({ pageSize: 100, ubicacionActualId: Number(ubicacionId) }),
    );
  } catch (error) {
    ui.notify(error.userMessage || "No fue posible cargar los equipos del origen.", "error");
  }
}
watch(() => form.originId, cargarEquiposDelOrigen);
const flowLabel = computed(() =>
  !origin.value || !destination.value
    ? ""
    : isTechnology(origin.value)
      ? "Tecnología → Filial"
      : "Filial → Tecnología",
);
function resetItem() {
  Object.keys(item).forEach((k) => (item[k] = ""));
  item.envioEquipoId = null;
  item.ticketHeredado = false;
  item.ticketFilial = "";
  item.casoFilialId = null;
}

// Filial a la que obliga el caso del primer equipo con caso abierto del envío. Solo aplica
// saliendo de Tecnología: en el otro sentido el destino es Tecnología y no hay nada que elegir.
const destinoFijado = computed(() => {
  if (!isTechnology(origin.value)) return "";
  const conCaso = form.equipment.find((x) => x.ticketHeredado && x.casoFilialId);
  return conCaso
    ? locations.value.find((x) => x.ubicacionId === conCaso.casoFilialId)?.nombre || ""
    : "";
});

/**
 * El ticket pertenece al caso, no al viaje: si el equipo trae un caso abierto, el número ya
 * está decidido y el usuario no lo escribe. Solo un equipo sin caso estrena ticket.
 */
async function heredarTicket(equipoId) {
  item.ticketHeredado = false;
  item.ticketFilial = "";
  item.casoFilialId = null;
  if (!equipoId) return;
  try {
    const { data: caso } = await casoService.byEquipment(equipoId);
    if (!caso) return;
    item.ticket = caso.numeroTicket;
    item.ticketHeredado = true;
    item.casoFilialId = caso.filialId;
    item.ticketFilial = locations.value.find((x) => x.ubicacionId === caso.filialId)?.nombre || "";
    // La devolución solo puede ir a la filial del caso: se selecciona sola y queda fijada.
    if (isTechnology(origin.value)) form.destinationId = caso.filialId;
  } catch {
    // Si la consulta falla el campo sigue editable: es preferible a bloquearlo sin dato.
  }
}
function fill(e) {
  item.existingId = e.equipoId;
  heredarTicket(e.equipoId);
  item.typeId = e.tipoEquipoId;
  item.brand = e.marca || "";
  item.model = e.modelo || "";
  item.serial = e.numeroSerie || "";
  item.assetCode = e.codigoActivo || "";
}
function lookup() {
  const s = item.serial?.trim().toLowerCase(),
    a = item.assetCode?.trim().toLowerCase(),
    m = registeredEquipment.value.find(
      (e) =>
        (s && e.numeroSerie?.toLowerCase() === s) || (a && e.codigoActivo?.toLowerCase() === a),
    );
  if (m) fill(m);
}
function ensure() {
  if (item.existingId) return Promise.resolve(Number(item.existingId));
  const d = registeredEquipment.value.find(
    (e) =>
      e.numeroSerie?.toLowerCase() === item.serial.toLowerCase() ||
      e.codigoActivo?.toLowerCase() === item.assetCode?.toLowerCase(),
  );
  if (d) {
    fill(d);
    return Promise.resolve(d.equipoId);
  }
  registering.value = true;
  return equipoService
    .create({
      tipoEquipoId: Number(item.typeId),
      ubicacionActualId: Number(form.originId),
      codigoActivo: item.assetCode || null,
      numeroSerie: item.serial,
      marca: item.brand,
      modelo: item.model,
      observaciones: item.notes || null,
    })
    .then((r) => {
      registeredEquipment.value.push({
        equipoId: r.data,
        // Sin la ubicación el equipo recién creado no pasaría el filtro por origen.
        ubicacionActualId: Number(form.originId),
        tipoEquipoId: Number(item.typeId),
        codigoActivo: item.assetCode,
        numeroSerie: item.serial,
        marca: item.brand,
        modelo: item.model,
      });
      return r.data;
    })
    .finally(() => (registering.value = false));
}
async function addEquipment() {
  itemErrors.typeId = item.typeId ? "" : "Tipo de equipo";
  itemErrors.brand = item.brand ? "" : "Marca";
  itemErrors.model = item.model ? "" : "Modelo";
  itemErrors.serial = item.serial ? "" : "Serial";
  itemErrors.ticket = !item.ticket
    ? "Ticket"
    : !/^\d+$/.test(item.ticket)
      ? "El ticket solo puede contener números"
      : "";
  if (!form.originId) {
    ui.notify("Selecciona el origen antes de agregar un equipo.", "warning");
    return;
  }
  // Cada equipo con caso abierto vuelve a SU filial, así que un envío no puede mezclar casos
  // de filiales distintas. Se avisa al agregarlo y no al guardar, cuando ya se perdió el rato.
  const otraFilial = form.equipment.find(
    (x) => x.casoFilialId && item.casoFilialId && x.casoFilialId !== item.casoFilialId,
  );
  if (otraFilial) {
    ui.notify(
      `Este equipo debe volver a ${item.ticketFilial || "su filial"} y el envío ya lleva equipos que van a ${otraFilial.ticketFilial || "otra filial"}. Haz un envío por filial.`,
      "warning",
    );
    return;
  }
  const duplicate =
    (item.existingId &&
      form.equipment.some((e) => Number(e.existingId) === Number(item.existingId))) ||
    (item.serial &&
      form.equipment.some((e) => e.serial?.toLowerCase() === item.serial.toLowerCase())) ||
    form.equipment.some((e) => e.ticket === item.ticket) ||
    (item.assetCode &&
      form.equipment.some((e) => e.assetCode?.toLowerCase() === item.assetCode.toLowerCase()));
  if (duplicate) {
    ui.notify("Este equipo ya fue agregado a este envío.", "warning");
    return;
  }
  if (Object.values(itemErrors).some(Boolean)) return;
  try {
    const id = await ensure();
    form.equipment.push({
      id: Date.now(),
      envioEquipoId: item.envioEquipoId || null,
      existingId: id,
      typeId: item.typeId,
      brand: item.brand,
      model: item.model,
      serial: item.serial,
      assetCode: item.assetCode,
      ticket: item.ticket,
      ticketHeredado: item.ticketHeredado,
      ticketFilial: item.ticketFilial,
      casoFilialId: item.casoFilialId,
      notes: item.notes,
      typeName:
        types.value.find((x) => x.tipoEquipoId === Number(item.typeId))?.nombre ||
        `Tipo #${item.typeId}`,
    });
    // El destino de un equipo con caso lo decide el caso, venga por donde venga: agregarlo
    // por primera vez o volver a agregarlo tras editarlo. Así la regla no depende del camino.
    if (item.casoFilialId && isTechnology(origin.value)) form.destinationId = item.casoFilialId;
    ui.notify("Equipo listo para asociar al envío.", "success");
    resetItem();
    Object.keys(itemErrors).forEach((k) => (itemErrors[k] = ""));
  } catch (e) {
    ui.notify(e.userMessage || "No fue posible registrar el equipo.", "error");
  }
}
function removeEquipment(id) {
  // Si el equipo que sale es el que fijaba el destino, el destino deja de estar decidido y hay
  // que limpiarlo. Sin esto el campo se desbloquea conservando la filial del caso anterior:
  // parece editable, viene relleno con un valor plausible, y el envío se va a otra filial.
  const fijadoAntes = form.equipment.find((x) => x.id === id)?.casoFilialId ?? null;
  form.equipment = form.equipment.filter((x) => x.id !== id);
  const sigueFijado = form.equipment.some((x) => x.ticketHeredado && x.casoFilialId);
  if (fijadoAntes && !sigueFijado && Number(form.destinationId) === Number(fijadoAntes)) {
    form.destinationId = "";
  }
}

// Al editar, el envío puede no tener transporte todavía (se crea) o tenerlo (se actualiza).
async function guardarTransporte() {
  if (technologyMode.value || !form.transportTypeId) return;
  const datos = {
    tipoTransporteId: Number(form.transportTypeId),
    choferInternoId: form.internalDriverId ? Number(form.internalDriverId) : null,
    nombreResponsable: form.privateName || null,
    parentesco: form.relationship || null,
    tipoDocumento: Number(form.documentType) || TIPO_CEDULA,
    documentoResponsable: form.privateId || null,
    placaVehiculo: form.vehiclePlate || null,
  };
  if (transporteExistente.value) {
    await transporteService.update(transporteExistente.value.transporteId, datos);
  } else {
    await transporteService.create({ envioId: Number(route.params.id), ...datos });
  }
}

// Guarda solo la diferencia contra lo que había: quitar, agregar y actualizar lo que cambió.
// Enviar todo de nuevo haría fallar el backend, que rechaza asociar dos veces el mismo equipo.
async function guardarEquipos() {
  const envioId = Number(route.params.id);
  const actuales = form.equipment;
  const previos = equiposOriginales.value;

  const quitados = previos.filter(
    (anterior) => !actuales.some((x) => x.envioEquipoId === anterior.envioEquipoId),
  );
  for (const equipo of quitados) await envioService.removeEquipment(equipo.envioEquipoId);

  for (const equipo of actuales) {
    if (!equipo.envioEquipoId) {
      await envioService.addEquipment({
        envioId,
        equipoId: Number(equipo.existingId),
        numeroTicket: equipo.ticket,
        observaciones: equipo.notes || "Equipo asociado al envío.",
      });
      continue;
    }
    const anterior = previos.find((x) => x.envioEquipoId === equipo.envioEquipoId);
    if (anterior && (anterior.ticket !== equipo.ticket || anterior.notes !== equipo.notes)) {
      await envioService.updateEquipment(equipo.envioEquipoId, {
        numeroTicket: equipo.ticket,
        observaciones: equipo.notes || "Equipo asociado al envío.",
      });
    }
  }
}
/** La fila que el lápiz abrió. El popup edita una copia, así que cancelar no la toca. */
const equipoEnEdicion = ref(null);

function editEquipment(equipo) {
  equipoEnEdicion.value = equipo;
}

/**
 * Guarda la edición de una fila ya agregada.
 *
 * Hay dos casos y distinguirlos importa. Si el serial no cambió, es la misma máquina con algún
 * dato corregido y se actualiza su ficha del inventario. Si el serial cambió, la fila pasa a ser
 * de OTRA máquina —el usuario cambió el ticket y trajo el equipo del ticket nuevo—, y entonces no
 * se puede tocar la ficha de la anterior: se reescribiría un equipo con los datos de otro.
 *
 * En ese segundo caso la fila se reapunta al equipo nuevo y se le quita el envioEquipoId, con lo
 * que guardarEquipos() quita la asociación vieja y crea la nueva. Es la única forma de cambiar de
 * equipo, porque el endpoint de actualizar solo acepta ticket y observaciones.
 */
async function guardarEdicionEquipo(actualizado) {
  const anterior = form.equipment.find((fila) => fila.id === actualizado.id);
  if (!anterior) return;

  const mismoSerial =
    String(anterior.serial ?? "").toLowerCase() === String(actualizado.serial ?? "").toLowerCase();

  try {
    const fila = mismoSerial
      ? await actualizarEquipoEnSitio(anterior, actualizado)
      : await reapuntarAOtroEquipo(actualizado);

    form.equipment = form.equipment.map((x) => (x.id === fila.id ? fila : x));
    equipoEnEdicion.value = null;
    ui.notify("Equipo actualizado.", "success");
  } catch (error) {
    ui.notify(error.userMessage || "No fue posible actualizar el equipo.", "error");
  }
}

/** Misma máquina: marca, modelo o tipo corregidos. Se persiste en su ficha. */
async function actualizarEquipoEnSitio(anterior, actualizado) {
  const campos = ["typeId", "brand", "model", "serial", "assetCode"];
  const cambio = campos.some((c) => String(anterior[c] ?? "") !== String(actualizado[c] ?? ""));
  if (!cambio || !actualizado.existingId) return actualizado;

  const enInventario = registeredEquipment.value.find(
    (x) => Number(x.equipoId) === Number(actualizado.existingId),
  );

  await equipoService.update(Number(actualizado.existingId), {
    equipoId: Number(actualizado.existingId),
    tipoEquipoId: Number(actualizado.typeId),
    // La edición no mueve el equipo: conserva su ubicación actual en vez del origen del
    // formulario, que no tiene por qué ser la misma.
    ubicacionActualId: Number(enInventario?.ubicacionActualId ?? form.originId),
    codigoActivo: actualizado.assetCode || null,
    numeroSerie: actualizado.serial,
    marca: actualizado.brand,
    modelo: actualizado.model,
    observaciones: actualizado.notes || null,
  });

  // El inventario en memoria alimenta el combo y a ensure(): si no se refresca, agregar otro
  // equipo con este serial lo reconocería con los datos viejos.
  if (enInventario) {
    Object.assign(enInventario, {
      tipoEquipoId: Number(actualizado.typeId),
      codigoActivo: actualizado.assetCode,
      numeroSerie: actualizado.serial,
      marca: actualizado.brand,
      modelo: actualizado.model,
    });
  }

  return actualizado;
}

/** Otra máquina: se busca en el inventario y, si no está, se registra. */
async function reapuntarAOtroEquipo(actualizado) {
  const existente = registeredEquipment.value.find(
    (x) =>
      (actualizado.serial && x.numeroSerie?.toLowerCase() === actualizado.serial.toLowerCase()) ||
      (actualizado.assetCode &&
        x.codigoActivo?.toLowerCase() === actualizado.assetCode.toLowerCase()),
  );

  let equipoId = existente?.equipoId;
  if (!equipoId) {
    const { data } = await equipoService.create({
      tipoEquipoId: Number(actualizado.typeId),
      ubicacionActualId: Number(form.originId),
      codigoActivo: actualizado.assetCode || null,
      numeroSerie: actualizado.serial,
      marca: actualizado.brand,
      modelo: actualizado.model,
      observaciones: actualizado.notes || null,
    });
    equipoId = data;
    registeredEquipment.value.push({
      equipoId,
      ubicacionActualId: Number(form.originId),
      tipoEquipoId: Number(actualizado.typeId),
      codigoActivo: actualizado.assetCode,
      numeroSerie: actualizado.serial,
      marca: actualizado.brand,
      modelo: actualizado.model,
    });
  }

  // Sin envioEquipoId, guardarEquipos() ve la asociación vieja como quitada y esta como nueva.
  return { ...actualizado, existingId: equipoId, envioEquipoId: null };
}

async function load() {
  try {
    // El inventario no se descarga al abrir: se pide el del origen cuando ya se conoce.
    const [l, t, tt, d] = await Promise.all([
      catalogoService.allLocations(),
      catalogoService.allTypes(),
      catalogoService.allTransportTypes(),
      catalogoService.allInternalDrivers(),
    ]);
    locations.value = l;
    types.value = t;
    transportTypes.value = tt;
    drivers.value = d;
    if (editing.value) {
      const [r, asociaciones, estados] = await Promise.all([
        envioService.get(route.params.id),
        envioService.equipment(route.params.id, { pageSize: 100 }),
        catalogoService.allStates(),
      ]);

      // Una vez entregado a Transportación el envío ya no se toca. Sin esta comprobación se
      // podía volver al formulario por URL o con el botón atrás del navegador, editarlo y
      // recién enterarse del rechazo al guardar.
      const codigo = estados.find((x) => x.estadoEnvioId === r.data.estadoEnvioId)?.codigo;
      if (!["EN_FILIAL", "PREPARACION_TECNOLOGIA"].includes(codigo)) {
        ui.notify("El envío ya salió y no admite cambios.", "warning");
        router.replace(`/envios/${route.params.id}`);
        return;
      }

      form.originId = r.data.ubicacionOrigenId;
      form.destinationId = r.data.ubicacionDestinoId;
      form.notes = r.data.observaciones || "";
      await cargarEquiposDelOrigen(r.data.ubicacionOrigenId);

      // Los equipos ya asociados se cargan al formulario. Se guarda envioEquipoId para poder
      // distinguir después qué se agregó, qué cambió y qué se quitó.
      form.equipment = filas(asociaciones).map((asociacion) => {
        const equipo =
          registeredEquipment.value.find((x) => x.equipoId === asociacion.equipoId) || {};
        return {
          id: `existente-${asociacion.envioEquipoId}`,
          envioEquipoId: asociacion.envioEquipoId,
          existingId: asociacion.equipoId,
          typeId: equipo.tipoEquipoId ?? "",
          brand: equipo.marca || "",
          model: equipo.modelo || "",
          serial: equipo.numeroSerie || "",
          assetCode: equipo.codigoActivo || "",
          ticket: asociacion.numeroTicket || "",
          // Si el movimiento cuelga de una apertura, su ticket viene del caso: ni se valida
          // ni se edita, igual que al crearlo.
          ticketHeredado: Boolean(asociacion.envioEquipoOrigenId),
          ticketFilial: "",
          notes: asociacion.observaciones || "",
          typeName:
            types.value.find((x) => x.tipoEquipoId === equipo.tipoEquipoId)?.nombre ||
            `Tipo #${equipo.tipoEquipoId}`,
        };
      });
      equiposOriginales.value = form.equipment.map((x) => ({ ...x }));

      // El transporte también se puede corregir mientras el envío no salga: un chofer mal
      // elegido al crear es justo lo que hay que poder arreglar.
      try {
        const { data: transporte } = await transporteService.getByEnvio(route.params.id);
        if (transporte) {
          transporteExistente.value = transporte;
          form.transportTypeId = transporte.tipoTransporteId ?? "";
          form.internalDriverId = transporte.choferInternoId ?? "";
          form.privateName = transporte.nombreResponsable || "";
          form.relationship = transporte.parentesco || "";
          form.documentType = Number(transporte.tipoDocumento) || TIPO_CEDULA;
          form.privateId = transporte.documentoResponsable || "";
          form.vehiclePlate = transporte.placaVehiculo || "";
        }
      } catch {
        // Todavía sin transporte asignado: se registra al guardar.
        transporteExistente.value = null;
      }
    } else if (technologyMode.value) {
      const headquarters =
        locations.value.find(
          (x) =>
            isTechnology(x) &&
            (/centro\s*sede/i.test(x.nombre || "") || /tecnolog/i.test(x.nombre || "")),
        ) || locations.value.find(isTechnology);
      if (headquarters) form.originId = headquarters.ubicacionId;
    } else {
      // El token trae la filial del usuario, así que el envío nace con su filial como origen
      // y Tecnología como destino. Solo el perfil global elige, porque no está atado a una.
      const tecnologia = locations.value.find(isTechnology);
      const propia = auth.perfil?.ubicacionId
        ? locations.value.find((x) => x.ubicacionId === auth.perfil.ubicacionId)
        : null;
      if (propia) form.originId = propia.ubicacionId;
      if (tecnologia) form.destinationId = tecnologia.ubicacionId;
    }
  } catch (e) {
    ui.notify(e.userMessage || "No fue posible cargar los catálogos.", "error");
  } finally {
    loading.value = false;
  }
}
function validate() {
  errors.origin = !form.originId ? "Selecciona el origen." : "";
  errors.destination = !form.destinationId
    ? "Selecciona el destino."
    : form.originId === form.destinationId
      ? "El destino debe ser diferente al origen."
      : "";
  const selected = transportTypes.value.find(
    (x) => x.tipoTransporteId === Number(form.transportTypeId),
  );
  if (!editing.value && !technologyMode.value) {
    errors.transportType = !selected
      ? "Selecciona el tipo de transporte."
      : isPrivateTransport(selected.estrategia) && isTechnology(origin.value)
        ? "El transporte privado directo solo aplica a envíos hacia Tecnología."
        : "";
    errors.internalDriver =
      isInternalTransport(selected?.estrategia) && !form.internalDriverId
        ? "Selecciona el chofer."
        : "";
    errors.privateName =
      isPrivateTransport(selected?.estrategia) && !form.privateName ? "Indica el nombre." : "";
    errors.relationship =
      isPrivateTransport(selected?.estrategia) && !form.relationship ? "Indica el parentesco." : "";
    // El formato y el mensaje dependen del tipo de documento: la cédula no admite letras y son
    // once dígitos exactos; el pasaporte sí las admite. La regla vive en utils/documento.js, que
    // espeja FormatosDocumento.cs del backend.
    errors.privateId = isPrivateTransport(selected?.estrategia)
      ? errorDocumento(form.documentType, form.privateId)
      : "";
    errors.vehiclePlate =
      isPrivateTransport(selected?.estrategia) && !form.vehiclePlate ? "Indica la placa." : "";
  }
  // También al editar: un envío sin equipos no se puede despachar, así que guardarlo así
  // lo dejaría en un callejón sin salida.
  if (!form.equipment.length) ui.notify("Agrega al menos un equipo antes de guardar.", "warning");
  return !Object.values(errors).some(Boolean) && Boolean(form.equipment.length);
}
async function submit() {
  if (!validate()) return;
  // Solo se comprueban los tickets que se estrenan. Un ticket heredado ya está usado por la
  // apertura de su caso a propósito, así que preguntar si está libre siempre diría que no.
  // Al editar hay que excluir la propia asociación: si no, el ticket que ya tiene el equipo
  // en este mismo envío se reportaría como ocupado y no se podría guardar nada.
  const porVerificar = form.equipment.filter((equipment) => !equipment.ticketHeredado);
  const availability = await Promise.all(
    porVerificar.map((equipment) =>
      envioService.ticketAvailable(
        equipment.ticket,
        equipment.envioEquipoId ? { excluirEnvioEquipoId: equipment.envioEquipoId } : {},
      ),
    ),
  );
  const duplicateIndex = availability.findIndex((response) => response.data !== true);
  if (duplicateIndex >= 0) {
    ui.notify(
      `El ticket ${porVerificar[duplicateIndex].ticket} ya está asociado a otro equipo o envío.`,
      "error",
    );
    return;
  }
  const transportLabel = selectedTransportType.value?.nombre || "Sin indicar";
  const driverLabel = isInternalTransport(selectedTransportType.value?.estrategia)
    ? selectedDriver.value?.nombreCompleto || "Sin indicar"
    : `${form.privateName || "Sin indicar"} · ${form.vehiclePlate || "Sin placa"}`;
  const equipmentHtml = form.equipment.length
    ? form.equipment
        .map(
          (equipment, index) =>
            `<div style="border:1px solid #e1e6ee;border-radius:6px;padding:9px;margin:7px 0"><strong>${index + 1}. ${escapeHtml(equipment.typeName)}</strong><br><small>${escapeHtml(equipment.brand)} ${escapeHtml(equipment.model)} · Serial: ${escapeHtml(equipment.serial)} · Activo: ${escapeHtml(equipment.assetCode)} · Ticket: ${escapeHtml(equipment.ticket)}</small></div>`,
        )
        .join("")
    : "<p>Los equipos asociados se conservarán sin cambios.</p>";
  const accepted = await confirmAction({
    title: editing.value ? "Confirmar actualización" : "Confirmar nuevo envío",
    html: `<div style="text-align:left"><p><strong>Origen:</strong> ${escapeHtml(origin.value?.nombre)}</p><p><strong>Destino:</strong> ${escapeHtml(destination.value?.nombre)}</p><p><strong>Transporte:</strong> ${escapeHtml(transportLabel)}</p>${editing.value ? "" : `<p><strong>Responsable/chofer:</strong> ${escapeHtml(driverLabel)}</p>`}<p><strong>Observaciones:</strong> ${escapeHtml(form.notes || "Sin observaciones")}</p><hr><strong>Equipos (${form.equipment.length})</strong><div style="max-height:280px;overflow:auto">${equipmentHtml}</div></div>`,
    confirmText: editing.value ? "Guardar cambios" : "Crear envío",
  });
  if (accepted) await save();
}
async function save() {
  confirmOpen.value = false;
  saving.value = true;
  try {
    const r = editing.value
      ? await (async () => {
          const respuesta = await envioService.update(route.params.id, {
            ubicacionOrigenId: Number(form.originId),
            ubicacionDestinoId: Number(form.destinationId),
            observaciones: form.notes || null,
          });
          await guardarEquipos();
          await guardarTransporte();
          return respuesta;
        })()
      : await envioService.createWithEquipment({
          ubicacionOrigenId: Number(form.originId),
          ubicacionDestinoId: Number(form.destinationId),
          observaciones: form.notes || null,
          equipos: form.equipment.map((e) => ({
            equipoId: Number(e.existingId),
            numeroTicket: e.ticket,
            observaciones: e.notes || "Equipo asociado al envío.",
          })),
        });
    const id = editing.value ? route.params.id : r.data.envioId;
    if (!editing.value && !technologyMode.value) {
      await transporteService.create({
        envioId: Number(id),
        tipoTransporteId: Number(form.transportTypeId),
        choferInternoId: form.internalDriverId ? Number(form.internalDriverId) : null,
        nombreResponsable: form.privateName || null,
        parentesco: form.relationship || null,
        tipoDocumento: Number(form.documentType) || TIPO_CEDULA,
        documentoResponsable: form.privateId || null,
        placaVehiculo: form.vehiclePlate || null,
      });
    }
    ui.notify(editing.value ? "Envío actualizado correctamente." : "Envío creado correctamente.");
    router.push(`/envios/${id}`);
  } catch (e) {
    ui.notify(e.userMessage || "No fue posible guardar el envío.", "error");
  } finally {
    saving.value = false;
  }
}
watch(
  () => form.originId,
  () => {
    if (
      form.destinationId &&
      !destinationLocations.value.some((x) => x.ubicacionId === Number(form.destinationId))
    )
      form.destinationId = "";
    if (!editing.value && origin.value && !isTechnology(origin.value)) {
      const technologyHeadquarters = locations.value.find(
        (location) =>
          isTechnology(location) &&
          (/centro\s*sede/i.test(location.nombre || "") || /tecnolog/i.test(location.nombre || "")),
      );
      if (technologyHeadquarters) form.destinationId = technologyHeadquarters.ubicacionId;
    }
  },
);
watch(() => [item.serial, item.assetCode], lookup);
watch(
  () => item.existingId,
  (id) => {
    const e = registeredEquipment.value.find((x) => x.equipoId === Number(id));
    if (e) fill(e);
  },
);
onMounted(load);
</script>
<template>
  <div class="shipment-create-page">
    <PageHeader
      :title="editing ? 'Editar envío' : 'Crear envío'"
      subtitle="Registra la información logística y los equipos asociados"
      ><button class="btn btn-secondary" type="button" @click="router.push('/envios')">
        <ArrowLeft :size="16" /> Cancelar</button
      ><button class="btn btn-primary" type="button" :disabled="saving || loading" @click="submit">
        <Save :size="16" /> {{ saving ? "Guardando…" : "Guardar envío" }}
      </button></PageHeader
    >
    <div v-if="loading" class="loading-state">Cargando catálogos…</div>
    <form v-else class="shipment-create-layout" @submit.prevent="submit">
      <main class="shipment-create-main">
        <ShipmentSection title="Información general"
          ><ShipmentGeneralSection
            :form="form"
            :errors="errors"
            :locations="locations"
            :destination-locations="destinationLocations"
            :flow-label="flowLabel"
            :destino-fijado="destinoFijado"
            :readonly-origin="technologyMode" /></ShipmentSection
        ><ShipmentSection v-if="!technologyMode" title="Transportación"
          ><ShipmentTransportSection
            :form="form"
            :transport-types="transportTypes"
            :drivers="drivers"
            :errors="errors" /></ShipmentSection
        ><ShipmentSection title="Equipos"
          ><ShipmentEquipmentSection
            :item="item"
            :equipments="form.equipment"
            :types="types"
            :registered-equipment="availableEquipment"
            :registering="registering"
            @add="addEquipment"
            @remove="removeEquipment"
            @edit="editEquipment"
            @new="resetItem"
        /></ShipmentSection>
      </main>
      <ShipmentSummary
        :form="form"
        :origin-name="origin?.nombre"
        :destination-name="destination?.nombre"
        :flow-label="flowLabel"
      />
    </form>
    <ShipmentEquipmentEditModal
      :open="Boolean(equipoEnEdicion)"
      :equipment="equipoEnEdicion"
      :equipments="form.equipment"
      :types="types"
      :puede-editar-equipo="auth.can('equipos.gestionar')"
      @close="equipoEnEdicion = null"
      @save="guardarEdicionEquipo"
    />
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
        <h2>Confirmar guardado</h2>
        <p>¿Deseas guardar este envío con la información indicada?</p>
        <dl class="confirm-summary">
          <div>
            <dt>Origen</dt>
            <dd>{{ origin?.nombre || "Sin seleccionar" }}</dd>
          </div>
          <div>
            <dt>Destino</dt>
            <dd>{{ destination?.nombre || "Sin seleccionar" }}</dd>
          </div>
          <div>
            <dt>Flujo operativo</dt>
            <dd>{{ flowLabel || "Por definir" }}</dd>
          </div>
          <div>
            <dt>Tipo de transporte</dt>
            <dd>{{ selectedTransportType?.nombre || "No indicado" }}</dd>
          </div>
          <div>
            <dt>Chofer / responsable</dt>
            <dd>{{ selectedDriver?.nombreCompleto || form.privateName || "No indicado" }}</dd>
          </div>
          <div>
            <dt>Equipos</dt>
            <dd>{{ form.equipment.length }}</dd>
          </div>
          <div class="confirm-observations">
            <dt>Observaciones</dt>
            <dd>{{ form.notes || "Sin observaciones" }}</dd>
          </div>
        </dl>
        <div v-if="form.equipment.length" class="confirm-equipment-list">
          <h3>Equipos a asociar</h3>
          <article v-for="(equipment, index) in form.equipment" :key="equipment.id">
            <strong>{{ index + 1 }}. {{ equipment.typeName }}</strong
            ><span>Marca / modelo: {{ equipment.brand }} {{ equipment.model }}</span
            ><span
              >Serial: {{ equipment.serial || "No indicado" }} · Código:
              {{ equipment.assetCode || "No indicado" }}</span
            ><span>Ticket: {{ equipment.ticket }}</span
            ><span v-if="equipment.notes">Observación: {{ equipment.notes }}</span>
          </article>
        </div>
        <div class="confirm-actions">
          <button class="btn btn-secondary" type="button" @click="confirmOpen = false">
            Cancelar</button
          ><button class="btn btn-primary" type="button" :disabled="saving" @click="save">
            <Save :size="15" /> Confirmar y guardar
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
