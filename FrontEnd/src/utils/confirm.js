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
    confirmButtonColor: "#3267d6",
    cancelButtonColor: "#68758a",
  });
  return result.isConfirmed;
}

export function notifyNotificationsChanged() {
  window.dispatchEvent(new CustomEvent("notifications-changed"));
}
