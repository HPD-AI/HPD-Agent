using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Swift;
using System.Runtime.CompilerServices;

/// <summary>
/// Swift interop declarations for Apple's FoundationModels framework
/// Accurately reflects the actual Apple Foundation Models Swift API
/// </summary>
public static partial class FoundationModelsInterop
{
    // SystemLanguageModel - matches Apple's actual API
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial SystemLanguageModelHandle GetDefaultSystemLanguageModel();
    
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial SystemLanguageModelHandle CreateSystemLanguageModel(SystemLanguageModelUseCase useCase);
    
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial SystemLanguageModelAvailability GetSystemLanguageModelAvailability(SystemLanguageModelHandle model);
    
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool GetSystemLanguageModelIsAvailable(SystemLanguageModelHandle model);
}

/// <summary>
/// Swift interop for LanguageModelSession - matches Apple's actual API design
/// </summary>
public static partial class LanguageModelSessionInterop
{
    // Session creation - reflecting Apple's actual initializers
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial LanguageModelSessionHandle CreateLanguageModelSession(
        SystemLanguageModelHandle model);
    
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial LanguageModelSessionHandle CreateLanguageModelSessionWithInstructions(
        SystemLanguageModelHandle model,
        SwiftString instructions);
    
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial LanguageModelSessionHandle CreateLanguageModelSessionWithTools(
        SystemLanguageModelHandle model,
        ToolHandle[] tools,
        int toolCount);
        
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial LanguageModelSessionHandle CreateLanguageModelSessionWithToolsAndInstructions(
        SystemLanguageModelHandle model,
        ToolHandle[] tools,
        int toolCount,
        SwiftString instructions);
    
    // Session management
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial void LanguageModelSessionPrewarm(LanguageModelSessionHandle session);
    
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool LanguageModelSessionIsResponding(LanguageModelSessionHandle session);
    
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial SessionTranscriptHandle LanguageModelSessionGetTranscript(LanguageModelSessionHandle session);
    
    // Response methods - matching Apple's respond(to:) API
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial void LanguageModelSessionRespondTo(
        LanguageModelSessionHandle session,
        SwiftString prompt,
        LanguageModelResponseCallback callback,
        IntPtr callbackContext);
    
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial void LanguageModelSessionRespondToWithOptions(
        LanguageModelSessionHandle session,
        SwiftString prompt,
        GenerationOptionsHandle options,
        LanguageModelResponseCallback callback,
        IntPtr callbackContext);
    
    // Structured generation - matching Apple's generating: parameter
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial void LanguageModelSessionRespondToGenerating(
        LanguageModelSessionHandle session,
        SwiftString prompt,
        SwiftString generableTypeName,
        SwiftString generableSchema,
        LanguageModelResponseCallback callback,
        IntPtr callbackContext);
        
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial void LanguageModelSessionRespondToGeneratingWithOptions(
        LanguageModelSessionHandle session,
        SwiftString prompt,
        SwiftString generableTypeName,
        SwiftString generableSchema,
        GenerationOptionsHandle options,
        [MarshalAs(UnmanagedType.I1)] bool includeSchemaInPrompt,
        LanguageModelResponseCallback callback,
        IntPtr callbackContext);
    
    // Streaming - matching Apple's streamResponse(to:) API
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial StreamingResponseHandle LanguageModelSessionStreamResponseTo(
        LanguageModelSessionHandle session,
        SwiftString prompt,
        LanguageModelStreamingCallback streamCallback,
        LanguageModelResponseCallback completionCallback,
        IntPtr callbackContext);
        
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial StreamingResponseHandle LanguageModelSessionStreamResponseToWithOptions(
        LanguageModelSessionHandle session,
        SwiftString prompt,
        GenerationOptionsHandle options,
        LanguageModelStreamingCallback streamCallback,
        LanguageModelResponseCallback completionCallback,
        IntPtr callbackContext);
    
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial StreamingResponseHandle LanguageModelSessionStreamResponseToGenerating(
        LanguageModelSessionHandle session,
        SwiftString prompt,
        SwiftString generableTypeName,
        SwiftString generableSchema,
        GenerationOptionsHandle options,
        [MarshalAs(UnmanagedType.I1)] bool includeSchemaInPrompt,
        LanguageModelStreamingCallback streamCallback,
        LanguageModelResponseCallback completionCallback,
        IntPtr callbackContext);
    
    // Resource cleanup
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial void DisposeLanguageModelSession(LanguageModelSessionHandle session);
    
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial void DisposeStreamingResponse(StreamingResponseHandle stream);
}

/// <summary>
/// Swift interop for GenerationOptions - matches Apple's API
/// </summary>
public static partial class GenerationOptionsInterop
{
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial GenerationOptionsHandle CreateGenerationOptions();
    
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial GenerationOptionsHandle CreateGenerationOptionsWithTemperature(double temperature);
    
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial GenerationOptionsHandle CreateGenerationOptionsComplete(
        double temperature,
        int maximumResponseTokens,
        GenerationSamplingMethod sampling);
    
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial void DisposeGenerationOptions(GenerationOptionsHandle options);
}

/// <summary>
/// Swift interop for Tool protocol - matches Apple's Tool design
/// </summary>
public static partial class ToolInterop
{
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial ToolHandle CreateTool(
        SwiftString name,
        SwiftString description,
        SwiftString argumentsSchema,
        ToolCallCallback callback,
        IntPtr callbackContext);
    
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial void DisposeTool(ToolHandle tool);
}

