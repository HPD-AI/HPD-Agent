using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Swift;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

/// <summary>
/// Swift interop declarations for Apple's AVSpeechSynthesizer and NSSpeechSynthesizer APIs
/// </summary>
public static partial class AVSpeechSynthesisInterop
{
    [LibraryImport("AVFoundation")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool IsAvailable();

    [LibraryImport("AVFoundation")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial SpeechSynthesizerHandle CreateSynthesizer();

    [LibraryImport("AVFoundation")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial SpeechUtteranceHandle CreateUtterance(SwiftString text);

    [LibraryImport("AVFoundation")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial void SetVoice(SpeechUtteranceHandle utterance, SwiftString voiceId);

    [LibraryImport("AVFoundation")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial void SetRate(SpeechUtteranceHandle utterance, float rate);

    [LibraryImport("AVFoundation")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial void SetPitchMultiplier(SpeechUtteranceHandle utterance, float pitch);

    [LibraryImport("AVFoundation")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial void SetVolume(SpeechUtteranceHandle utterance, float volume);

    [LibraryImport("AVFoundation")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial void SynthesizeUtterance(
        SpeechSynthesizerHandle synthesizer,
        SpeechUtteranceHandle utterance,
        SpeechSynthesisHandler onComplete);

    [LibraryImport("AVFoundation")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial void StartStreamingSynthesis(
        SpeechSynthesizerHandle synthesizer,
        SpeechUtteranceHandle utterance,
        StreamingSpeechSynthesisHandler onAudioChunk);

    [LibraryImport("AVFoundation")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial void StopStreamingSynthesis(SpeechSynthesizerHandle synthesizer);

    [LibraryImport("AVFoundation")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial void DisposeSynthesizer(SpeechSynthesizerHandle synthesizer);

    [LibraryImport("AVFoundation")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial void DisposeUtterance(SpeechUtteranceHandle utterance);

    [LibraryImport("AVFoundation")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial void DisposeAudioData(IntPtr audioData);
}

// Handle types for TTS
public readonly struct SpeechSynthesizerHandle
{
    public readonly IntPtr Handle;
    public SpeechSynthesizerHandle(IntPtr handle) => Handle = handle;
}

public readonly struct SpeechUtteranceHandle
{
    public readonly IntPtr Handle;
    public SpeechUtteranceHandle(IntPtr handle) => Handle = handle;
}

// Delegate types for TTS
public delegate void SpeechSynthesisHandler(IntPtr audioData, int audioSize, IntPtr error);

public delegate void StreamingSpeechSynthesisHandler(
    IntPtr audioChunk,
    int chunkSize,
    [MarshalAs(UnmanagedType.I1)] bool isComplete,
    IntPtr error);