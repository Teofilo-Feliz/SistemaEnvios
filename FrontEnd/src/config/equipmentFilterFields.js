export const equipmentFilterFields = [
  { key: "numeroSerie", label: "Serial", type: "text" },
  { key: "codigoActivo", label: "Código de activo", type: "text" },
  { key: "marca", label: "Marca", type: "text" },
  { key: "modelo", label: "Modelo", type: "text" },
  { key: "tipoEquipoId", label: "Tipo de equipo", type: "select", source: "types" },
  { key: "ubicacionActualId", label: "Ubicación actual", type: "select", source: "locations" },
  { key: "equipoId", label: "Identificador", type: "number" },
];
