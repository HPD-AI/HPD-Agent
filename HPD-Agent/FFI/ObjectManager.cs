using System.Collections.Concurrent;

namespace HPD_Agent.FFI;

internal static class ObjectManager
{
    private static readonly ConcurrentDictionary<IntPtr, object> s_liveObjects = new();
    private static long s_lastHandle = 0;

    public static IntPtr Add(object obj)
    {
        IntPtr handle = new IntPtr(Interlocked.Increment(ref s_lastHandle));
        s_liveObjects[handle] = obj;
        return handle;
    }

    public static T? Get<T>(IntPtr handle) where T : class
    {
        return s_liveObjects.TryGetValue(handle, out var obj) ? obj as T : null;
    }

    public static void Remove(IntPtr handle)
    {
        s_liveObjects.TryRemove(handle, out _);
    }
}
