<script setup>
import { onMounted } from "vue";
import { useRouter } from "vue-router";
import { useAuthStore } from "@/stores/authStore";
defineOptions({ name: "CallbackPage" });
const router = useRouter();
const authStore = useAuthStore();
onMounted(async () => {
  try {
    const callbackUser = await authStore.handleRedirectCallback();
    const state = callbackUser?.state;
    await router.replace(state?.returnUrl || "/");
  } catch {
    await router.replace("/login");
  }
});
</script>
<template><div class="page-loading">Validando sesión…</div></template>
