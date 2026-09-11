import Swal from "sweetalert2";

export function escapeHtml(value) {
  return String(value ?? "—")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

export async function confirmAction({
  title = "Confirmar operación",
  text,
  html,
  confirmText = "Confirmar",
  icon = "question",
} = {}) {
  const result = await Swal.fire({
    title,
    text,
    html,
    icon,
    showCancelButton: true,
    confirmButtonText: confirmText,
    cancelButtonText: "Cancelar",
    reverseButtons: true,
    focusCancel: true,
    confirmButtonColor: "#3d5f70",
    cancelButtonColor: "#6b7c87",
  });
  return result.isConfirmed;
}

/** Aviso de un solo botón: cuando no hay nada que confirmar sino algo que entender. */
export function avisar({ title = "Atención", text, html, icon = "warning" } = {}) {
  return Swal.fire({
    title,
    text,
    html,
    icon,
    confirmButtonText: "Entendido",
    confirmButtonColor: "#3d5f70",
  });
}

export function notifyNotificationsChanged() {
  window.dispatchEvent(new CustomEvent("notifications-changed"));
}
