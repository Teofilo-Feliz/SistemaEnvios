<script setup>defineProps({ title: String, padded: { type: Boolean, default: true } })</script>
<template><section class="base-card" :class="{ 'no-padding': !padded }"><header v-if="title || $slots.header" class="card-header"><slot name="header"><h2>{{ title }}</h2></slot><slot name="actions" /></header><div class="card-content"><slot /></div></section></template>
