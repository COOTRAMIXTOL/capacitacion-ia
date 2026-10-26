using OpenAI.Chat;

/// <summary>
/// Decide qué parte del historial se envía al modelo en cada llamada.
/// Se separó de Program.cs para poder probarlo sin llamar a Azure OpenAI.
/// </summary>
public static class GestorContexto
{
    /// <summary>
    /// Devuelve el prompt de sistema + los últimos <paramref name="maxTurnos"/> turnos
    /// (pregunta + respuesta) + la pregunta actual del usuario.
    /// No modifica el historial original.
    /// </summary>
    /// <param name="historial">Historial completo. El primer mensaje debe ser el de sistema
    /// y el último la pregunta actual del usuario.</param>
    /// <param name="maxTurnos">Cantidad de turnos anteriores a conservar (0 o más).</param>
    public static List<ChatMessage> Construir(IReadOnlyList<ChatMessage> historial, int maxTurnos)
    {
        ArgumentNullException.ThrowIfNull(historial);
        ArgumentOutOfRangeException.ThrowIfNegative(maxTurnos);

        if (historial.Count == 0 || historial[0] is not SystemChatMessage)
            throw new ArgumentException("El historial debe iniciar con el prompt de sistema.", nameof(historial));

        if (historial[^1] is not UserChatMessage)
            throw new ArgumentException("El último mensaje del historial debe ser la pregunta del usuario.", nameof(historial));

        var contexto = new List<ChatMessage> { historial[0] };   // el prompt de sistema SIEMPRE va

        int mensajesConversacion = historial.Count - 1;          // sin contar el de sistema
        int aTomar = Math.Min(mensajesConversacion, maxTurnos * 2 + 1);

        // Contando desde el final (que es una pregunta), un número impar de mensajes
        // garantiza que la ventana empiece en una pregunta del usuario, nunca en una respuesta suelta.
        contexto.AddRange(historial.Skip(historial.Count - aTomar));

        return contexto;
    }
}
