namespace UvcCaptureSim.Core.Sinks;

/// <summary>A virtual camera could not be started. The message is safe to show in the UI.</summary>
public sealed class VirtualCameraException : Exception
{
    public VirtualCameraException(string message) : base(message)
    {
    }

    public VirtualCameraException(string message, Exception inner) : base(message, inner)
    {
    }
}
