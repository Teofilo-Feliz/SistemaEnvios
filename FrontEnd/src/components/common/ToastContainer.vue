<script setup>
import { CheckCircle2, CircleAlert, Info, X } from "lucide-vue-next";
import { useUiStore } from "@/stores/uiStore";
const ui = useUiStore();
const icons = { success: CheckCircle2, error: CircleAlert, warning: CircleAlert, info: Info };
</script>
<template>
  <div class="toast-container" aria-live="polite">
    <div v-for="toast in ui.toasts" :key="toast.id" class="toast" :class="toast.type">
      <component :is="icons[toast.type]" :size="19" /><span>{{ toast.message }}</span
      ><button @click="ui.removeToast(toast.id)"><X :size="16" /></button>
    </div>
  </div>
</template>
