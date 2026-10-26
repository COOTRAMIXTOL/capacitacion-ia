namespace AsistenteIA.Core;

/// <summary>
/// Configuración del modelo. Se llena desde la sección "AzureOpenAI" de appsettings.json
/// y se puede sobreescribir con variables de entorno (AzureOpenAI__Propiedad).
/// </summary>
public class OpcionesIA
{
    public string Endpoint { get; init; } = "";
    public string Deployment { get; init; } = "";
    public float Temperature { get; init; } = 0.2f;
    public int MaxOutputTokens { get; init; } = 300;
    public int MaxTurnosHistorial { get; init; } = 3;
}
