using System.Runtime.InteropServices;

namespace UvcCaptureSim.Core.Sinks;

/// <summary>
/// cdecl bindings for the softcam sender API. The C++ <c>bool</c> results are one byte.
/// </summary>
internal static partial class SoftcamInterop
{
    private const string Dll = "softcam.dll";

    [LibraryImport(Dll)]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial IntPtr scCreateCamera(int width, int height, float framerate);

    [LibraryImport(Dll)]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial void scDeleteCamera(IntPtr camera);

    [LibraryImport(Dll)]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial void scSendFrame(IntPtr camera, IntPtr imageBits);

    [LibraryImport(Dll)]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool scWaitForConnection(IntPtr camera, float timeout);

    [LibraryImport(Dll)]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool scIsConnected(IntPtr camera);
}
