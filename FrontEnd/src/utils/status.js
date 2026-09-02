export const statusMap = {
  PREPARACION_TECNOLOGIA: {
    label: "En preparación de tecnología",
    tone: "neutral",
  },
  ENTREGADO_TRANSPORTACION: {
    label: "Entregado a transportación",
    tone: "info",
  },
  DESPACHADO_TRANSPORTE_PRIVADO: {
    label: "Despachado por transporte privado",
    tone: "info",
  },
  EN_TRANSITO: { label: "En tránsito", tone: "primary" },
  RECIBIDO_TRANSPORTACION: {
    label: "Recibido por transportación",
    tone: "success",
  },
  ESPERA_TECNOLOGIA: { label: "En espera de tecnología", tone: "warning" },
  EN_REVISION_TECNOLOGIA: { label: "En proceso de revisión", tone: "primary" },
  RECIBIDO_TECNOLOGIA: { label: "Recibido por tecnología", tone: "success" },
  DESPACHADO_TECNOLOGIA: { label: "Entregado a transportación", tone: "info" },
  EN_TRANSPORTACION: { label: "En transportación", tone: "info" },
  TRANSPORTE_ASIGNADO: { label: "Chofer asignado", tone: "info" },
  DESPACHADO_TRANSPORTACION: { label: "Despachado por transportación", tone: "info" },
  PENDIENTE_RECEPCION_FILIAL: {
    label: "Pendiente de recepción en filial",
    tone: "warning",
  },
  RECIBIDO_FILIAL: { label: "Recibido en filial", tone: "success" },
  RECEPCION_VALIDADA_FILIAL: {
    label: "Recepción validada en filial",
    tone: "success",
  },
  INCIDENCIA_TRANSPORTACION: {
    label: "Recibido con incidencia",
    tone: "danger",
  },
  CERRADO: { label: "Cerrado", tone: "success" },
  branch: { label: "En filial", tone: "neutral" },
  transport_delivered: { label: "Entregado a transportación", tone: "info" },
  transport_pending: { label: "Pendiente confirmación", tone: "warning" },
  transport_confirmed: { label: "Confirmado por transportación", tone: "info" },
  transit: { label: "En tránsito", tone: "primary" },
  transport_received: { label: "Recibido por transportación", tone: "success" },
  tech_pending: { label: "En espera de tecnología", tone: "warning" },
  review: { label: "En proceso de revisión", tone: "primary" },
  tech_received: { label: "Recibido por tecnología", tone: "success" },
  technology_dispatch: { label: "Despachado por tecnología", tone: "info" },
  branch_pending: {
    label: "Pendiente de recepción en filial",
    tone: "warning",
  },
  branch_received: { label: "Recibido en filial", tone: "success" },
  branch_validated: { label: "Recepción validada en filial", tone: "success" },
  incident: { label: "Recibido con incidencia", tone: "danger" },
  closed: { label: "Cerrado", tone: "success" },
  open: { label: "Abierta", tone: "danger" },
  resolved: { label: "Resuelta", tone: "success" },
};

export function getStatus(status) {
  return (
    statusMap[status] || { label: status || "Sin estado", tone: "neutral" }
  );
}
