<script setup>
import { onMounted } from "vue";
import { useRouter } from "vue-router";
import { authService, isOidcConfigured } from "@/services/authService";
import { useAuthStore } from "@/stores/authStore";

// Destino del post_logout_redirect_uri. No muestra pantalla propia: limpia la sesión local
// y manda de vuelta al login de AuthManager, que es donde se inicia sesión de verdad.
// La pantalla /login solo tiene sentido cuando OIDC no está configurado.
const router = useRouter();
const auth = useAuthStore();

onMounted(async () => {
  auth.logoutLocal();
  if (!isOidcConfigured) return router.replace("/login");
  try {
    await authService.signinRedirect();
  } catch {
    router.replace("/login");
  }
});
</script>

<template>
  <div />
</template>
