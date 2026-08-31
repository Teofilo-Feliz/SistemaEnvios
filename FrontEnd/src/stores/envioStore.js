import { reactive } from 'vue'
import { defineStore } from 'pinia'

export const useEnvioStore = defineStore('envios', () => {
  const filters = reactive({ search: '', status: '', origin: '', destination: '', from: '', to: '' })
  function setFilters(values) { Object.assign(filters, values) }
  function clearFilters() { Object.keys(filters).forEach((key) => { filters[key] = '' }) }
  return { filters, setFilters, clearFilters }
})
