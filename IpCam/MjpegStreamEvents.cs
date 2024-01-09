namespace IpCam
{
    using System;

    public delegate void NewFrameEventHandler( object sender, NewFrameEventArgs eventArgs );
    public delegate void NewByteArrayEventHandler(object sender, NewByteArrayEventArgs eventArgs);
    public delegate void VideoSourceErrorEventHandler( object sender, VideoSourceErrorEventArgs eventArgs );    

    /// <summary>
    /// Arguments for new frame event from video source.
    /// </summary>
    /// 
    public class NewFrameEventArgs : EventArgs
    {
        private System.Drawing.Bitmap frame;

        /// <summary>
        /// Initializes a new instance of the <see cref="NewFrameEventArgs"/> class.
        /// </summary>
        /// 
        /// <param name="frame">New frame.</param>
        /// 
        public NewFrameEventArgs( System.Drawing.Bitmap frame )
        {
            this.frame = frame;
        }
                
        /// <summary>
        /// New frame from video source.
        /// </summary>
        /// 
        public System.Drawing.Bitmap Frame
        {
            get { return frame; }
        }
    }

    /// <summary>
    /// Arguments for new frame event from video source.
    /// </summary>
    /// 
    public class NewByteArrayEventArgs : EventArgs
    {
        private byte[] byteArrayFrame;

        /// <summary>
        /// Initializes a new instance of the <see cref="NewFrameEventArgs"/> class.
        /// </summary>
        /// 
        /// <param name="frame">New frame.</param>
        /// 
        public NewByteArrayEventArgs(byte[] byteArrayFrame)
        {
            this.byteArrayFrame = byteArrayFrame;
        }

        /// <summary>
        /// New frame from video source.
        /// </summary>
        /// 
        public byte[] ByteArrayFrame
        {
            get { return this.byteArrayFrame; }
        }
    }

    /// <summary>
    /// Arguments for video source error event from video source.
    /// </summary>
    /// 
    public class VideoSourceErrorEventArgs : EventArgs
    {
        private string description;

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoSourceErrorEventArgs"/> class.
        /// </summary>
        /// 
        /// <param name="description">Error description.</param>
        /// 
        public VideoSourceErrorEventArgs( string description )
        {
            this.description = description;
        }
        
        /// <summary>
        /// Video source error description.
        /// </summary>
        /// 
        public string Description
        {
            get { return description; }
        }        
    }
}
