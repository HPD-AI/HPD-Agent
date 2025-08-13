using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Swift;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

/// <summary>
/// Swift interop declarations for Apple's SpeechAnalyzer and SpeechTranscriber APIs
/// </summary>
public static partial class SpeechAnalyzerInterop
{
    [LibraryImport("Speech")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool IsAvailable();

    [LibraryImport("Speech")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial SpeechTranscriberHandle CreateTranscriber(
        SwiftString locale, 
        int preset);

    [LibraryImport("Speech")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial SpeechAnalyzerHandle CreateAnalyzer(
        SpeechTranscriberHandle[] transcribers);

    [LibraryImport("Speech")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial AudioDataHandle CreateAudioData(
        byte[] audioBytes, 
        int length);

    [LibraryImport("Speech")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial void AnalyzeAudio(
        SpeechAnalyzerHandle analyzer,
        AudioDataHandle audioData,
        SpeechAnalysisHandler onComplete);

    // ===== FIXED: Use callback-based streaming instead of IAsyncEnumerable =====
    [LibraryImport("Speech")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial void StartStreamingAnalysis(
        SpeechAnalyzerHandle analyzer,
        AudioDataHandle audioData,
        StreamingSpeechAnalysisHandler onResult);

    [LibraryImport("Speech")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial void StopStreamingAnalysis(SpeechAnalyzerHandle analyzer);

    [LibraryImport("Speech")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial void DisposeAnalyzer(SpeechAnalyzerHandle analyzer);

    [LibraryImport("Speech")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial void DisposeTranscriber(SpeechTranscriberHandle transcriber);

    [LibraryImport("Speech")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial void DisposeAudioData(AudioDataHandle audioData);
}

// ===== FIXED DELEGATES =====
public delegate void SpeechAnalysisHandler(SwiftString transcription, IntPtr error);

public delegate void StreamingSpeechAnalysisHandler(
    SwiftString transcription, 
    [MarshalAs(UnmanagedType.I1)] bool isFinal,
    float confidence,
    IntPtr error);

// Handle types
public readonly struct SpeechAnalyzerHandle
{
    public readonly IntPtr Handle;
    public SpeechAnalyzerHandle(IntPtr handle) => Handle = handle;
}

public readonly struct SpeechTranscriberHandle  
{
    public readonly IntPtr Handle;
    public SpeechTranscriberHandle(IntPtr handle) => Handle = handle;
}

public readonly struct AudioDataHandle
{
    public readonly IntPtr Handle;
    public AudioDataHandle(IntPtr handle) => Handle = handle;
}
