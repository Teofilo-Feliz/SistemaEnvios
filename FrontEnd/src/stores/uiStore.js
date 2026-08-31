import { ref, watch } from 'vue'
import { defineStore } from 'pinia'
import Swal from 'sweetalert2'

export const useUiStore = defineStore('ui', () => {
  const collapsed = ref(localStorage.getItem('sidebar_collapsed') === 'true')
  const mobileOpen = ref(false)
  const toasts = ref([])
  watch(collapsed, (value) => localStorage.setItem('sidebar_collapsed', String(value)))
  function toggleSidebar() { collapsed.value = !collapsed.value }
  function notify(message, type = 'success') {
    if (type === 'warning' || type === 'error') {
      Swal.fire({ icon: type === 'warning' ? 'warning' : 'error', title: type === 'warning' ? 'Advertencia' : 'No se pudo completar', text: message, confirmButtonText: 'Entendido', confirmButtonColor: '#3267d6' })
      return
    }
    const id = Date.now() + Math.random()
    toasts.value.push({ id, message, type })
    window.setTimeout(() => removeToast(id), 4500)
  }
  function removeToast(id) { toasts.value = toasts.value.filter((toast) => toast.id !== id) }
  return { collapsed, mobileOpen, toasts, toggleSidebar, notify, showToast: notify, removeToast }
})
