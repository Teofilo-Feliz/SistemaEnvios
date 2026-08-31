<script setup>
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { Eye, EyeOff, LockKeyhole, LogIn, Package, Plane, Truck, Container } from 'lucide-vue-next'
import { authService } from '@/services/authService'
import { useAuthStore } from '@/stores/authStore'
import '@/assets/styles/login.css'

const router = useRouter()
const auth = useAuthStore()
const form = reactive({ username: '', password: '' })
const errors = reactive({ username: '', password: '' })
const loading = ref(false)
const showPassword = ref(false)
const serverError = ref('')

function validate() {
  errors.username = form.username ? '' : 'El usuario es obligatorio.'
  errors.password = form.password ? '' : 'La contraseña es obligatoria.'
  return !errors.username && !errors.password
}

async function handleLogin() {
  serverError.value = ''
  if (!validate()) return
  loading.value = true
  try {
    const { data } = await authService.demoLogin(form)
    localStorage.setItem('auth_token', data.accessToken)
    auth.setIdentity({ user: data.user, permissions: data.permissions })
    await router.push({ name: 'dashboard' })
  } catch (exception) {
    serverError.value = exception.userMessage || 'Usuario o contraseña incorrectos.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <main class="login-page">
    <div class="login-backdrop" aria-hidden="true"></div>
    <Truck class="login-decor login-decor-truck" :size="150" stroke-width="1" />
    <Container class="login-decor login-decor-container" :size="135" stroke-width="1" />
    <Package class="login-decor login-decor-package" :size="125" stroke-width="1" />
    <Plane class="login-decor login-decor-plane" :size="145" stroke-width="1" />

    <section class="login-card" aria-labelledby="login-title">
      <div class="login-brand">
        <div class="login-brand-mark"><Package :size="27" stroke-width="2.2" /></div>
        <div><h1 id="login-title">ADR Track</h1><p>Rastreo y gestión de envíos</p></div>
      </div>
      <p class="login-welcome">Ingresa a tu cuenta para continuar</p>
      <form class="login-form" @submit.prevent="handleLogin" novalidate>
        <div class="login-field"><label for="username">Usuario</label><div class="login-input-wrap" :class="{ 'has-error': errors.username }"><LogIn :size="17" aria-hidden="true" /><input id="username" v-model.trim="form.username" autocomplete="username" placeholder="Tu usuario" @input="errors.username = ''" /></div><small v-if="errors.username" class="login-field-error">{{ errors.username }}</small></div>
        <div class="login-field"><label for="password">Contraseña</label><div class="login-input-wrap" :class="{ 'has-error': errors.password }"><LockKeyhole :size="17" aria-hidden="true" /><input id="password" v-model="form.password" :type="showPassword ? 'text' : 'password'" autocomplete="current-password" placeholder="Tu contraseña" @input="errors.password = ''" /><button type="button" class="password-toggle" :aria-label="showPassword ? 'Ocultar contraseña' : 'Mostrar contraseña'" @click="showPassword = !showPassword"><EyeOff v-if="showPassword" :size="17" /><Eye v-else :size="17" /></button></div><small v-if="errors.password" class="login-field-error">{{ errors.password }}</small></div>
        <label class="remember-me"><input type="checkbox" /> <span>Recordarme</span></label>
        <div v-if="serverError" class="login-server-error" role="alert">{{ serverError }}</div>
        <button class="login-submit" type="submit" :disabled="loading"><span v-if="loading" class="login-spinner" aria-hidden="true"></span><LogIn v-else :size="17" />{{ loading ? 'Ingresando...' : 'Entrar al sistema' }}</button>
      </form>
      <p class="login-footer">Plataforma corporativa de logística</p>
    </section>
  </main>
</template>
