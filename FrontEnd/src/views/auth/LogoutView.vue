<script setup>
import { onMounted } from "vue";
import { useAuthStore } from "@/stores/authStore";
defineOptions({ name: "LogoutPage" });
const authStore = useAuthStore();
onMounted(async () => {
  await authStore.logout();
});
</script>
<template><div></div></template>
