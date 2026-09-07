import { computed, ref } from "vue";
import { dashboardService } from "@/services/dashboardService";

export function useDashboard() {
  // Arranca cargando: la vista pide los datos en onMounted, así que entre el primer pintado y
  // esa llamada el widget mostraba el slot con datos vacíos. Los cuatro gráficos se montaban
  // para desmontarse un tick después, y ApexCharts resolvía su render contra un elemento ya
  // sacado del DOM ("Element not found", una rechazada sin capturar por gráfico y por carga).
  const data = ref(null);
  const loading = ref(true);
  const error = ref("");
  const summary = computed(() => data.value?.summary || {});
  async function load() {
    loading.value = true;
    error.value = "";
    try {
      data.value = (await dashboardService.summary()).data;
    } catch (exception) {
      error.value = exception.userMessage || "No fue posible cargar el dashboard.";
    } finally {
      loading.value = false;
    }
  }
  return { data, summary, loading, error, load };
}
