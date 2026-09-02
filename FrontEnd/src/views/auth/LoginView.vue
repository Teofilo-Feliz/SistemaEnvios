<script setup>
import { LogIn } from 'lucide-vue-next'
import { authService, isOidcConfigured } from '@/services/authService'
import logo from '@/assets/img/logitrack.webp'
// Sin esta importación la pantalla se dibuja sin estilos: el archivo existe pero ningún
// otro módulo lo carga.
import '@/assets/styles/login.css'
function login(){if(isOidcConfigured) authService.signinRedirect()}
</script>
<template>
  <main class="login-page">
    <section class="login-card" aria-labelledby="login-title">
      <div class="login-brand">
        <div class="login-brand-mark"><img :src="logo" alt="LogiTrack" /></div>
        <div>
          <h1 id="login-title">LogiTrack</h1>
          <p>Rastreo y gestión de envíos</p>
        </div>
      </div>
      <p class="login-welcome">El acceso se gestiona desde AuthManager.</p>
      <button class="login-submit" type="button" :disabled="!isOidcConfigured" @click="login">
        <LogIn :size="17" />{{ isOidcConfigured ? 'Continuar con AuthManager' : 'AuthManager no configurado' }}
      </button>
      <p class="login-footer">Serás redirigido al portal corporativo para iniciar sesión.</p>
    </section>
  </main>
</template>
