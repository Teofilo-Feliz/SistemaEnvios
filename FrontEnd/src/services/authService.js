import { UserManager, WebStorageStateStore } from 'oidc-client-ts'

const authority = import.meta.env.VITE_AUTH_AUTHORITY || ''
const settings = {
  userStore: new WebStorageStateStore({ store: window.localStorage }),
  authority,
  client_id: import.meta.env.VITE_AUTH_CLIENT_ID || '',
  redirect_uri: `${window.location.origin}/callback`,
  post_logout_redirect_uri: `${window.location.origin}/signed-out`,
  response_type: import.meta.env.VITE_AUTH_RESPONSE_TYPE || 'code',
  scope: import.meta.env.VITE_AUTH_SCOPES || 'openid profile roles',
  loadUserInfo: true,
  automaticSilentRenew: true,
  silent_redirect_uri: `${window.location.origin}/silent-renew`,
  monitorSession: false,
  includeIdTokenInSilentRenew: true,
  // Sin metadata a mano: al declararla, oidc-client-ts omite el discovery, y el bloque que
  // había no incluía issuer ni jwks_uri — sin ellos no se puede validar la firma del id_token.
  // El servidor publica los mismos cuatro endpoints en su .well-known, más esos dos.
}
export const userManager = authority && settings.client_id ? new UserManager(settings) : null
export const isOidcConfigured = Boolean(userManager)

// Cierre de sesion sin id_token_hint.
//
// oidc-client-ts adjunta el id_token del usuario almacenado como id_token_hint. Si ese token
// no fue emitido para este cliente, OpenIddict rechaza el logout con ID2141 ("The client
// application is not allowed to use the specified identity token hint") y el usuario queda en
// una pagina de error, con la sesion local todavia abierta.
//
// Quitando el usuario antes del redirect no se envia hint, y client_id le da al servidor lo
// que necesita para validar el post_logout_redirect_uri. El orden importa: si el servidor
// rechazara igual, la sesion local ya quedo cerrada en vez de a medias.
async function signoutRedirect() {
  if (!userManager) return
  await userManager.removeUser().catch(() => {})
  return userManager.signoutRedirect({
    extraQueryParams: { client_id: settings.client_id },
  })
}

export const authService = {
  signinRedirect: (options) => userManager?.signinRedirect(options),
  signinCallback: () => userManager?.signinRedirectCallback(),
  signinSilentCallback: () => userManager?.signinSilentCallback(),
  signoutRedirect,
}
