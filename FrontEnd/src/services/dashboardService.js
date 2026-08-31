import api from './api'

export const dashboardService = {
  summary: (months = 12) => api.get('/dashboard/resumen', { params: { meses: months } }),
}
