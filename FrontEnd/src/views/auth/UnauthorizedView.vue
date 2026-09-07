<script setup>
import { computed } from 'vue'
import { useAuthStore } from '@/stores/authStore'
const auth = useAuthStore()

// Sin perfil reconocido, "volver al dashboard" rebota aquí mismo: el guard lo devuelve porque
// el usuario no alcanza ningún módulo. Se ofrece solo si hay a dónde volver de verdad.
const inicio = computed(() => auth.moduloInicio)
const hayADondeVolver = computed(() => inicio.value !== '/unauthorized')

// Quien administra necesita la posición y los roles exactos que emite AuthManager para poder
// mapearlos; sin ese dato la única salida es adivinar cómo viene escrito el cargo.
const posicion = computed(() => auth.profile?.position?.trim() || null)
const roles = computed(() => auth.assignedRoles.filter(Boolean))
</script>

<template>
  <main class="login-page">
    <section class="login-card">
      <h1>Acceso no autorizado</h1>
      <p class="login-welcome">
        Tu usuario no tiene un perfil asignado en este sistema, así que no puede abrir ningún
        módulo. Verifica con Tecnología los permisos de la aplicación en AuthManager.
      </p>
      <p v-if="posicion || roles.length" class="login-welcome">
        Datos para quien lo configure —
        <strong>posición:</strong> {{ posicion || 'sin posición' }};
        <strong>roles:</strong> {{ roles.length ? roles.join(', ') : 'sin roles' }}.
      </p>
      <RouterLink v-if="hayADondeVolver" class="login-submit" :to="inicio">Volver a mi módulo</RouterLink>
      <p class="login-footer"><a href="#" @click.prevent="auth.logout()">Cerrar sesión</a></p>
    </section>
  </main>
</template>
