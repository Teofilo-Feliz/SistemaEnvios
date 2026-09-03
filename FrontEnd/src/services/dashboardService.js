import api from './api'

export const dashboardService = {
  // Totales de Transportación agregados en la base: la pantalla dibuja números, no envíos.
  transportacion: () => api.get('/dashboard/transportacion'),
  summary: (months = 12) => api.get('/dashboard/resumen', { params: { meses: months } }),
}
