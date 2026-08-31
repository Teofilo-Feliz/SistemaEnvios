import { computed, ref } from 'vue'
import { dashboardService } from '@/services/dashboardService'

export function useDashboard() {
  const data = ref(null); const loading = ref(false); const error = ref('')
  const summary = computed(() => data.value?.summary || {})
  async function load() { loading.value = true; error.value = ''; try { data.value = (await dashboardService.summary()).data } catch (exception) { error.value = exception.userMessage || 'No fue posible cargar el dashboard.' } finally { loading.value = false } }
  return { data, summary, loading, error, load }
}
