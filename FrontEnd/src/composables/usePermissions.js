import { useAuthStore } from '@/stores/authStore'
export function usePermissions() { const auth = useAuthStore(); return { can: auth.can } }
