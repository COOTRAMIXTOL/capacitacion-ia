using System.Globalization;
using System.Threading.RateLimiting;
using AsistenteIA.Core;
using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;

var builder = WebApplication.CreateBuilder(args);

// ---------- Configuración (appsettings.json + variables de entorno) ----------

OpcionesIA ia = builder.Configuration.GetSection("AzureOpenAI").Get<OpcionesIA>()
    ?? throw new InvalidOperationException("Falta la sección AzureOpenAI en appsettings.json");
string promptSistema = builder.Configuration["Asistente:PromptSistema"]
    ?? throw new InvalidOperationException("Falta Asistente:PromptSistema en appsettings.json");

if (string.IsNullOrWhiteSpace(ia.Endpoint) || string.IsNullOrWhiteSpace(ia.Deployment))
    throw new InvalidOperationException("Endpoint y Deployment son obligatorios.");

// ---------- Servicios ----------

builder.Services.AddSingleton(ia);

// Un solo cliente para toda la aplicación: reutiliza el token y las conexiones.
// Sin claves: en local usa az login / Visual Studio; en Azure, la identidad administrada.
builder.Services.AddSingleton(_ =>
    new AzureOpenAIClient(new Uri(ia.Endpoint), new DefaultAzureCredential())
        .GetChatClient(ia.Deployment));

// La API depende de la interfaz, no de Azure
builder.Services.AddSingleton<IServicioIA, ServicioAzureOpenAI>();

// Errores en formato estándar RFC 9457 (Problem Details)
builder.Services.AddProblemDetails();

// Documento OpenAPI en /openapi/v1.json (solo en desarrollo)
builder.Services.AddOpenApi();

// Verificación de salud en /health (la usará Azure App Service)
builder.Services.AddHealthChecks();

// Límite de uso: 10 preguntas por minuto por dirección IP. Protege la cuota y el costo.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("por-ip", contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

var app = builder.Build();

app.UseExceptionHandler();   // nunca expone stack traces: responde Problem Details
app.UseStatusCodePages();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapHealthChecks("/health");

// ---------- Endpoint de chat ----------

app.MapPost("/api/v1/chat", async (
        SolicitudChat solicitud,
        IServicioIA servicio,
        OpcionesIA opciones,
        ILogger<Program> log,
        CancellationToken ct) =>
    {
        // 1. Validar antes de gastar tokens
        Dictionary<string, string[]> errores = ValidadorChat.Validar(solicitud);
        if (errores.Count > 0)
            return Results.ValidationProblem(errores);

        // 2. Armar el historial: el prompt de sistema lo pone SIEMPRE el servidor
        var cultura = new CultureInfo("es-CO");
        string fechaHoy = DateTime.Now.ToString("dddd d 'de' MMMM 'de' yyyy", cultura);

        var historial = new List<ChatMessage>
        {
            new SystemChatMessage($"{promptSistema}\nFecha actual: {fechaHoy}.")
        };

        foreach (MensajeHistorial m in solicitud.Historial ?? [])
        {
            historial.Add(m.Rol == ValidadorChat.RolUsuario
                ? new UserChatMessage(m.Contenido)
                : (ChatMessage)new AssistantChatMessage(m.Contenido));
        }

        historial.Add(new UserChatMessage(solicitud.Pregunta));

        // 3. Ventana deslizante: la misma regla probada en GestorContextoTests
        List<ChatMessage> contexto = GestorContexto.Construir(historial, opciones.MaxTurnosHistorial);

        // 4. Llamar al modelo y traducir sus errores a respuestas HTTP
        try
        {
            RespuestaIA r = await servicio.CompletarAsync(contexto, ct);

            // Se registran métricas, NUNCA el contenido de la conversación (Ley 1581)
            log.LogInformation(
                "Chat respondido. Entrada: {Entrada} tokens, salida: {Salida} tokens, fin: {Fin}, mensajes: {Mensajes}",
                r.TokensEntrada, r.TokensSalida, r.MotivoFin, contexto.Count);

            return Results.Ok(new RespuestaChat(
                r.Texto, r.TokensEntrada, r.TokensSalida, r.MotivoFin, contexto.Count));
        }
        catch (ErrorIAException ex) when (ex.Tipo == TipoErrorIA.ContenidoBloqueado)
        {
            log.LogWarning("Solicitud bloqueada por el filtro de contenido.");
            return Results.Problem(
                title: "Solicitud no procesada",
                detail: "No puedo procesar esa solicitud. Reformule su pregunta sobre trámites mineros.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (ErrorIAException ex) when (ex.Tipo == TipoErrorIA.CuotaExcedida)
        {
            log.LogWarning("Cuota del modelo excedida.");
            return Results.Problem(
                title: "Alta demanda",
                detail: "Hay alta demanda en este momento. Intente de nuevo en unos segundos.",
                statusCode: StatusCodes.Status429TooManyRequests);
        }
        catch (ErrorIAException ex) when (ex.Tipo == TipoErrorIA.Permisos)
        {
            // Es un problema de configuración del servidor: se registra el detalle,
            // pero al cliente solo se le dice que el servicio no está disponible.
            log.LogError(ex, "La identidad de la API no tiene permiso sobre el modelo.");
            return Results.Problem(
                title: "Servicio no disponible",
                detail: "El asistente no está disponible en este momento.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    })
    .RequireRateLimiting("por-ip")
    .WithName("Chat");

app.Run();