/// <summary>
/// Swift interop for SessionTranscript
/// </summary>
public static partial class SessionTranscriptInterop
{
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial int SessionTranscriptGetEntryCount(SessionTranscriptHandle transcript);
    
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial TranscriptEntryHandle SessionTranscriptGetEntry(SessionTranscriptHandle transcript, int index);
    
    [LibraryImport("FoundationModels")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static partial void DisposeSessionTranscript(SessionTranscriptHandle transcript);
}

/// <summary>
/// Handle types for Swift objects - matching Apple's actual types
/// </summary>
public readonly struct SystemLanguageModelHandle
{
    public readonly IntPtr Handle;
    public SystemLanguageModelHandle(IntPtr handle) => Handle = handle;
    public static implicit operator IntPtr(SystemLanguageModelHandle handle) => handle.Handle;
}

public readonly struct LanguageModelSessionHandle
{
    public readonly IntPtr Handle;
    public LanguageModelSessionHandle(IntPtr handle) => Handle = handle;
    public static implicit operator IntPtr(LanguageModelSessionHandle handle) => handle.Handle;
}

public readonly struct GenerationOptionsHandle
{
    public readonly IntPtr Handle;
    public GenerationOptionsHandle(IntPtr handle) => Handle = handle;
    public static implicit operator IntPtr(GenerationOptionsHandle handle) => handle.Handle;
}

public readonly struct ToolHandle
{
    public readonly IntPtr Handle;
    public ToolHandle(IntPtr handle) => Handle = handle;
    public static implicit operator IntPtr(ToolHandle handle) => handle.Handle;
}

public readonly struct SessionTranscriptHandle
{
    public readonly IntPtr Handle;
    public SessionTranscriptHandle(IntPtr handle) => Handle = handle;
    public static implicit operator IntPtr(SessionTranscriptHandle handle) => handle.Handle;
}

public readonly struct StreamingResponseHandle
{
    public readonly IntPtr Handle;
    public StreamingResponseHandle(IntPtr handle) => Handle = handle;
    public static implicit operator IntPtr(StreamingResponseHandle handle) => handle.Handle;
}

public readonly struct TranscriptEntryHandle
{
    public readonly IntPtr Handle;
    public TranscriptEntryHandle(IntPtr handle) => Handle = handle;
    public static implicit operator IntPtr(TranscriptEntryHandle handle) => handle.Handle;
}

/// <summary>
/// System language model availability - matches Apple's actual enum
/// </summary>
public enum SystemLanguageModelAvailability : int
{
    Available = 0,
    UnavailableAppleIntelligenceNotEnabled = 1,
    UnavailableDeviceNotEligible = 2,
    UnavailableModelNotReady = 3,
    UnavailableUnsupportedLanguage = 4,
    UnavailableUnknown = 99
}

/// <summary>
/// System language model use cases - based on Apple's documentation
/// </summary>
public enum SystemLanguageModelUseCase : int
{
    General = 0,
    ContentTagging = 1
}

/// <summary>
/// Generation sampling methods - matches Apple's GenerationOptions
/// </summary>
public enum GenerationSamplingMethod : int
{
    Random = 0,
    Greedy = 1
}

/// <summary>
/// Swift string interop with proper UTF-8 handling
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct SwiftString
{
    private readonly IntPtr _ptr;
    private readonly long _length;
    
    public static implicit operator SwiftString(string str)
    {
        if (string.IsNullOrEmpty(str))
            return new SwiftString(IntPtr.Zero, 0);
            
        var utf8Bytes = System.Text.Encoding.UTF8.GetBytes(str);
        var ptr = Marshal.AllocHGlobal(utf8Bytes.Length + 1); // +1 for null terminator
        Marshal.Copy(utf8Bytes, 0, ptr, utf8Bytes.Length);
        Marshal.WriteByte(ptr, utf8Bytes.Length, 0); // null terminator
        return new SwiftString(ptr, utf8Bytes.Length);
    }
    
    public static implicit operator string(SwiftString swiftStr)
    {
        if (swiftStr._ptr == IntPtr.Zero || swiftStr._length == 0)
            return string.Empty;
            
        var utf8Bytes = new byte[swiftStr._length];
        Marshal.Copy(swiftStr._ptr, utf8Bytes, 0, (int)swiftStr._length);
        return System.Text.Encoding.UTF8.GetString(utf8Bytes);
    }
    
    private SwiftString(IntPtr ptr, long length)
    {
        _ptr = ptr;
        _length = length;
    }
    
    public void Dispose()
    {
        if (_ptr != IntPtr.Zero)
            Marshal.FreeHGlobal(_ptr);
    }
}

/// <summary>
/// Callback delegates matching Apple's async patterns
/// </summary>
public delegate void LanguageModelResponseCallback(
    SwiftString response, 
    double duration,
    AppleIntelligenceErrorCode errorCode,
    SwiftString errorMessage,
    IntPtr context);

public delegate void LanguageModelStreamingCallback(
    SwiftString partialResponse,
    [MarshalAs(UnmanagedType.I1)] bool isComplete,
    IntPtr context);

public delegate void ToolCallCallback(
    SwiftString argumentsJson,
    IntPtr resultCallback,
    IntPtr context);

/// <summary>
/// Apple Intelligence error codes based on research
/// </summary>
public enum AppleIntelligenceErrorCode : int
{
    None = 0,
    UnsupportedLanguage = 1,
    ContextWindowExceeded = 2,
    GuardrailViolation = 3,
    ModelUnavailable = 4,
    ToolCallError = 5,
    InvalidSchema = 6,
    GenerationCancelled = 7,
    SessionDisposed = 8,
    InvalidGenerableType = 9,
    Unknown = 999
}