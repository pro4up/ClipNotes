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

    private record MarkerWork(
        Marker Marker,
        string BaseName,
        string Ext,
        string FinalClipPath,
        string? TranscribeWavPath,
        bool DeleteFinalAfterTranscription,
        TimeSpan ClipStart,
        TimeSpan ClipDuration);

    public async Task RunAsync(SessionData session, CancellationToken ct = default)
    {
        var settings = session.SettingsSnapshot;
        var videoPath = session.VideoFilePath
            ?? throw new InvalidOperationException("Видеофайл не найден");

        // 1. Extract master audio (once)
        StatusChanged?.Invoke("Извлечение аудио из видео...");
        var audioDir = session.EffectiveAudioDir;
        if (string.IsNullOrEmpty(audioDir)) audioDir = Path.Combine(session.SessionFolder, "audio");
        Directory.CreateDirectory(audioDir);
        var masterAudio = Path.Combine(audioDir, "master.wav");
        await _ffmpeg.ExtractMasterAudioAsync(videoPath, masterAudio, ct);
        session.MasterAudioPath = masterAudio;

        var actualDuration = await _ffmpeg.GetDurationAsync(masterAudio, ct);
        if (actualDuration > TimeSpan.Zero)
            session.Duration = actualDuration;

        var glossary = settings.Glossary;
        if (!string.IsNullOrWhiteSpace(settings.GlossaryFilePath) && File.Exists(settings.GlossaryFilePath))
            glossary = await File.ReadAllTextAsync(settings.GlossaryFilePath, ct);

        var markersToProcess = session.Markers.Where(m => m.GenerateAudio || m.GenerateText).ToList();
        if (markersToProcess.Count == 0)
        {
            StatusChanged?.Invoke("Нет маркеров для обработки.");
            return;
        }

        // 2. Build work items (determine clip bounds and output paths)
        var works = new List<MarkerWork>(markersToProcess.Count);
        foreach (var marker in markersToProcess)
        {
            TimeSpan pre, post;
            if (marker.HoldDuration.HasValue)
            {
                pre = TimeSpan.FromSeconds(settings.HoldPreSeconds);
                post = TimeSpan.FromSeconds(settings.HoldPostSeconds);
            }
            else
            {
                pre = TimeSpan.FromSeconds(settings.PreSeconds);
                post = TimeSpan.FromSeconds(settings.PostSeconds);
            }
            var clipStart = marker.Timestamp - pre;
            if (clipStart < TimeSpan.Zero) clipStart = TimeSpan.Zero;
            var holdEnd = marker.HoldDuration.HasValue
                ? marker.Timestamp + marker.HoldDuration.Value
                : marker.Timestamp;
            var clipEnd = holdEnd + post;
            if (clipEnd > session.Duration) clipEnd = session.Duration;
            var clipDuration = clipEnd - clipStart;
            if (clipDuration <= TimeSpan.Zero) continue;

            var baseName = $"{marker.Index:D3}_{marker.Type}_{marker.Timestamp:hh\\-mm\\-ss}";
            var ext = settings.AudioCodec.ToLower() switch
            {
                "mp3" => ".mp3",
                "aac" => ".m4a",
                _ => ".wav"
            };
            var finalClipPath = Path.Combine(audioDir, baseName + ext);
            string? transcribeWavPath = null;
            bool deleteFinalAfterTranscription = false;

            if (marker.GenerateText)
            {
                if (ext == ".wav" && !marker.GenerateAudio)
                    // Text-only + WAV: cut final, delete after transcription
                    deleteFinalAfterTranscription = true;
                else if (ext != ".wav")
                    // mp3/aac: need separate temp WAV for Whisper
                    transcribeWavPath = Path.Combine(audioDir, baseName + "_tmp.wav");
                // ext==".wav" && GenerateAudio: reuse finalClipPath — no extra cut
            }

            works.Add(new MarkerWork(marker, baseName, ext, finalClipPath,
                transcribeWavPath, deleteFinalAfterTranscription, clipStart, clipDuration));
        }

        // 3. Phase 1: Cut all clips in parallel (FFmpeg is CPU/IO bound, parallelize safely)
        StatusChanged?.Invoke("Нарезка аудиоклипов...");
        await Parallel.ForEachAsync(
            works,
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
            async (work, pct) =>
            {
                // Cut final clip (if needed for audio output or text-only WAV)
                if (work.Marker.GenerateAudio || (work.Marker.GenerateText && work.Ext == ".wav"))
                {
                    await _ffmpeg.ExtractAudioClipAsync(masterAudio, work.FinalClipPath,
                        work.ClipStart, work.ClipDuration, settings.AudioCodec, settings.AudioBitrate, pct);
                }

                // Cut temp WAV for transcription (mp3/aac case)
                if (work.TranscribeWavPath != null)
                {
                    await _ffmpeg.ExtractAudioClipAsync(masterAudio, work.TranscribeWavPath,
                        work.ClipStart, work.ClipDuration, "wav", settings.AudioBitrate, pct);
                }
            });

        // 4. Phase 2: Transcribe sequentially (Whisper/GPU can't be shared across processes)
        for (int i = 0; i < works.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var work = works[i];
            var marker = work.Marker;

            StatusChanged?.Invoke($"Транскрипция {i + 1}/{works.Count}: {marker.Type} @ {marker.TimestampFormatted}");
            ProgressChanged?.Invoke(i, works.Count);

            if (marker.GenerateAudio)
            {
                // Store relative path if audioDir is inside sessionFolder, else absolute
                var defaultAudioDir = Path.Combine(session.SessionFolder, "audio");
                marker.AudioFilePath = audioDir == defaultAudioDir
                    ? Path.Combine("audio", work.BaseName + work.Ext)
                    : work.FinalClipPath;
            }

            if (marker.GenerateText)
            {
                var transcribePath = work.TranscribeWavPath ?? work.FinalClipPath;
                var txtDir = session.EffectiveTxtDir;
                if (string.IsNullOrEmpty(txtDir)) txtDir = Path.Combine(session.SessionFolder, "txt");
                Directory.CreateDirectory(txtDir);
                var txtBasePath = Path.Combine(txtDir, work.BaseName);

                var text = await _whisper.TranscribeSegmentAsync(
                    transcribePath, txtBasePath, settings.TranscriptionLanguage, glossary, ct);
                marker.Text = text.Trim();
                var defaultTxtDir = Path.Combine(session.SessionFolder, "txt");
                marker.TextFilePath = txtDir == defaultTxtDir
                    ? Path.Combine("txt", work.BaseName + ".txt")
                    : Path.Combine(txtDir, work.BaseName + ".txt");

                // Cleanup temp files
                if (work.TranscribeWavPath != null && File.Exists(work.TranscribeWavPath))
                    File.Delete(work.TranscribeWavPath);

                if (work.DeleteFinalAfterTranscription && File.Exists(work.FinalClipPath))
                    File.Delete(work.FinalClipPath);
            }
        }

        // 5. Generate Excel
        StatusChanged?.Invoke("Создание Excel-отчёта...");
        var tableDir = session.EffectiveTableDir;
        if (string.IsNullOrEmpty(tableDir)) tableDir = Path.Combine(session.SessionFolder, "table");
        Directory.CreateDirectory(tableDir);
        var xlsxPath = Path.Combine(tableDir, "ClipNotes.xlsx");
        _excel.GenerateReport(session, xlsxPath);

        // 6. Save meta
        StatusChanged?.Invoke("Сохранение метаданных сессии...");
        new SessionService().SaveSessionMeta(session);

        ProgressChanged?.Invoke(works.Count, works.Count);
        StatusChanged?.Invoke("Готово!");
    }
}
