<script setup>
import { reactive } from 'vue'
import { Plus } from 'lucide-vue-next'
import ShipmentEquipmentTable from './ShipmentEquipmentTable.vue'
import { envioService } from '@/services/envioService'
import { glpiService } from '@/services/glpiService'

const props = defineProps({ item: Object, equipments: Array, types: Array, registeredEquipment: Array, registering: Boolean })
const emit = defineEmits(['add', 'remove', 'edit', 'new'])
const errors = reactive({ typeId: '', brand: '', model: '', serial: '', ticket: '' })

async function validateAndAdd() {
  errors.typeId = props.item.typeId ? '' : 'El tipo de equipo es obligatorio.'
  errors.brand = props.item.brand ? '' : 'La marca es obligatoria.'
  errors.model = props.item.model ? '' : 'El modelo es obligatorio.'
  errors.serial = props.item.serial ? '' : 'El número de serie es obligatorio.'
  // Un ticket heredado no se valida ni se comprueba contra los ya usados: repetirlo es
  // exactamente lo que se busca, porque pertenece al caso y no a este viaje.
  errors.ticket = props.item.ticketHeredado
    ? ''
    : !props.item.ticket
      ? 'El número de ticket es obligatorio.'
      : !/^\d+$/.test(props.item.ticket)
        ? 'El número de ticket solo permite caracteres numéricos.'
        : props.item.ticket.length < 3
          ? 'El número de ticket debe tener al menos 3 dígitos.'
          // "0" o "000" son dígitos y del largo pedido, pero en la mesa de ayuda no existe el
          // caso cero. Se mira dígito a dígito porque el campo admite hasta 50 y ese número
          // desborda cualquier entero. Los ceros a la izquierda sí valen: "007" es el ticket 7.
          : !/[1-9]/.test(props.item.ticket)
            ? 'El número de ticket debe ser mayor que cero.'
            : props.equipments.some((equipment) => equipment.ticket === props.item.ticket && equipment.id !== props.item.id)
              ? 'Este número de ticket ya fue agregado.'
              : ''
  if (Object.values(errors).some(Boolean)) return
  if (!props.item.ticketHeredado) {
    try {
      const response = await envioService.ticketAvailable(props.item.ticket)
      if (response.data !== true) {
        errors.ticket = 'Este número de ticket ya está registrado en otro equipo o envío.'
        return
      }
    } catch (error) {
      errors.ticket = error.userMessage || 'No se pudo validar el número de ticket.'
      return
    }

    // Que el ticket exista en la mesa de ayuda es distinto de que esté libre: lo anterior mira
    // nuestra base, esto mira GLPI. El servidor lo vuelve a comprobar al guardar; esto es para
    // que el usuario se entere aquí y no después de llenar el envío completo.
    try {
      const { data } = await glpiService.ticketExiste(props.item.ticket)
      if (data?.existe === false) {
        errors.ticket = `El ticket ${props.item.ticket} no existe en la mesa de ayuda.`
        return
      }
    } catch {
      // GLPI caído no bloquea: el servidor aplica la misma política y deja pasar registrando el
      // aviso. Frenar aquí dejaría a todas las filiales sin poder crear envíos.
    }
  }
  emit('add')
}
</script>

<template>
  <div class="equipment-form compact-equipment-form">
    <label class="equipment-existing">Equipo existente (opcional)<select v-model="item.existingId" class="form-control"><option value="">Registrar equipo nuevo</option><option v-for="equipment in registeredEquipment" :key="equipment.equipoId" :value="equipment.equipoId">{{ equipment.numeroSerie || equipment.codigoActivo || `Equipo #${equipment.equipoId}` }} · {{ equipment.marca }} {{ equipment.modelo }}</option></select></label>
    <button v-if="item.existingId" class="btn btn-ghost equipment-new-button" type="button" @click="emit('new')">+ Nuevo equipo</button>
    <label>Tipo *<select v-model="item.typeId" class="form-control"><option value="">Seleccionar</option><option v-for="type in types" :key="type.tipoEquipoId" :value="type.tipoEquipoId">{{ type.nombre }}</option></select><small v-if="errors.typeId" class="field-error">{{ errors.typeId }}</small></label>
    <label>Marca *<input v-model.trim="item.brand" class="form-control" placeholder="Marca" /><small v-if="errors.brand" class="field-error">{{ errors.brand }}</small></label>
    <label>Modelo *<input v-model.trim="item.model" class="form-control" placeholder="Modelo" /><small v-if="errors.model" class="field-error">{{ errors.model }}</small></label>
    <label>Serial *<input v-model.trim="item.serial" class="form-control" placeholder="Serial" /><small v-if="errors.serial" class="field-error">{{ errors.serial }}</small></label>
    <label>Código activo<input v-model.trim="item.assetCode" class="form-control" placeholder="Opcional" /></label>
    <label>Ticket *<input v-model.trim="item.ticket" class="form-control" :readonly="item.ticketHeredado" :class="{ 'input-heredado': item.ticketHeredado }" inputmode="numeric" pattern="[0-9]*" placeholder="Solo números" @input="item.ticket=item.ticket.replace(/\D/g, ''); errors.ticket=''" /><small v-if="item.ticketHeredado" class="field-hint">Heredado del caso abierto{{ item.ticketFilial ? ` de ${item.ticketFilial}` : '' }}. No se modifica.</small><small v-else-if="errors.ticket" class="field-error">{{ errors.ticket }}</small></label>
    <label class="equipment-notes">Observación<input v-model.trim="item.notes" class="form-control" placeholder="Observación" /></label>
    <button class="btn btn-secondary equipment-add-button" type="button" :disabled="registering" @click="validateAndAdd"><Plus :size="15" /> {{ registering ? 'Registrando…' : 'Agregar' }}</button>
  </div>
  <ShipmentEquipmentTable :equipments="equipments" @remove="emit('remove', $event)" @edit="emit('edit', $event)" />
</template>
