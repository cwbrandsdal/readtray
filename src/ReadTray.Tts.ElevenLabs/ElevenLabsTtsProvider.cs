using System.Net.Http.Headers;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReadTray.Core;

namespace ReadTray.Tts.ElevenLabs;

public sealed class ElevenLabsTtsProvider : ITtsProvider
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<ElevenLabsTtsProvider> _logger;
    private readonly HttpClient _httpClient;

    public ElevenLabsTtsProvider(ISettingsService settingsService, ILogger<ElevenLabsTtsProvider> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
        _httpClient = new HttpClient { BaseAddress = new Uri("https://api.elevenlabs.io/v1/") };
    }
     
    public string Id => "elevenlabs";
    public string DisplayName => "ElevenLabs";

    public async Task<IReadOnlyList<TtsVoice>> GetVoicesAsync(CancellationToken ct)
    {
        var settings = await _settingsService.LoadAsync(ct);
        if (string.IsNullOrWhiteSpace(settings.ElevenLabsApiKey))
        {
            return Array.Empty<TtsVoice>();
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "voices");
        request.Headers.Add("xi-api-key", settings.ElevenLabsApiKey);
        _logger.LogInformation("Fetching ElevenLabs voices.");
        using var response = await _httpClient.SendAsync(request, ct);
        await EnsureSuccessWithBodyAsync(response, "ElevenLabs voices", ct);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var voices = document.RootElement.GetProperty("voices")
            .EnumerateArray()
            .Select(v => new TtsVoice(v.GetProperty("voice_id").GetString()!, v.GetProperty("name").GetString() ?? "Voice"))
            .ToArray();
        _logger.LogInformation("Fetched ElevenLabs voices. Count={VoiceCount}", voices.Length);
        return voices;
    }

    public async IAsyncEnumerable<AudioChunk> StreamSpeechAsync(TtsRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        var settings = await _settingsService.LoadAsync(ct);
        if (string.IsNullOrWhiteSpace(settings.ElevenLabsApiKey))
        {
            throw new InvalidOperationException("ElevenLabs API key is not configured.");
        }

        var voiceId = !string.IsNullOrWhiteSpace(settings.ElevenLabsCustomVoiceId)
            ? settings.ElevenLabsCustomVoiceId.Trim()
            : string.IsNullOrWhiteSpace(request.VoiceId)
                ? settings.SelectedVoiceByProvider.GetValueOrDefault(Id)
                : request.VoiceId;
        if (string.IsNullOrWhiteSpace(voiceId))
        {
            throw new InvalidOperationException("Select an ElevenLabs voice in settings before using ElevenLabs.");
        }

        var providerSpeed = ClampElevenLabsSpeed(request.Speed);
        if (Math.Abs(providerSpeed - request.Speed) > 0.001)
        {
            _logger.LogInformation("Clamped ElevenLabs speed from {RequestedSpeed} to {ProviderSpeed}. ElevenLabs supports 0.7-1.2.", request.Speed, providerSpeed);
        }

        var modelId = string.IsNullOrWhiteSpace(request.ModelId) ? settings.ElevenLabsModelId : request.ModelId;
        var payload = JsonSerializer.Serialize(new
        {
            text = request.Text,
            model_id = modelId,
            voice_settings = new
            {
                stability = 0.45,
                similarity_boost = 0.8,
                style = 0.0,
                use_speaker_boost = true,
                speed = providerSpeed
            }
        });

        var relativeUrl = $"text-to-speech/{Uri.EscapeDataString(voiceId)}/stream?output_format=mp3_44100_128&optimize_streaming_latency=1";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, relativeUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Add("xi-api-key", settings.ElevenLabsApiKey);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/mpeg"));

        _logger.LogInformation(
            "Starting ElevenLabs streaming request. VoiceId={VoiceId} ModelId={ModelId} TextLength={Length} Speed={Speed} Url={Url}",
            voiceId,
            modelId,
            request.Text.Length,
            providerSpeed,
            relativeUrl);
        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        await EnsureSuccessWithBodyAsync(response, "ElevenLabs stream speech", ct);
        _logger.LogInformation("ElevenLabs stream response accepted. StatusCode={StatusCode} ContentType={ContentType}",
            (int)response.StatusCode,
            response.Content.Headers.ContentType?.ToString());
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[16 * 1024];
        var totalBytes = 0L;
        while (true)
        {
            var read = await stream.ReadAsync(buffer, ct);
            if (read == 0)
            {
                _logger.LogInformation("ElevenLabs stream completed. Bytes={Bytes}", totalBytes);
                yield break;
            }

            totalBytes += read;
            _logger.LogTrace("ElevenLabs audio chunk received. Bytes={Bytes} TotalBytes={TotalBytes}", read, totalBytes);
            yield return new AudioChunk(buffer[..read].ToArray(), "audio/mpeg");
        }
    }

    private static double ClampElevenLabsSpeed(double speed)
    {
        return Math.Clamp(Math.Round(speed, 2), 0.7, 1.2);
    }

    private async Task EnsureSuccessWithBodyAsync(HttpResponseMessage response, string operation, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await SafeReadBodyAsync(response, ct);
        _logger.LogError(
            "{Operation} failed. StatusCode={StatusCode} Reason={ReasonPhrase} ContentType={ContentType} ResponseBody={ResponseBody}",
            operation,
            (int)response.StatusCode,
            response.ReasonPhrase,
            response.Content.Headers.ContentType?.ToString(),
            body);

        throw ElevenLabsApiException.FromResponse(operation, response.StatusCode, response.ReasonPhrase, body);
    }

    private static async Task<string> SafeReadBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            return body.Length <= 2000 ? body : body[..2000] + "...";
        }
        catch (Exception ex)
        {
            return $"<unable to read response body: {ex.GetType().Name}: {ex.Message}>";
        }
    }
}

public sealed class ElevenLabsApiException : Exception
{
    private ElevenLabsApiException(string operation, HttpStatusCode statusCode, string? reasonPhrase, string responseBody, string? providerCode, string? providerMessage, string? requestId)
        : base($"{operation} failed with HTTP {(int)statusCode} {reasonPhrase}. {responseBody}")
    {
        Operation = operation;
        StatusCode = statusCode;
        ResponseBody = responseBody;
        ProviderCode = providerCode;
        ProviderMessage = providerMessage;
        RequestId = requestId;
    }

    public string Operation { get; }
    public HttpStatusCode StatusCode { get; }
    public string ResponseBody { get; }
    public string? ProviderCode { get; }
    public string? ProviderMessage { get; }
    public string? RequestId { get; }

    public static ElevenLabsApiException FromResponse(string operation, HttpStatusCode statusCode, string? reasonPhrase, string responseBody)
    {
        string? code = null;
        string? message = null;
        string? requestId = null;
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.TryGetProperty("detail", out var detail))
            {
                if (detail.TryGetProperty("code", out var codeElement)) code = codeElement.GetString();
                if (detail.TryGetProperty("message", out var messageElement)) message = messageElement.GetString();
                if (detail.TryGetProperty("request_id", out var requestIdElement)) requestId = requestIdElement.GetString();
            }
        }
        catch
        {
            message = responseBody;
        }

        return new ElevenLabsApiException(operation, statusCode, reasonPhrase, responseBody, code, message, requestId);
    }
}
