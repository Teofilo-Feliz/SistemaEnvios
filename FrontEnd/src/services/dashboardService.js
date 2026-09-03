import api from './api'

export const dashboardService = {
  // Totales de Transportación agregados en la base: la pantalla dibuja números, no envíos.
  transportacion: () => api.get('/dashboard/transportacion'),
  // Tablero de la filial del usuario: el servidor lo acota a sus propios envíos y equipos.
  filial: () => api.get('/dashboard/filial'),
  summary: (months = 12) => api.get('/dashboard/resumen', { params: { meses: months } }),
}
