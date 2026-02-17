using System.IO;
using ClipNotes.Models;

namespace ClipNotes.Services;

public class PipelineService
{
    private readonly FFmpegService _ffmpeg;
    private readonly WhisperService _whisper;
    private readonly ExcelService _excel;

    public event Action<string>? StatusChanged;
    public event Action<int, int>? ProgressChanged;

    public PipelineService(FFmpegService ffmpeg, WhisperService whisper, ExcelService excel)
    {
        _ffmpeg = ffmpeg;
        _whisper = whisper;
        _excel = excel;
    }

    public async Task RunAsync(SessionData session, CancellationToken ct = default)
    {
        var settings = session.SettingsSnapshot;
        var videoPath = session.VideoFilePath
            ?? throw new InvalidOperationException("Видеофайл не найден");

        // 1. Extract master audio
        StatusChanged?.Invoke("Извлечение аудио из видео...");
        var masterAudio = Path.Combine(session.SessionFolder, "audio", "master.wav");
        await _ffmpeg.ExtractMasterAudioAsync(videoPath, masterAudio, ct);
        session.MasterAudioPath = masterAudio;

        // Get actual duration from master audio
        var actualDuration = await _ffmpeg.GetDurationAsync(masterAudio, ct);
        if (actualDuration > TimeSpan.Zero)
            session.Duration = actualDuration;

        var glossary = settings.Glossary;
        if (!string.IsNullOrWhiteSpace(settings.GlossaryFilePath) && File.Exists(settings.GlossaryFilePath))
            glossary = await File.ReadAllTextAsync(settings.GlossaryFilePath, ct);

        // 2. Process each marker
        var markersToProcess = session.Markers.Where(m => m.GenerateAudio || m.GenerateText).ToList();
        for (int i = 0; i < markersToProcess.Count; i++)
        {
            var marker = markersToProcess[i];
            ct.ThrowIfCancellationRequested();

            StatusChanged?.Invoke($"Обработка маркера {i + 1}/{markersToProcess.Count}: {marker.Type} @ {marker.TimestampFormatted}");
            ProgressChanged?.Invoke(i, markersToProcess.Count);

            var pre = TimeSpan.FromSeconds(settings.PreSeconds);
            var post = TimeSpan.FromSeconds(settings.PostSeconds);

            var clipStart = marker.Timestamp - pre;
            if (clipStart < TimeSpan.Zero) clipStart = TimeSpan.Zero;

            var clipEnd = marker.Timestamp + post;
            if (clipEnd > session.Duration) clipEnd = session.Duration;

            var clipDuration = clipEnd - clipStart;
            if (clipDuration <= TimeSpan.Zero) continue;

            var baseName = $"{marker.Index:D3}_{marker.Type}_{marker.Timestamp:hh\\-mm\\-ss}";

            // Audio clip
            if (marker.GenerateAudio)
            {
                var ext = settings.AudioCodec.ToLower() switch
                {
                    "mp3" => ".mp3",
                    "aac" => ".m4a",
                    _ => ".wav"
                };
                var audioClipPath = Path.Combine(session.SessionFolder, "audio", baseName + ext);
                await _ffmpeg.ExtractAudioClipAsync(masterAudio, audioClipPath,
                    clipStart, clipDuration, settings.AudioCodec, settings.AudioBitrate, ct);
                marker.AudioFilePath = Path.Combine("audio", baseName + ext);
            }

            // Transcription
            if (marker.GenerateText)
            {
                var txtBasePath = Path.Combine(session.SessionFolder, "txt", baseName);
                var text = await _whisper.TranscribeSegmentAsync(
                    masterAudio, clipStart, clipDuration,
                    txtBasePath, settings.TranscriptionLanguage, glossary, ct);
                marker.Text = text.Trim();
                marker.TextFilePath = Path.Combine("txt", baseName + ".txt");
            }
        }

        // 3. Generate Excel
        StatusChanged?.Invoke("Создание Excel-отчёта...");
        var xlsxPath = Path.Combine(session.SessionFolder, "table", "ClipNotes.xlsx");
        _excel.GenerateReport(session, xlsxPath);

        // 4. Save meta
        StatusChanged?.Invoke("Сохранение метаданных сессии...");
        new SessionService().SaveSessionMeta(session);

        ProgressChanged?.Invoke(markersToProcess.Count, markersToProcess.Count);
        StatusChanged?.Invoke("Готово!");
    }
}
