<script setup>
import { useAuthStore } from '@/stores/authStore'
const auth = useAuthStore()
</script>
<template><main class="login-page"><section class="login-card"><h1>Acceso no autorizado</h1><p class="login-welcome">Tu usuario no tiene permisos asignados para esta sección. Si crees que es un error, verifica con el administrador los permisos de la aplicación en AuthManager.</p><RouterLink class="login-submit" to="/dashboard">Volver al dashboard</RouterLink><p class="login-footer"><a href="#" @click.prevent="auth.logout()">Cerrar sesión</a></p></section></main></template>
