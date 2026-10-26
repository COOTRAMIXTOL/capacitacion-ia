namespace AsistenteIA.Core;

/// <summary>Un mensaje anterior de la conversación, enviado por el cliente.</summary>
/// <param name="Rol">"usuario" o "asistente". El rol de sistema NUNCA lo envía el cliente.</param>
/// <param name="Contenido">Texto del mensaje.</param>
public sealed record MensajeHistorial(string Rol, string Contenido);

/// <summary>Cuerpo de POST /api/v1/chat.</summary>
/// <param name="Pregunta">Pregunta actual del usuario.</param>
/// <param name="Historial">Mensajes anteriores (opcional). La API no guarda estado: el cliente lo reenvía.</param>
public sealed record SolicitudChat(string Pregunta, List<MensajeHistorial>? Historial);

/// <summary>Respuesta de POST /api/v1/chat.</summary>
public sealed record RespuestaChat(
    string Respuesta,
    int TokensEntrada,
    int TokensSalida,
    string MotivoFin,
    int MensajesEnviados);
