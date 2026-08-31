<script setup>
defineProps({ title: String })
</script>
<template><section class="shipment-section"><header>{{ title }}</header><div class="shipment-section-body"><slot /></div></section></template>
