namespace IpCam
{
    using System;

    /// <summary>
    /// Video source interface.
    /// </summary>
    /// 
    /// <remarks>The interface describes common methods for different type of video sources.</remarks>
    /// 
    public interface IMjpegStream
    {
        event NewFrameEventHandler NewFrame;
        event NewByteArrayEventHandler NewByteArray;
        event VideoSourceErrorEventHandler VideoSourceError;

        int FramesReceived { get; }        
        void Start( );
        void Stop();

    }
}
