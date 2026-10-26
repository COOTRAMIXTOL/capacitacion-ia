using System.ClientModel;
using OpenAI.Chat;

namespace AsistenteIA.Core;

/// <summary>
/// Implementación de <see cref="IServicioIA"/> sobre un despliegue de Azure OpenAI.
/// Traduce los errores del SDK a <see cref="ErrorIAException"/>.
/// </summary>
public sealed class ServicioAzureOpenAI(ChatClient chat, OpcionesIA opciones) : IServicioIA
{
    public async Task<RespuestaIA> CompletarAsync(IReadOnlyList<ChatMessage> contexto, CancellationToken ct = default)
    {
        var opcionesChat = new ChatCompletionOptions
        {
            Temperature = opciones.Temperature,
            MaxOutputTokenCount = opciones.MaxOutputTokens
        };

        try
        {
            ChatCompletion r = await chat.CompleteChatAsync(contexto, opcionesChat, ct);

            // El filtro también revisa la SALIDA: si la cortó, no se entrega al usuario
            if (r.FinishReason == ChatFinishReason.ContentFilter)
                throw new ErrorIAException(TipoErrorIA.ContenidoBloqueado,
                    "La respuesta fue interrumpida por el filtro de contenido.");

            string texto = r.Content.Count > 0 ? r.Content[0].Text : "";
            return new RespuestaIA(
                texto,
                r.Usage?.InputTokenCount ?? 0,
                r.Usage?.OutputTokenCount ?? 0,
                r.FinishReason.ToString());
        }
        catch (ClientResultException ex) when (ex.Status == 400 && ex.Message.Contains("content_filter"))
        {
            throw new ErrorIAException(TipoErrorIA.ContenidoBloqueado,
                "La pregunta fue bloqueada por el filtro de contenido.", ex);
        }
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            throw new ErrorIAException(TipoErrorIA.CuotaExcedida,
                "Se superó la cuota del modelo.", ex);
        }
        catch (ClientResultException ex) when (ex.Status is 401 or 403)
        {
            throw new ErrorIAException(TipoErrorIA.Permisos,
                "La aplicación no tiene permiso sobre el modelo.", ex);
        }
    }
}
