import api from './api'

// El perfil lo decide el backend. El frontend no puede deducirlo de los permisos: un
// administrador de filial y Transportacion comparten transportes.gestionar.
export const perfilService = {
  get: () => api.get('/perfil'),
}
