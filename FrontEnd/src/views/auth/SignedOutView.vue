<script setup>
import { onMounted } from 'vue'
import { LogIn } from 'lucide-vue-next'
import { authService, isOidcConfigured } from '@/services/authService'
import { useAuthStore } from '@/stores/authStore'
import logo from '@/assets/img/logitrack.webp'
const auth = useAuthStore()
onMounted(() => auth.logoutLocal())
function login(){if(isOidcConfigured) authService.signinRedirect()}
</script>
<template>
  <main class="login-page">
    <section class="login-card" aria-labelledby="signed-out-title">
      <div class="login-brand">
        <div class="login-brand-mark"><img :src="logo" alt="LogiTrack" /></div>
        <div>
          <h1 id="signed-out-title">LogiTrack</h1>
          <p>Rastreo y gestión de envíos</p>
        </div>
      </div>
      <p class="login-welcome">Tu sesión se cerró correctamente.</p>
      <button class="login-submit" type="button" :disabled="!isOidcConfigured" @click="login">
        <LogIn :size="17" />Volver a iniciar sesión
      </button>
    </section>
  </main>
</template>
