<script setup>
import { computed } from 'vue'
import VueApexCharts from 'vue3-apexcharts'

const props = defineProps({
  type: { type: String, default: 'line' },
  data: { type: Object, default: () => ({ labels: [], datasets: [] }) },
})

const chartType = computed(() => props.type === 'doughnut' ? 'donut' : props.type === 'bar' ? 'bar' : 'line')
const series = computed(() => {
  const datasets = props.data?.datasets || []
  if (chartType.value === 'donut') return datasets[0]?.data || []
  return datasets.map((dataset) => ({ name: dataset.label, data: dataset.data || [] }))
})

const options = computed(() => ({
  chart: {
    type: chartType.value,
    toolbar: { show: false },
    fontFamily: 'Inter, system-ui, sans-serif',
    animations: { enabled: true, speed: 350 },
    stacked: props.type === 'bar',
  },
  colors: (props.data?.datasets || []).map((dataset) => dataset.borderColor || dataset.backgroundColor).flat().filter(Boolean),
  labels: props.data?.labels || [],
  legend: {
    position: 'bottom',
    fontSize: '11px',
    labels: { colors: '#526174' },
    itemMargin: { horizontal: 8, vertical: 2 },
  },
  stroke: {
    curve: 'smooth',
    width: chartType.value === 'line' ? 2 : 0,
  },
  markers: { size: chartType.value === 'line' ? 3 : 0, strokeWidth: 0 },
  fill: {
    type: chartType.value === 'line' ? 'solid' : 'solid',
    opacity: chartType.value === 'line' ? 0.16 : 1,
  },
  dataLabels: { enabled: false },
  grid: { borderColor: '#e9edf3', strokeDashArray: 3 },
  xaxis: {
    categories: props.data?.labels || [],
    labels: { style: { colors: '#718096', fontSize: '11px' } },
    axisBorder: { show: false },
    axisTicks: { show: false },
  },
  yaxis: {
    min: 0,
    labels: { style: { colors: '#718096', fontSize: '11px' } },
  },
  plotOptions: {
    bar: { borderRadius: 2, columnWidth: '58%' },
    pie: { donut: { size: '64%' } },
  },
  tooltip: { theme: 'light' },
  responsive: [{ breakpoint: 640, options: { legend: { fontSize: '10px' } } }],
}))
</script>

<template>
  <div class="chart-wrap apex-chart-wrap">
    <VueApexCharts :type="chartType" height="100%" :options="options" :series="series" />
  </div>
</template>
