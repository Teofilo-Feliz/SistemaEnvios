export const filterFields = [
  { key: "numeroEnvio", label: "Número de envío", type: "text" },
  {
    key: "estadoEnvioId",
    label: "Estado del envío",
    type: "select",
    source: "states",
  },
  {
    key: "ubicacionOrigenId",
    label: "Origen",
    type: "select",
    source: "locations",
  },
  {
    key: "ubicacionDestinoId",
    label: "Destino",
    type: "select",
    source: "locations",
  },
  {
    key: "direccion",
    label: "Dirección",
    type: "select",
    source: "directions",
  },
  {
    key: "tipoTransporteId",
    label: "Tipo de transporte",
    type: "select",
    source: "transportTypes",
  },
  { key: "observaciones", label: "Observaciones", type: "text" },
  { key: "envioId", label: "Identificador", type: "number" },
];
export const operatorsByType = {
  text: [
    { value: "equals", label: "es" },
    { value: "notEquals", label: "no es" },
    { value: "contains", label: "contiene" },
    { value: "notContains", label: "no contiene" },
    { value: "startsWith", label: "empieza por" },
    { value: "endsWith", label: "termina en" },
    { value: "empty", label: "vacío" },
    { value: "notEmpty", label: "no vacío" },
  ],
  number: [
    { value: "equals", label: "=" },
    { value: "notEquals", label: "!=" },
    { value: "greaterThan", label: ">" },
    { value: "greaterOrEqual", label: ">=" },
    { value: "lessThan", label: "<" },
    { value: "lessOrEqual", label: "<=" },
  ],
  date: [
    { value: "equals", label: "es" },
    { value: "before", label: "antes de" },
    { value: "after", label: "después de" },
    { value: "between", label: "entre" },
  ],
  select: [
    { value: "equals", label: "es" },
    { value: "notEquals", label: "no es" },
  ],
  boolean: [{ value: "equals", label: "es" }],
};
export function createRule() {
  return {
    id: crypto.randomUUID(),
    type: "rule",
    logic: "AND",
    field: filterFields[0].key,
    operator: "equals",
    value: "",
  };
}
export function createGlobalRule() {
  return {
    id: crypto.randomUUID(),
    type: "global",
    logic: "AND",
    operator: "contains",
    value: "",
  };
}
export function createGroup() {
  return {
    id: crypto.randomUUID(),
    type: "group",
    logic: "OR",
    rules: [createRule()],
  };
}
