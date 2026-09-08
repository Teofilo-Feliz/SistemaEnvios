import { computed, ref } from "vue";
import { defineStore } from "pinia";
import { authService, userManager } from "@/services/authService";
import { perfilService } from "@/services/perfilService";
import { inicioDe } from "@/config/modulos";
import { registrarProveedorDeToken } from "@/services/api";

const aliases = {
  canCreateShipment: "envios.crear",
  canReceiveShipment: "recepciones.gestionar",
  canConfirmTransport: "transportes.confirmar",
  canReviewEquipment: "recepciones.gestionar",
  canManageCatalogs: "catalogos.administrar",
};
const toList = (value) =>
  String(value ?? "")
    .split(",")
    .map((x) => x.trim())
    .filter(Boolean);

export const useAuthStore = defineStore("auth", () => {
  const user = ref(null);
  const permissions = ref([]);
  let unauthorizedInFlight = null;
  const isAuthenticated = computed(() => Boolean(user.value && !user.value.expired));
  const profile = computed(() => user.value?.profile || {});
  const assignedRoles = computed(() => toList(profile.value.roles));
  const roles = computed(() => assignedRoles.value);
  const displayName = computed(() => profile.value.name || profile.value.email || "Usuario");
  const initials = computed(() =>
    displayName.value
      .split(/\s+/)
      .map((x) => x[0])
      .join("")
      .slice(0, 2)
      .toUpperCase(),
  );
  const roleLabel = computed(() => roles.value[0] || profile.value.position || "");
  // El claim llega como "30,SANTO DOMINGO (SEDE)": id de filial en AuthManager + nombre.
  // Misma separación que HttpUserContext en el backend, que es quien restringe los datos.
  const affiliate = computed(() => profile.value.affiliate || null);
  const affiliateId = computed(() => {
    const id = Number.parseInt(
      String(affiliate.value ?? "")
        .split(",", 1)[0]
        .trim(),
      10,
    );
    return Number.isInteger(id) ? id : null;
  });
  const affiliateName = computed(() => {
    const partes = String(affiliate.value ?? "").split(",");
    return partes.length > 1 ? partes.slice(1).join(",").trim() : null;
  });
  const hasRole = computed(
    () =>
      (required = []) =>
        required.some((role) => roles.value.includes(role)),
  );
  const hasAssignedRole = computed(
    () =>
      (required = []) =>
        required.some((role) => assignedRoles.value.includes(role)),
  );
  const hasPermission = computed(
    () =>
      (required = []) =>
        required.some((permission) => permissions.value.includes(permission)),
  );
  const can = (permission) =>
    !permission ||
    permissions.value.includes(permission) ||
    permissions.value.includes(aliases[permission]);

  // El perfil y los permisos efectivos los resuelve el backend: AuthManager emite permisos de
  // otra aplicacion ("evaluador") y una posicion que aqui se traduce a alcance. Hasta que
  // /api/perfil responda, el frontend no sabe que puede pedir.
  const perfil = ref(null);
  const perfilNombre = computed(() => perfil.value?.perfil || null);
  const esGlobal = computed(() => perfil.value?.perfil === "Global");
  // Espeja EsTecnologia() del backend: Global y Tecnologia mandan sobre todo el sistema, y lo
  // que las separa son las pantallas, no los datos. Sin esto, soporte técnico no veía el botón
  // de editar un envío en preparación que el backend sí le permite.
  const mandaEnTodo = computed(() => ["Global", "Tecnologia"].includes(perfil.value?.perfil));
  const esFilial = computed(() => perfil.value?.perfil === "Filial");
  // Dónde aterriza este usuario al entrar: su propio módulo, no el tablero general.
  const moduloInicio = computed(() => inicioDe(perfilNombre.value));
  const puedeFiltrarPorFilial = computed(() => Boolean(perfil.value?.puedeFiltrarPorFilial));
  const filialNombre = computed(() => perfil.value?.filialNombre || affiliateName.value);
  const filialSinMapear = computed(
    () => Boolean(perfil.value) && perfil.value.filialId != null && !perfil.value.filialMapeada,
  );

  async function cargarPerfil() {
    if (!user.value) return null;
    try {
      const { data } = await perfilService.get();
      perfil.value = data;
      if (Array.isArray(data?.permisos)) permissions.value = data.permisos;
      return data;
    } catch {
      perfil.value = null;
      return null;
    }
  }

  function setIdentity(identity) {
    user.value = identity;
    permissions.value = toList(identity?.profile?.permissions);
  }
  function logoutLocal() {
    localStorage.removeItem("auth_permissions");
    user.value = null;
    permissions.value = [];
    perfil.value = null;
  }

  async function handleRedirectCallback() {
    const result = await authService.signinCallback();
    if (result) setIdentity(result);
    return result;
  }
  async function handleSilentRenewCallback() {
    return authService.signinSilentCallback();
  }
  async function renewSilent() {
    if (!userManager) return null;
    try {
      const result = await userManager.signinSilent();
      if (result) setIdentity(result);
      return result;
    } catch {
      return null;
    }
  }
  // Nunca propaga: un fallo de sesión significa "no autenticado", no una navegación rota.
  // Sin usuario almacenado no se intenta renovación silenciosa: el redirect al AuthManager
  // resuelve el SSO por cookie sin esperar el timeout del iframe.
  async function checkSession() {
    if (!userManager) {
      logoutLocal();
      return false;
    }
    const current = await userManager.getUser().catch(() => null);
    if (current && !current.expired) {
      setIdentity(current);
      return true;
    }
    const renewed = current ? await renewSilent() : null;
    if (!renewed) logoutLocal();
    return Boolean(renewed);
  }
  async function getValidToken() {
    if (user.value?.access_token && !user.value.expired) return user.value.access_token;
    const result = await renewSilent();
    return result?.access_token || null;
  }
  // Pasa por authService.signoutRedirect, que cierra la sesion local antes de redirigir:
  // si el AuthManager rechaza el logout, la app no queda creyendo que seguis autenticado.
  async function logout() {
    if (!userManager) return logoutLocal();
    try {
      await authService.signoutRedirect();
    } catch {
      logoutLocal();
    }
  }
  function forceLogoutAndRedirectToLogin(returnUrl) {
    return userManager
      ? authService.signinRedirect(returnUrl ? { state: { returnUrl } } : undefined)
      : null;
  }

  // 401 del API o token vencido: un solo intento de renovación silenciosa y, si falla, de vuelta al AuthManager.
  // Responde al interceptor con el token renovado para que reintente la petición perdida.
  async function handleUnauthorizedEvent(evento) {
    const resolver = evento?.detail?.resolve;
    if (!resolver) return handleUnauthorized();
    const renovado = await renewSilent();
    if (renovado?.access_token) return resolver(renovado.access_token);
    resolver(null);
    logoutLocal();
    await forceLogoutAndRedirectToLogin(location.pathname + location.search);
  }
  function handleUnauthorized() {
    if (unauthorizedInFlight) return unauthorizedInFlight;
    unauthorizedInFlight = (async () => {
      if (await renewSilent()) return;
      logoutLocal();
      await forceLogoutAndRedirectToLogin(location.pathname + location.search);
    })().finally(() => {
      unauthorizedInFlight = null;
    });
    return unauthorizedInFlight;
  }
  function initSsoMonitoring() {
    // El interceptor pide el token al store en cada peticion, asi nunca manda uno vencido.
    registrarProveedorDeToken(getValidToken);
    window.addEventListener("auth:unauthorized", handleUnauthorizedEvent);
    if (!userManager) return;
    userManager.events.addUserLoaded(setIdentity);
    userManager.events.addUserUnloaded(logoutLocal);
    userManager.events.addAccessTokenExpired(handleUnauthorized);
    userManager.events.addSilentRenewError(logoutLocal);
  }

  return {
    user,
    permissions,
    profile,
    isAuthenticated,
    roles,
    assignedRoles,
    displayName,
    initials,
    roleLabel,
    affiliate,
    affiliateId,
    affiliateName,
    perfil,
    perfilNombre,
    esGlobal,
    mandaEnTodo,
    esFilial,
    moduloInicio,
    puedeFiltrarPorFilial,
    filialNombre,
    filialSinMapear,
    cargarPerfil,
    hasRole,
    hasAssignedRole,
    hasPermission,
    can,
    setIdentity,
    logoutLocal,
    logout,
    renewSilent,
    handleRedirectCallback,
    handleSilentRenewCallback,
    checkSession,
    getValidToken,
    forceLogoutAndRedirectToLogin,
    initSsoMonitoring,
  };
});
