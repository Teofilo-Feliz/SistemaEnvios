namespace SistemaEnvios.Api.Security;

/// <summary>
/// Marca una acción que exige usuario autenticado pero ningún permiso concreto. Es la única
/// excepción admitida a "toda acción exige una política", y ControllerSecurityTests verifica
/// que la lista de acciones marcadas no crezca sin que alguien lo decida.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class AutenticacionSuficienteAttribute : Attribute;
