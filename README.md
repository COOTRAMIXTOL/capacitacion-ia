# Asistente institucional con Azure OpenAI

[![CI](https://github.com/COOTRAMIXTOL/capacitacion-ia/actions/workflows/ci.yml/badge.svg)](https://github.com/COOTRAMIXTOL/capacitacion-ia/actions/workflows/ci.yml)

Aplicación de consola en **.NET 10** que integra un modelo GPT desplegado en **Azure OpenAI (Microsoft Foundry)** para orientar sobre trámites mineros en Colombia. El proyecto se construyó como práctica guiada de un perfil *Full Stack AI* y aplica las prácticas que se esperan en una solución institucional: autenticación sin claves, control del consumo de tokens, manejo de errores de IA, pruebas unitarias e integración continua.

> Proyecto de aprendizaje. Las respuestas del modelo no constituyen orientación oficial; para información vigente consulte los canales de la Agencia Nacional de Minería (ANM).

## Funcionalidades

- **Chat con historial** sobre un modelo GPT desplegado en Azure.
- **Autenticación sin claves** con Microsoft Entra ID (`DefaultAzureCredential`) y rol de plano de datos *Cognitive Services OpenAI User*.
- **Configuración externa** en `appsettings.json`, sobreescribible por variables de entorno según el ambiente (desarrollo, pruebas, producción).
- **Prompt de sistema configurable**, versionado fuera del código.
- **Grounding de la fecha actual** para evitar que el modelo invente datos que no conoce.
- **Streaming** de respuestas, con métricas de tiempo hasta el primer token.
- **Ventana deslizante del historial**: controla el crecimiento de los tokens de entrada.
- **Warm-up** de autenticación y conexión al iniciar.
- **Manejo de errores de IA**: filtro de contenido y *jailbreak* (Prompt Shields), cuota excedida (429) y permisos (401/403), con mensajes en español para el usuario.

## Arquitectura

```
capacitacion-ia/
├── ConsolaAzureOpenAI/            # Aplicación de consola
│   ├── Program.cs                 # Flujo del chat, streaming y manejo de errores
│   ├── GestorContexto.cs          # Lógica de la ventana deslizante (probable sin Azure)
│   └── appsettings.json           # Configuración: endpoint, despliegue, parámetros y prompt
├── ConsolaAzureOpenAI.Tests/      # Pruebas unitarias con xUnit
├── .github/workflows/ci.yml       # Integración continua en GitHub Actions
└── CapacitacionIA.slnx            # Solución
```

La lógica que decide qué parte del historial se envía al modelo está aislada en `GestorContexto`, de modo que se prueba sin llamar al servicio de IA: las pruebas son rápidas, gratuitas y deterministas.

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
- Un recurso de Azure OpenAI o Microsoft Foundry con un modelo GPT desplegado.
- Su usuario con el rol **Cognitive Services OpenAI User** sobre ese recurso.

## Configuración

Edite `ConsolaAzureOpenAI/appsettings.json`:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://<su-recurso>.openai.azure.com/",
    "Deployment": "<nombre-del-despliegue>",
    "Temperature": 0.2,
    "MaxOutputTokens": 300,
    "MaxTurnosHistorial": 3
  }
}
```

Cualquier valor se puede sobreescribir con variables de entorno usando doble guion bajo, por ejemplo:

```powershell
$env:AzureOpenAI__MaxTurnosHistorial = "1"
```

El repositorio **no contiene claves ni secretos**: la autenticación usa la identidad del usuario (Azure CLI o Visual Studio) en desarrollo, y debe usar una identidad administrada en Azure.

## Ejecución

```powershell
az login
cd ConsolaAzureOpenAI
dotnet run
```

Cada respuesta muestra métricas de latencia, mensajes enviados y consumo de tokens:

```
[primer texto: 812 ms | respuesta completa: 844 ms | fragmentos con texto: 32]
[mensajes enviados al modelo: 8 de 10 en el historial]
[entrada: 227 | salida: 22 | fin: Stop | acumulado sesión: 1032]
```

## Pruebas

```powershell
dotnet test
```

13 pruebas unitarias sobre `GestorContexto`: tamaño de la ventana, conservación del prompt de sistema, inicio de la ventana en una pregunta del usuario, inmutabilidad del historial y validaciones de entrada. Se verificó su efectividad con una prueba de mutación (al alterar la lógica, fallan 5 pruebas).

## Integración continua

Cada pull request hacia `main` o `develop` ejecuta en GitHub Actions la restauración, la compilación en Release y las pruebas unitarias. El flujo de trabajo usa ramas `feature/*`, pull requests y *squash merge*.

## Resultados medidos

| Mejora | Antes | Después |
|---|---|---|
| Tiempo hasta el primer texto, primera pregunta (warm-up) | 4 886 ms | 812 ms |
| Tokens de entrada en el turno 5 (ventana de 3 turnos) | Crecían en cada turno | Estables (~230) |
| Tokens de una respuesta con prompt institucional frente a genérico (playground) | 507 | 444 |

## Lecciones aprendidas

- Tener permisos de **administración** sobre un recurso no da permiso de **uso del modelo**: se requiere un rol de plano de datos.
- El modelo **no tiene reloj ni memoria**: cada llamada debe incluir el contexto necesario, y el historial cuesta tokens.
- Los modelos **alucinan con seguridad** (fechas, plazos legales); en un contexto institucional se requiere RAG sobre fuentes oficiales.
- **Medir antes de suponer**: la latencia inicial no venía del modelo, sino de obtener el token de autenticación.

## Próximos pasos

- [ ] RAG sobre el Código de Minas con citas de la fuente.
- [ ] Conjunto dorado de preguntas y evaluación sistemática de la calidad de las respuestas.
- [ ] Resumen automático de los turnos antiguos del historial.
- [ ] API REST en ASP.NET Core y frontend en Angular.

## Autor

**Rodrigo Ortiz López** · Bogotá, Colombia
