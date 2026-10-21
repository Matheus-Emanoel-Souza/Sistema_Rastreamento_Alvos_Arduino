using System;
using System.Globalization;
using System.Speech.Synthesis;
using System.Threading.Tasks;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação de <see cref="IAlertaSonoroService"/>: alarme sonoro via
/// <see cref="Console.Beep(int, int)"/> (API do Windows, sem arquivo de áudio nem dependência
/// nova) seguido do anúncio por voz via <see cref="SpeechSynthesizer"/> (SAPI do Windows,
/// builtin). Ambos rodam numa <see cref="Task.Run"/> dedicada — nunca na thread de UI, e uma
/// falha de TTS (ex.: nenhuma voz pt-BR instalada) só é logada, nunca propagada.
/// </summary>
public sealed class AlertaSonoroService : IAlertaSonoroService
{
    private readonly ILoggingService _logger;

    public AlertaSonoroService(ILoggingService logger)
    {
        _logger = logger;
    }

    public void Disparar(double distanciaMetros)
    {
        Task.Run(() =>
        {
            TocarAlarme();
            AnunciarDistancia(distanciaMetros);
        });
    }

    private void TocarAlarme()
    {
        try
        {
            for (int i = 0; i < 3; i++)
            {
                Console.Beep(1800, 200);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning($"Não foi possível tocar o alarme sonoro: {ex.Message}");
        }
    }

    private void AnunciarDistancia(double distanciaMetros)
    {
        try
        {
            string texto = $"Alvo identificado. Distância: {distanciaMetros.ToString("0.0", CultureInfo.InvariantCulture)} metros.";

            using var synth = new SpeechSynthesizer();
            synth.SelectVoiceByHints(VoiceGender.NotSet, VoiceAge.NotSet);
            synth.Speak(texto);
        }
        catch (Exception ex)
        {
            // Melhor esforço — SAPI pode não ter nenhuma voz instalada no Windows do usuário.
            _logger.Warning($"Não foi possível anunciar a distância por voz: {ex.Message}");
        }
    }
}
