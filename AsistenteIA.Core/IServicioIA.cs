using OpenAI.Chat;

namespace AsistenteIA.Core;

/// <summary>
/// Abstracción del modelo de IA. La API depende de esta interfaz y no de Azure directamente:
/// así se puede cambiar de modelo o de proveedor por configuración, y en las pruebas
/// se reemplaza por una implementación falsa sin gastar tokens.
/// </summary>
public interface IServicioIA
{
    Task<RespuestaIA> CompletarAsync(IReadOnlyList<ChatMessage> contexto, CancellationToken ct = default);
}

/// <summary>Respuesta del modelo con los datos de consumo.</summary>
public sealed record RespuestaIA(string Texto, int TokensEntrada, int TokensSalida, string MotivoFin);
