using System.ClientModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using AsistenteIA.Core;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

// Configuración: appsettings.json, sobreescribible con variables de entorno
IConfiguration config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

OpcionesIA ia = config.GetSection("AzureOpenAI").Get<OpcionesIA>()
    ?? throw new InvalidOperationException("Falta la sección AzureOpenAI en appsettings.json");
string promptSistema = config["Asistente:PromptSistema"]
    ?? throw new InvalidOperationException("Falta Asistente:PromptSistema en appsettings.json");

if (string.IsNullOrWhiteSpace(ia.Endpoint) || string.IsNullOrWhiteSpace(ia.Deployment))
    throw new InvalidOperationException("Endpoint y Deployment son obligatorios.");
if (ia.MaxTurnosHistorial < 0)
    throw new InvalidOperationException("MaxTurnosHistorial no puede ser negativo.");

// Sin claves: usa tu sesión de Azure CLI o de Visual Studio
var cliente = new AzureOpenAIClient(new Uri(ia.Endpoint), new DefaultAzureCredential());
ChatClient chat = cliente.GetChatClient(ia.Deployment);

// WARM-UP: una llamada mínima A TRAVÉS DEL CLIENTE al arrancar.
// El SDK guarda el token y la conexión dentro del cliente, así que esta llamada
// deja ambos listos y la primera pregunta del usuario ya no paga ese costo.
var relojInicio = Stopwatch.StartNew();
try
{
    await chat.CompleteChatAsync(
        new ChatMessage[] { new UserChatMessage("ok") },
        new ChatCompletionOptions { MaxOutputTokenCount = 16 });
    Console.WriteLine($"[conexión y autenticación listas en {relojInicio.ElapsedMilliseconds} ms]");
}
catch (AuthenticationFailedException ex)
{
    Console.WriteLine($"No fue posible autenticarse. Ejecuta 'az login'.\n{ex.Message}");
    return;
}
catch (ClientResultException ex) when (ex.Status is 401 or 403)
{
    Console.WriteLine($"Error de permisos ({ex.Status}): revisa el rol en IAM.\n{ex.Message}");
    return;
}

// Grounding: el modelo no tiene reloj, así que le damos la fecha real
var cultura = new CultureInfo("es-CO");
string fechaHoy = DateTime.Now.ToString("dddd d 'de' MMMM 'de' yyyy", cultura);

// Historial COMPLETO de la conversación (útil para auditoría o para mostrar al usuario).
// Al modelo NO se le envía completo: se le envía una ventana (ver GestorContexto en AsistenteIA.Core).
var historial = new List<ChatMessage>
{
    new SystemChatMessage($"{promptSistema}\nFecha actual: {fechaHoy}.")
};

var opciones = new ChatCompletionOptions
{
    Temperature = ia.Temperature,
    MaxOutputTokenCount = ia.MaxOutputTokens
};

int totalTokens = 0;
Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine($"Asistente ANM - despliegue: {ia.Deployment} | ventana: {ia.MaxTurnosHistorial} turnos " +
                  "(escribe 'salir' para terminar)\n");

while (true)
{
    Console.Write("Tú: ");
    string? texto = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(texto)) continue;
    if (texto.Trim().ToLower() == "salir") break;

    historial.Add(new UserChatMessage(texto));

    // VENTANA DESLIZANTE: prompt de sistema + últimos N turnos + la pregunta actual
    List<ChatMessage> contexto = GestorContexto.Construir(historial, ia.MaxTurnosHistorial);

    // Aquí se va armando la respuesta completa mientras llega por partes
    var respuesta = new StringBuilder();
    ChatFinishReason? motivoFin = null;
    ChatTokenUsage? uso = null;

    // Métricas de streaming
    var reloj = Stopwatch.StartNew();
    long msPrimerToken = -1;
    int fragmentosConTexto = 0;

    try
    {
        Console.Write("\nAsistente: ");

        // STREAMING: el modelo envía la respuesta en fragmentos a medida que la genera
        await foreach (StreamingChatCompletionUpdate fragmento in
                       chat.CompleteChatStreamingAsync(contexto, opciones))
        {
            foreach (ChatMessageContentPart parte in fragmento.ContentUpdate)
            {
                if (string.IsNullOrEmpty(parte.Text)) continue;
                if (msPrimerToken < 0) msPrimerToken = reloj.ElapsedMilliseconds;
                fragmentosConTexto++;

                Console.Write(parte.Text);      // se muestra de inmediato
                respuesta.Append(parte.Text);   // y se acumula para el historial
            }

            if (fragmento.FinishReason is not null) motivoFin = fragmento.FinishReason;
            if (fragmento.Usage is not null) uso = fragmento.Usage;   // llega en el último fragmento
        }

        reloj.Stop();
        Console.WriteLine();
        Console.WriteLine($"[primer texto: {msPrimerToken} ms | respuesta completa: {reloj.ElapsedMilliseconds} ms " +
                          $"| fragmentos con texto: {fragmentosConTexto}]");
        Console.WriteLine($"[mensajes enviados al modelo: {contexto.Count} de {historial.Count} en el historial]");

        // El filtro de contenido también revisa la SALIDA mientras se genera
        if (motivoFin == ChatFinishReason.ContentFilter)
        {
            Console.WriteLine("\n[La respuesta fue interrumpida por el filtro de contenido.]\n");
            historial.RemoveAt(historial.Count - 1);
            continue;
        }

        historial.Add(new AssistantChatMessage(respuesta.ToString()));

        if (uso is not null)
        {
            totalTokens += uso.TotalTokenCount;
            Console.WriteLine($"[entrada: {uso.InputTokenCount} | salida: {uso.OutputTokenCount} " +
                              $"| fin: {motivoFin} | acumulado sesión: {totalTokens}]\n");
        }
        else
        {
            Console.WriteLine($"[fin: {motivoFin} | el servicio no reportó tokens en este streaming]\n");
        }

        if (motivoFin == ChatFinishReason.Length)
            Console.WriteLine("Aviso: la respuesta se cortó por el límite de tokens.\n");
    }
    catch (ClientResultException ex) when (ex.Status == 400 && ex.Message.Contains("content_filter"))
    {
        historial.RemoveAt(historial.Count - 1); // no guardamos el mensaje bloqueado
        Console.WriteLine("No puedo procesar esa solicitud. " +
                          "Reformula tu pregunta sobre trámites mineros.\n");
    }
    catch (ClientResultException ex) when (ex.Status == 429)
    {
        historial.RemoveAt(historial.Count - 1);
        Console.WriteLine("Hay alta demanda en este momento. Intenta de nuevo en unos segundos.\n");
    }
    catch (ClientResultException ex) when (ex.Status is 401 or 403)
    {
        Console.WriteLine($"Error de permisos ({ex.Status}):\n{ex.Message}\n");
        break;
    }
}
