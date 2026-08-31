import api from './api'

export const authService = {
  demoLogin: (credentials) => api.post('/auth/demo-login', credentials),
}
