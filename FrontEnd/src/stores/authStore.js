import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

export const useAuthStore = defineStore('auth', () => {
  const user = ref({ name: 'María Rodríguez', role: 'Administradora logística', initials: 'MR' })
  const permissions = ref(['envios.consultar', 'envios.crear', 'envios.editar', 'envios.despachar', 'transportes.gestionar', 'transportes.confirmar', 'recepciones.gestionar', 'incidencias.gestionar', 'equipos.gestionar', 'catalogos.administrar'])
  const isAuthenticated = computed(() => Boolean(user.value))
  const aliases = {
    canCreateShipment: 'envios.crear',
    canReceiveShipment: 'recepciones.gestionar',
    canConfirmTransport: 'transportes.confirmar',
    canReviewEquipment: 'recepciones.gestionar',
    canManageCatalogs: 'catalogos.administrar',
    canViewAudit: 'catalogos.administrar',
  }
  const can = (permission) => !permission || permissions.value.includes(permission) || permissions.value.includes(aliases[permission])
  function setIdentity(identity) {
    user.value = identity.user
    permissions.value = identity.permissions || []
    localStorage.setItem('auth_permissions', JSON.stringify(permissions.value))
  }
  function logout() {
    localStorage.removeItem('auth_token')
    localStorage.removeItem('auth_permissions')
    user.value = null
    permissions.value = []
  }
  return { user, permissions, isAuthenticated, can, setIdentity, logout }
})
