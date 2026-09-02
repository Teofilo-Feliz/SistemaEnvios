import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import { useAuthStore } from './stores/authStore'
import './assets/styles/main.css'

const app = createApp(App).use(createPinia())
useAuthStore().initSsoMonitoring()
app.use(router).mount('#app')
