namespace IpCam
{
    using System;

    /// <summary>
    /// Video source interface.
    /// </summary>
    /// 
    /// <remarks>The interface describes common methods for different type of video sources.</remarks>
    /// 
    public enum MjpegStreamState
    {
        Connecting = 0,
        ErrorConnecting = 10,
        Working = 1,
        ErrorWorking = 11,
        Pause = 12,
    }
}