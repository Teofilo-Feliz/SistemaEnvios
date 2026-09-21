using FluentValidation;
using SistemaEnvios.Application.DTOs.Common;
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Casos;
using SistemaEnvios.Application.DTOs.Envios;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Application.Interfaces.Services.Integraciones;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.Envios;

public sealed class EnvioService(
    IGenericRepository<Envio> envios,
    SistemaEnviosDbContext db,
    IUnitOfWork unitOfWork,
    IValidator<CrearEnvioRequest> validator,
    IValidator<ActualizarEnvioRequest> actualizarValidator,
    IUserContext userContext,
    IAlcanceEnvios alcance,
    ICasoEquipoService casos,
    IValidadorTicketGlpi ticketsGlpi) : IEnvioService
{
    public async Task<Result<EnvioResponse>> CrearConEquiposAsync(CrearEnvioConEquiposRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Equipos is null || request.Equipos.Count == 0)
            return Result<EnvioResponse>.Failure("El envío debe incluir al menos un equipo.", ErrorType.Validation);
        // Los equipos que arrastran un caso abierto heredan su ticket, así que lo que venga
        // en la solicitud para ellos no se valida ni se usa: el ticket es del caso, no del viaje.
        var casosPorEquipo = new Dictionary<int, CasoAbiertoResponse?>();
        foreach (var item in request.Equipos)
            casosPorEquipo[item.EquipoId] = await casos.BuscarAbiertoAsync(item.EquipoId, cancellationToken);

        var ticketsNuevos = request.Equipos
            .Where(x => casosPorEquipo[x.EquipoId] is null)
            .Select(x => x.NumeroTicket?.Trim())
            .ToList();
        if (ticketsNuevos.Any(x => !NumeroTicket.EsValido(x)) ||
            ticketsNuevos.Distinct(StringComparer.Ordinal).Count() != ticketsNuevos.Count)
            return Result<EnvioResponse>.Failure(
                "Los tickets deben ser numéricos, mayores que cero, de 3 a 50 dígitos y no repetirse.",
                ErrorType.Validation);

        // Se comprueban todos contra GLPI antes de tocar la base: si uno no existe, el envío no
        // llega a crearse a medias. Solo los nuevos; los heredados de un caso ya están fuera de
        // esta lista por construcción. En paralelo y con tope: en serie, tres tickets con GLPI
        // lento pasaban del minuto y el navegador abandonaba antes.
        var enGlpi = await ticketsGlpi.ValidarVariosAsync([.. ticketsNuevos!], cancellationToken);
        if (enGlpi.IsFailure)
            return Result<EnvioResponse>.Failure(enGlpi.Error!, enGlpi.ErrorType);
        if (userContext.UserId is not Guid usuarioId)
            return Result<EnvioResponse>.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);
        // Todo se arma en memoria y se guarda con un único SaveChanges, que ya es atómico.
        // Antes eran dos: el envío se guardaba primero y los equipos después, así que un fallo
        // en el segundo paso dejaba un envío vacío, y un reintento por error transitorio de SQL
        // volvía a ejecutar la creación sobre un contexto que aún seguía a la entidad anterior,
        // duplicando el envío.
        var envioResult = await ConstruirEnvioAsync(
            new CrearEnvioRequest
            {
                UbicacionOrigenId = request.UbicacionOrigenId,
                UbicacionDestinoId = request.UbicacionDestinoId,
                Observaciones = request.Observaciones,
            },
            usuarioId, cancellationToken);
        if (envioResult.IsFailure)
            return Result<EnvioResponse>.Failure(envioResult.Error!, envioResult.ErrorType);

        var envio = envioResult.Value!;
        var fechaAsociacion = DateTime.UtcNow;
        foreach (var item in request.Equipos)
        {
            var equipo = await db.Equipos.FindAsync([item.EquipoId], cancellationToken);
            if (equipo is null || equipo.UbicacionActualId != request.UbicacionOrigenId)
                return Result<EnvioResponse>.Failure("Uno de los equipos no existe o no está en el origen.", ErrorType.Conflict);
            if (await db.ReservasEquipoEnvio.AnyAsync(x => x.EquipoId == item.EquipoId, cancellationToken))
                return Result<EnvioResponse>.Failure($"El equipo ya pertenece a otro envío activo.", ErrorType.Conflict);

            var caso = casosPorEquipo[item.EquipoId];
            if (caso is not null && request.UbicacionDestinoId != caso.FilialId && envio.Direccion == DireccionEnvioEnum.HaciaFilial)
                return Result<EnvioResponse>.Failure(
                    "El equipo tiene un caso abierto con otra filial y debe volver a ella. Descártelo de esa filial para poder reasignarlo.",
                    ErrorType.Conflict);

            // Un ticket solo puede estrenarse una vez: se compara contra las aperturas, que son
            // las únicas filas que lo estrenan. Las continuaciones repiten el ticket a propósito.
            var ticket = caso?.NumeroTicket ?? item.NumeroTicket.Trim();
            if (caso is null && await db.EnvioEquipos.AnyAsync(
                    x => x.EnvioEquipoOrigenId == null && x.NumeroTicket == ticket, cancellationToken))
                return Result<EnvioResponse>.Failure($"El ticket {ticket} ya fue utilizado para abrir otro caso.", ErrorType.Conflict);

            db.EnvioEquipos.Add(new EnvioEquipo
            {
                Envio = envio,
                EquipoId = item.EquipoId,
                NumeroTicket = ticket,
                EnvioEquipoOrigenId = caso?.EnvioEquipoAperturaId,
                Observaciones = item.Observaciones?.Trim() ?? "Equipo asociado al envío.",
                UsuarioSolicitanteId = usuarioId,
                FechaCreacion = fechaAsociacion,
                UsuarioCreacionId = usuarioId,
            });
            db.ReservasEquipoEnvio.Add(new ReservaEquipoEnvio
            {
                EquipoId = item.EquipoId,
                Envio = envio,
                FechaReserva = fechaAsociacion,
                UsuarioId = usuarioId,
            });
        }

        var aperturas = casosPorEquipo.Values
            .Where(x => x is not null)
            .Select(x => x!.EnvioEquipoAperturaId)
            .ToArray();
        if (aperturas.Length > 0 && await db.EnvioEquipos.AsNoTracking()
                .AnyAsync(x => aperturas.Contains(x.EnvioEquipoId) && x.FechaCierreCaso != null, cancellationToken))
        {
            return Result<EnvioResponse>.Failure(
                "El caso de uno de los equipos se cerró mientras se preparaba el envío. " +
                "Vuelva a cargar la pantalla antes de intentarlo de nuevo.",
                ErrorType.Conflict);
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return await ExplicarConflictoAsync(request, casosPorEquipo, cancellationToken);
        }
        return Result<EnvioResponse>.Success(ToResponse(envio));
    }
    /// <summary>
    /// Qué chocó exactamente, para que el mensaje lo diga.
    /// </summary>
    /// <remarks>
    /// Se vuelve a preguntar a la base en vez de leer el texto del error de SQL Server: ahí el
    /// nombre del índice se rompe con cualquier renombrado y el valor duplicado hay que
    /// trocearlo de una frase.
    ///
    /// Medido en la prueba de concurrencia: con ocho personas usando el mismo ticket, a las siete
    /// que pierden las para el índice, no la comprobación previa. O sea, este es el camino normal
    /// cuando dos personas coinciden, no un caso raro.
    /// </remarks>
    private async Task<Result<EnvioResponse>> ExplicarConflictoAsync(
        CrearEnvioConEquiposRequest request,
        IReadOnlyDictionary<int, CasoAbiertoResponse?> casosPorEquipo,
        CancellationToken cancellationToken)
    {
        var ticketsNuevos = request.Equipos
            .Where(x => casosPorEquipo[x.EquipoId] is null)
            .Select(x => x.NumeroTicket.Trim())
            .ToArray();

        var tomado = await db.EnvioEquipos.AsNoTracking()
            .Where(x => x.EnvioEquipoOrigenId == null && ticketsNuevos.Contains(x.NumeroTicket))
            .Select(x => x.NumeroTicket)
            .FirstOrDefaultAsync(cancellationToken);
        if (tomado is not null)
        {
            return Result<EnvioResponse>.Failure(
                $"El ticket {tomado} ya fue utilizado para abrir otro caso. " +
                "Alguien lo registró mientras usted llenaba el formulario.",
                ErrorType.Conflict);
        }

        var equipoIds = request.Equipos.Select(x => x.EquipoId).ToArray();
        var reservado = await db.ReservasEquipoEnvio.AsNoTracking()
            .Where(x => equipoIds.Contains(x.EquipoId))
            .Select(x => x.Equipo.NumeroSerie ?? x.Equipo.CodigoActivo)
            .FirstOrDefaultAsync(cancellationToken);
        if (reservado is not null)
        {
            return Result<EnvioResponse>.Failure(
                $"El equipo {reservado} ya viaja en otro envío activo. " +
                "Alguien lo agregó mientras usted llenaba el formulario.",
                ErrorType.Conflict);
        }

        return Result<EnvioResponse>.Failure(
            "No fue posible asociar los equipos; el envío no se creó.", ErrorType.Conflict);
    }

    public async Task<Result<EnvioResponse>> CrearAsync(
        CrearEnvioRequest request,
        CancellationToken cancellationToken = default)
    {
        if (userContext.UserId is not Guid usuarioId)
            return Result<EnvioResponse>.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);

        var construido = await ConstruirEnvioAsync(request, usuarioId, cancellationToken);
        if (construido.IsFailure)
            return Result<EnvioResponse>.Failure(construido.Error!, construido.ErrorType);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<EnvioResponse>.Success(ToResponse(construido.Value!));
    }

    /// <summary>
    /// Deja el envío y su primer registro de historial en el contexto, sin guardar. Quien llama
    /// decide cuándo persistir, para que crear un envío con equipos sea un solo SaveChanges.
    /// </summary>
    private async Task<Result<Envio>> ConstruirEnvioAsync(
        CrearEnvioRequest request,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result<Envio>.Failure(validation.ToErrorMessage(), ErrorType.Validation);

        var ubicaciones = await db.Ubicaciones
            .Where(x => (x.UbicacionId == request.UbicacionOrigenId ||
                         x.UbicacionId == request.UbicacionDestinoId) && x.Activo)
            .ToDictionaryAsync(x => x.UbicacionId, cancellationToken);

        if (ubicaciones.Count != 2)
            return Result<Envio>.Failure("La ubicación de origen o destino no existe o está inactiva.", ErrorType.Validation);

        var origen = ubicaciones[request.UbicacionOrigenId];
        var destino = ubicaciones[request.UbicacionDestinoId];
        var direccionResult = DeterminarDireccion(origen, destino);

        if (direccionResult.IsFailure)
            return Result<Envio>.Failure(direccionResult.Error!, direccionResult.ErrorType);

        var direccion = direccionResult.Value;

        // Un usuario de filial solo crea envíos de su propia filial. Sin esto podría registrar
        // uno a nombre de otra y después ni verlo, porque el alcance de lectura sí filtra.
        if (await alcance.ResolverPerfilAsync(cancellationToken) == PerfilAlcance.Filial)
        {
            var filialDelEnvio = direccion == DireccionEnvioEnum.HaciaTecnologia ? origen : destino;
            if (filialDelEnvio.FilialExternaId != userContext.AffiliateId)
                return Result<Envio>.Failure(
                    $"Solo puede registrar envíos de su filial ({filialDelEnvio.Nombre} no le corresponde).",
                    ErrorType.Forbidden);
        }

        var codigoInicial = direccion == DireccionEnvioEnum.HaciaTecnologia
            ? EstadoEnvioCodigos.EnFilial
            : EstadoEnvioCodigos.EnPreparacionTecnologia;
        var estadoInicial = await db.EstadosEnvio.FirstOrDefaultAsync(
            x => x.Codigo == codigoInicial && x.Activo,
            cancellationToken);

        if (estadoInicial is null)
            return Result<Envio>.Failure("El estado inicial del flujo no se encuentra configurado.", ErrorType.Conflict);

        // Ni NumeroEnvio ni FechaCreacion se asignan aquí: los pone la base al insertar, el
        // primero calculado a partir del segundo. EF los lee de vuelta tras el SaveChanges.
        var fechaActual = DateTime.UtcNow;
        var envio = new Envio
        {
            UbicacionOrigenId = request.UbicacionOrigenId,
            UbicacionDestinoId = request.UbicacionDestinoId,
            EstadoEnvioId = estadoInicial.EstadoEnvioId,
            Direccion = direccion,
            UsuarioSolicitanteId = usuarioId,
            Observaciones = NormalizarOpcional(request.Observaciones),
            UsuarioCreacionId = usuarioId
        };

        await envios.AddAsync(envio, cancellationToken);
        db.HistorialEstadosEnvio.Add(new HistorialEstadoEnvio
        {
            Envio = envio,
            EstadoEnvioId = estadoInicial.EstadoEnvioId,
            UbicacionId = request.UbicacionOrigenId,
            UsuarioId = usuarioId,
            Fecha = fechaActual,
            Observaciones = "Envío creado.",
            FechaCreacion = fechaActual,
            UsuarioCreacionId = usuarioId
        });

        return Result<Envio>.Success(envio);
    }

    public async Task<Result<EnvioResponse>> ObtenerAsync(
        int envioId,
        CancellationToken cancellationToken = default)
    {
        var envio = await db.Envios.Include(x => x.UbicacionOrigen).Include(x => x.UbicacionDestino).Include(x => x.Transporte!).ThenInclude(x => x.TipoTransporte).FirstOrDefaultAsync(x => x.EnvioId == envioId, cancellationToken);
        if (envio is null)
            return Result<EnvioResponse>.Failure("El envío no existe.", ErrorType.NotFound);

        var permitido = await alcance.VerificarAsync(envioId, cancellationToken);
        if (permitido.IsFailure)
            return Result<EnvioResponse>.Failure(permitido.Error!, permitido.ErrorType);

        return Result<EnvioResponse>.Success(ToResponse(envio));
    }

    public async Task<Result<PaginaResponse<EnvioResponse>>> ConsultarAsync(ConsultarEnviosRequest request, CancellationToken cancellationToken = default)
    {
        var mapeada = await alcance.VerificarFilialMapeadaAsync(cancellationToken);
        if (mapeada.IsFailure)
            return Result<PaginaResponse<EnvioResponse>>.Failure(mapeada.Error!, mapeada.ErrorType);

        var query = await alcance.FiltrarAsync(db.Envios.AsNoTracking(), cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.Search)) { var term = request.Search.Trim(); query = query.Where(x => x.NumeroEnvio.Contains(term) || (x.Observaciones != null && x.Observaciones.Contains(term))); }
        if (request.EstadoEnvioId.HasValue) query = query.Where(x => x.EstadoEnvioId == request.EstadoEnvioId);
        if (request.TipoTransporteId.HasValue) query = query.Where(x => x.Transporte != null && x.Transporte.TipoTransporteId == request.TipoTransporteId);
        if (request.UbicacionOrigenId.HasValue) query = query.Where(x => x.UbicacionOrigenId == request.UbicacionOrigenId);
        if (request.UbicacionDestinoId.HasValue) query = query.Where(x => x.UbicacionDestinoId == request.UbicacionDestinoId);
        if (request.UbicacionId.HasValue) query = query.Where(x => x.UbicacionOrigenId == request.UbicacionId || x.UbicacionDestinoId == request.UbicacionId);
        if (request.Direccion is 1 or 2) query = query.Where(x => (int)x.Direccion == request.Direccion);
        if (request.EstadoCodigos is { Length: > 0 } codigos) query = query.Where(x => codigos.Contains(x.EstadoEnvio.Codigo));
        if (request.EstrategiaTransporte is { } estrategia) query = query.Where(x => x.Transporte != null && x.Transporte.TipoTransporte.Estrategia == estrategia);
        // Deja pasar los que aún no tienen transporte: el envío que Tecnología despacha espera a
        // que Transportación le asigne chofer, y hasta entonces no hay transporte que consultar.
        if (request.ExcluirEstrategiaTransporte is { } excluida) query = query.Where(x => x.Transporte == null || x.Transporte.TipoTransporte.Estrategia != excluida);
        var pagina = await query.OrderByDescending(x => x.FechaCreacion).ThenByDescending(x => x.EnvioId)
            .PaginarAsync(request, x => new EnvioResponse(x.EnvioId,x.NumeroEnvio,x.UbicacionOrigenId,x.UbicacionDestinoId,x.EstadoEnvioId,x.Direccion,x.UsuarioSolicitanteId,x.Observaciones,x.Transporte == null ? null : x.Transporte.TipoTransporteId,x.Transporte == null ? null : x.Transporte.TipoTransporte.Estrategia,x.Transporte == null ? null : x.Transporte.TipoTransporte.Nombre), cancellationToken);
        return Result<PaginaResponse<EnvioResponse>>.Success(pagina);
    }

    public async Task<Result> ActualizarAsync(
        ActualizarEnvioRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await actualizarValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure(validation.ToErrorMessage(), ErrorType.Validation);
        if (userContext.UserId is not Guid usuarioId)
            return Result.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);

        var envio = await db.Envios.Include(x => x.EstadoEnvio).Include(x => x.UbicacionOrigen)
            .FirstOrDefaultAsync(x => x.EnvioId == request.EnvioId, cancellationToken);
        if (envio is null)
            return Result.Failure("El envío no existe.", ErrorType.NotFound);

        // Sin esto se podía editar cualquier envío conociendo su id, incluso de otra filial.
        var enAlcance = await alcance.VerificarAsync(envio.EnvioId, cancellationToken);
        if (enAlcance.IsFailure) return enAlcance;

        if (!EsEstadoEditable(envio.EstadoEnvio.Codigo))
            return Result.Failure("El envío ya fue despachado y no admite modificaciones.", ErrorType.Conflict);

        var custodia = await VerificarCustodiaAsync(envio, cancellationToken);
        if (custodia.IsFailure) return custodia;

        // Se traen también las ubicaciones actuales del envío: sin ellas el historial diría
        // "Origen: #4 → Azua" en vez de nombrar de dónde venía.
        int[] involucradas = [
            request.UbicacionOrigenId, request.UbicacionDestinoId,
            envio.UbicacionOrigenId, envio.UbicacionDestinoId];
        var ubicaciones = await db.Ubicaciones
            .Where(x => involucradas.Contains(x.UbicacionId))
            .ToDictionaryAsync(x => x.UbicacionId, cancellationToken);

        if (!ubicaciones.TryGetValue(request.UbicacionOrigenId, out var nuevoOrigen) || !nuevoOrigen.Activo ||
            !ubicaciones.TryGetValue(request.UbicacionDestinoId, out var nuevoDestino) || !nuevoDestino.Activo)
            return Result.Failure("La ubicación de origen o destino no existe o está inactiva.", ErrorType.Validation);

        var direccionResult = DeterminarDireccion(nuevoOrigen, nuevoDestino);
        if (direccionResult.IsFailure)
            return Result.Failure(direccionResult.Error!, direccionResult.ErrorType);

        var codigoInicial = direccionResult.Value == DireccionEnvioEnum.HaciaTecnologia
            ? EstadoEnvioCodigos.EnFilial
            : EstadoEnvioCodigos.EnPreparacionTecnologia;
        var estadoInicial = await db.EstadosEnvio.FirstOrDefaultAsync(
            x => x.Codigo == codigoInicial && x.Activo,
            cancellationToken);
        if (estadoInicial is null)
            return Result.Failure("El estado inicial del flujo no se encuentra configurado.", ErrorType.Conflict);

        var equiposFueraDelNuevoOrigen = await db.EnvioEquipos
            .AnyAsync(
                x => x.EnvioId == envio.EnvioId &&
                     x.Equipo.UbicacionActualId != request.UbicacionOrigenId,
                cancellationToken);
        if (equiposFueraDelNuevoOrigen)
            return Result.Failure(
                "No se puede cambiar el origen porque uno o más equipos no se encuentran en la nueva ubicación.",
                ErrorType.Conflict);

        var fecha = DateTime.UtcNow;
        var observacionesNuevas = NormalizarOpcional(request.Observaciones);
        // El detalle del cambio se arma antes de mutar el envío: después ya no hay con qué
        // comparar, y un historial que solo diga "se editó" no sirve para auditar nada.
        var cambios = DescribirCambios(envio, request, observacionesNuevas, ubicaciones);

        envio.UbicacionOrigenId = request.UbicacionOrigenId;
        envio.UbicacionDestinoId = request.UbicacionDestinoId;
        envio.Direccion = direccionResult.Value;
        envio.EstadoEnvioId = estadoInicial.EstadoEnvioId;
        envio.Observaciones = observacionesNuevas;
        envio.FechaModificacion = fecha;
        envio.UsuarioModificacionId = usuarioId;

        if (cambios.Count > 0)
        {
            db.HistorialEstadosEnvio.Add(new HistorialEstadoEnvio
            {
                EnvioId = envio.EnvioId,
                EstadoEnvioId = estadoInicial.EstadoEnvioId,
                UbicacionId = envio.UbicacionOrigenId,
                UsuarioId = usuarioId,
                Fecha = fecha,
                Observaciones = $"Envío editado. {string.Join(" ", cambios)}",
                FechaCreacion = fecha,
                UsuarioCreacionId = usuarioId
            });
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(
                "El envío fue modificado por otra operación. Actualice los datos e intente nuevamente.",
                ErrorType.Conflict);
        }
        return Result.Success();
    }

    private static Result<DireccionEnvioEnum> DeterminarDireccion(Ubicacion origen, Ubicacion destino)
    {
        if (origen.Tipo == TipoUbicacionEnum.Filial && destino.Tipo == TipoUbicacionEnum.Tecnologia)
            return Result<DireccionEnvioEnum>.Success(DireccionEnvioEnum.HaciaTecnologia);

        if (origen.Tipo == TipoUbicacionEnum.Tecnologia && destino.Tipo == TipoUbicacionEnum.Filial)
            return Result<DireccionEnvioEnum>.Success(DireccionEnvioEnum.HaciaFilial);

        return Result<DireccionEnvioEnum>.Failure(
            "El envío debe realizarse entre una filial y Tecnología.", ErrorType.Validation);
    }

    private static EnvioResponse ToResponse(Envio envio) => new(
        envio.EnvioId,
        envio.NumeroEnvio,
        envio.UbicacionOrigenId,
        envio.UbicacionDestinoId,
        envio.EstadoEnvioId,
        envio.Direccion,
        envio.UsuarioSolicitanteId,
        envio.Observaciones,
        envio.Transporte?.TipoTransporteId,
        envio.Transporte?.TipoTransporte.Estrategia,
        envio.Transporte?.TipoTransporte.Nombre);

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>
    /// Un envío se puede editar mientras no haya empezado a moverse. Cada dirección tiene su
    /// estado de partida: la filial prepara en EN_FILIAL y Tecnología en PREPARACION_TECNOLOGIA.
    /// </summary>
    private static bool EsEstadoEditable(string codigo) =>
        codigo is EstadoEnvioCodigos.EnFilial or EstadoEnvioCodigos.EnPreparacionTecnologia;

    /// <summary>
    /// Estar dentro del alcance no basta para editar: un envío que Tecnología prepara para una
    /// filial aparece en el listado de esa filial, y aun así es de Tecnología hasta que sale.
    /// Edita quien lo tiene en la mano, que es distinto de quién puede verlo.
    /// </summary>
    private async Task<Result> VerificarCustodiaAsync(Envio envio, CancellationToken cancellationToken)
    {
        var perfil = await alcance.ResolverPerfilAsync(cancellationToken);
        if (perfil.EsTecnologia()) return Result.Success();

        if (envio.EstadoEnvio.Codigo == EstadoEnvioCodigos.EnPreparacionTecnologia)
            return Result.Failure(
                "Este envío lo está preparando Tecnología y solo Tecnología puede modificarlo.",
                ErrorType.Forbidden);

        if (perfil == PerfilAlcance.Filial && envio.UbicacionOrigen.FilialExternaId != userContext.AffiliateId)
            return Result.Failure(
                "Solo puede modificar los envíos que originó su filial.", ErrorType.Forbidden);

        return perfil is PerfilAlcance.Filial
            ? Result.Success()
            : Result.Failure("Su perfil no puede modificar envíos.", ErrorType.Forbidden);
    }

    /// <summary>Diferencias legibles entre el envío guardado y lo que llega en la edición.</summary>
    private static List<string> DescribirCambios(
        Envio envio,
        ActualizarEnvioRequest request,
        string? observacionesNuevas,
        IReadOnlyDictionary<int, Ubicacion> ubicaciones)
    {
        var cambios = new List<string>();
        var nombre = (int id) => ubicaciones.TryGetValue(id, out var u) ? u.Nombre : $"#{id}";

        if (envio.UbicacionOrigenId != request.UbicacionOrigenId)
            cambios.Add($"Origen: {nombre(envio.UbicacionOrigenId)} → {nombre(request.UbicacionOrigenId)}.");
        if (envio.UbicacionDestinoId != request.UbicacionDestinoId)
            cambios.Add($"Destino: {nombre(envio.UbicacionDestinoId)} → {nombre(request.UbicacionDestinoId)}.");
        if (!string.Equals(envio.Observaciones, observacionesNuevas, StringComparison.Ordinal))
            cambios.Add("Observaciones actualizadas.");

        return cambios;
    }
}
