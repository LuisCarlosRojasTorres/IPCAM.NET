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
        /// <summary>
        /// New frame event.
        /// </summary>
        /// 
        /// <remarks><para>This event is used to notify clients about new available video frame.</para>
        /// 
        /// <para><note>Since video source may have multiple clients, each client is responsible for
        /// making a copy (cloning) of the passed video frame, but video source is responsible for
        /// disposing its own original copy after notifying of clients.</note></para>
        /// </remarks>
        /// 
        event NewFrameEventHandler NewFrame;

        /// <summary>
        /// Video source error event.
        /// </summary>
        /// 
        /// <remarks>This event is used to notify clients about any type of errors occurred in
        /// video source object, for example internal exceptions.</remarks>
        /// 
        event VideoSourceErrorEventHandler VideoSourceError;

        /// <summary>
        /// Video playing finished event.
        /// </summary>
        /// 
        /// <remarks><para>This event is used to notify clients that the video playing has finished.</para>
        /// </remarks>
        /// 
        event PlayingFinishedEventHandler PlayingFinished;

        /// <summary>
        /// Video source.
        /// </summary>
        /// 
        /// <remarks>The meaning of the property depends on particular video source.
        /// Depending on video source it may be a file name, URL or any other string
        /// describing the video source.</remarks>
        /// 
        string IpCamUrl { get; }

        /// <summary>
        /// Received frames count.
        /// </summary>
        /// 
        /// <remarks>Number of frames the video source provided from the moment of the last
        /// access to the property.
        /// </remarks>
        /// 
        int FramesReceived { get; }        

        /// <summary>
        /// State of the video source.
        /// </summary>
        /// 
        /// <remarks>Current state of video source object - running or not.</remarks>
        /// 
        bool IsRunning { get; }

        /// <summary>
        /// Start video source.
        /// </summary>
        /// 
        /// <remarks>Starts video source and return execution to caller. Video source
        /// object creates background thread and notifies about new frames with the
        /// help of <see cref="NewFrame"/> event.</remarks>
        /// 
        void Start( );        
        
    }
}
