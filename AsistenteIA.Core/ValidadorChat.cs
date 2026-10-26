namespace AsistenteIA.Core;

/// <summary>
/// Valida la solicitud ANTES de llamar al modelo: rechazar temprano es más barato
/// y evita enviar al modelo contenido malformado o manipulado.
/// </summary>
public static class ValidadorChat
{
    public const int MaxLongitudPregunta = 2000;
    public const int MaxLongitudMensaje = 4000;
    public const int MaxMensajesHistorial = 20;

    public const string RolUsuario = "usuario";
    public const string RolAsistente = "asistente";

    /// <summary>Devuelve los errores por campo. Un diccionario vacío significa solicitud válida.</summary>
    public static Dictionary<string, string[]> Validar(SolicitudChat solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var errores = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(solicitud.Pregunta))
            errores["pregunta"] = ["La pregunta es obligatoria."];
        else if (solicitud.Pregunta.Length > MaxLongitudPregunta)
            errores["pregunta"] = [$"La pregunta no puede superar {MaxLongitudPregunta} caracteres."];

        List<MensajeHistorial> historial = solicitud.Historial ?? [];

        if (historial.Count > MaxMensajesHistorial)
        {
            errores["historial"] = [$"El historial no puede tener más de {MaxMensajesHistorial} mensajes."];
            return errores;
        }

        if (historial.Count % 2 != 0)
            errores["historial"] = ["El historial debe tener pares completos de pregunta y respuesta."];

        for (int i = 0; i < historial.Count; i++)
        {
            MensajeHistorial m = historial[i];
            string rolEsperado = i % 2 == 0 ? RolUsuario : RolAsistente;

            // Solo se aceptan "usuario" y "asistente": así nadie puede inyectar
            // un mensaje de sistema que cambie las instrucciones del asistente.
            if (m.Rol != rolEsperado)
                errores[$"historial[{i}].rol"] = [$"Se esperaba el rol '{rolEsperado}'."];

            if (string.IsNullOrWhiteSpace(m.Contenido))
                errores[$"historial[{i}].contenido"] = ["El contenido es obligatorio."];
            else if (m.Contenido.Length > MaxLongitudMensaje)
                errores[$"historial[{i}].contenido"] = [$"El contenido no puede superar {MaxLongitudMensaje} caracteres."];
        }

        return errores;
    }
}
