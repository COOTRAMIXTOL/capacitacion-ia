using AsistenteIA.Core;
using OpenAI.Chat;

namespace ConsolaAzureOpenAI.Tests;

public class GestorContextoTests
{
    // ---------- Ayudas para armar historiales de prueba ----------

    private const string PromptSistema = "Eres el asistente institucional de la ANM.";

    /// <summary>
    /// Crea un historial con el prompt de sistema, 'turnosPrevios' pares pregunta/respuesta
    /// y una pregunta actual al final.
    /// </summary>
    private static List<ChatMessage> CrearHistorial(int turnosPrevios)
    {
        var historial = new List<ChatMessage> { new SystemChatMessage(PromptSistema) };

        for (int i = 1; i <= turnosPrevios; i++)
        {
            historial.Add(new UserChatMessage($"pregunta {i}"));
            historial.Add(new AssistantChatMessage($"respuesta {i}"));
        }

        historial.Add(new UserChatMessage("pregunta actual"));
        return historial;
    }

    private static string Texto(ChatMessage mensaje) => mensaje.Content[0].Text;

    // ---------- Pruebas ----------

    [Fact]
    public void Construir_SiempreIncluyePromptDeSistemaPrimero()
    {
        var historial = CrearHistorial(turnosPrevios: 10);

        var contexto = GestorContexto.Construir(historial, maxTurnos: 2);

        Assert.IsType<SystemChatMessage>(contexto[0]);
        Assert.Equal(PromptSistema, Texto(contexto[0]));
    }

    [Fact]
    public void Construir_UltimoMensajeEsLaPreguntaActual()
    {
        var historial = CrearHistorial(turnosPrevios: 10);

        var contexto = GestorContexto.Construir(historial, maxTurnos: 2);

        Assert.IsType<UserChatMessage>(contexto[^1]);
        Assert.Equal("pregunta actual", Texto(contexto[^1]));
    }

    [Fact]
    public void Construir_HistorialCorto_EnviaTodo()
    {
        var historial = CrearHistorial(turnosPrevios: 1);   // sistema + 2 + pregunta = 4 mensajes

        var contexto = GestorContexto.Construir(historial, maxTurnos: 3);

        Assert.Equal(historial.Count, contexto.Count);
    }

    [Theory]
    [InlineData(10, 0, 2)]   // sistema + pregunta actual
    [InlineData(10, 1, 4)]   // sistema + 1 turno (2) + pregunta actual
    [InlineData(10, 3, 8)]   // sistema + 3 turnos (6) + pregunta actual
    [InlineData(2, 5, 6)]    // ventana mayor que el historial: se envía todo
    public void Construir_RespetaElTamanoDeLaVentana(int turnosPrevios, int maxTurnos, int esperados)
    {
        var historial = CrearHistorial(turnosPrevios);

        var contexto = GestorContexto.Construir(historial, maxTurnos);

        Assert.Equal(esperados, contexto.Count);
    }

    [Fact]
    public void Construir_ConservaLosTurnosMasRecientes()
    {
        var historial = CrearHistorial(turnosPrevios: 10);

        var contexto = GestorContexto.Construir(historial, maxTurnos: 2);

        // Deben quedar los turnos 9 y 10, no los primeros
        Assert.Equal("pregunta 9", Texto(contexto[1]));
        Assert.Equal("respuesta 10", Texto(contexto[4]));
        Assert.DoesNotContain(contexto, m => Texto(m) == "pregunta 1");
    }

    [Fact]
    public void Construir_LaVentanaEmpiezaEnUnaPreguntaDelUsuario()
    {
        var historial = CrearHistorial(turnosPrevios: 10);

        var contexto = GestorContexto.Construir(historial, maxTurnos: 3);

        // El primer mensaje después del de sistema nunca debe ser una respuesta suelta
        Assert.IsType<UserChatMessage>(contexto[1]);
    }

    [Fact]
    public void Construir_NoModificaElHistorialOriginal()
    {
        var historial = CrearHistorial(turnosPrevios: 10);
        int cantidadAntes = historial.Count;

        GestorContexto.Construir(historial, maxTurnos: 1);

        Assert.Equal(cantidadAntes, historial.Count);
    }

    [Fact]
    public void Construir_SinPromptDeSistema_LanzaExcepcion()
    {
        var historial = new List<ChatMessage> { new UserChatMessage("hola") };

        Assert.Throws<ArgumentException>(() => GestorContexto.Construir(historial, maxTurnos: 3));
    }

    [Fact]
    public void Construir_SinPreguntaAlFinal_LanzaExcepcion()
    {
        var historial = CrearHistorial(turnosPrevios: 1);
        historial.Add(new AssistantChatMessage("respuesta sin pregunta"));

        Assert.Throws<ArgumentException>(() => GestorContexto.Construir(historial, maxTurnos: 3));
    }

    [Fact]
    public void Construir_MaxTurnosNegativo_LanzaExcepcion()
    {
        var historial = CrearHistorial(turnosPrevios: 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => GestorContexto.Construir(historial, maxTurnos: -1));
    }
}
