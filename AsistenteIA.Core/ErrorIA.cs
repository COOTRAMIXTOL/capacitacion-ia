namespace AsistenteIA.Core;

/// <summary>Tipos de error del servicio de IA que la aplicación sabe tratar.</summary>
public enum TipoErrorIA
{
    /// <summary>El filtro de contenido bloqueó la pregunta o la respuesta (incluye jailbreak).</summary>
    ContenidoBloqueado,

    /// <summary>Se superó la cuota de tokens o peticiones por minuto (HTTP 429).</summary>
    CuotaExcedida,

    /// <summary>La identidad de la aplicación no tiene permiso sobre el modelo (HTTP 401/403).</summary>
    Permisos
}

/// <summary>
/// Error del servicio de IA traducido a un tipo propio, para que quien lo consume
/// no dependa de los códigos ni de las excepciones del SDK de Azure.
/// </summary>
public sealed class ErrorIAException(TipoErrorIA tipo, string mensaje, Exception? interna = null)
    : Exception(mensaje, interna)
{
    public TipoErrorIA Tipo { get; } = tipo;
}
