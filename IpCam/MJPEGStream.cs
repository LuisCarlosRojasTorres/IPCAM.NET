namespace IpCam
{
	using System;
	using System.Drawing;
	using System.IO;
	using System.Text;
	using System.Threading;
	using System.Net;
    using System.Security;

    /// <summary>
    /// MJPEG video source.
    /// </summary>
    public class MjpegStream : IMjpegStream
    {
        // URL for MJPEG stream
        private string source;
        // received frames count
        private int framesReceived;
        // recieved byte count
        private long bytesReceived;
        // use separate HTTP connection group or use default
        private bool useSeparateConnectionGroup = true;
        // timeout value for web request
        private int requestTimeout = 10000;
        // if we should use basic authentication when connecting to the video source
        private bool forceBasicAuthentication = false;

        // buffer size used to download MJPEG stream
        private const int bufSize = 1024 * 1024;
        // size of portion to read at once
        private const int readSize = 1024;

		private Thread	thread = null;
		private ManualResetEvent? stopEvent = null;
		private ManualResetEvent? reloadEvent = null;

        /// <summary>
        /// New frame event.
        /// </summary>
        /// 
        /// <remarks><para>Notifies clients about new available frame from video source.</para>
        /// 
        /// <para><note>Since video source may have multiple clients, each client is responsible for
        /// making a copy (cloning) of the passed video frame, because the video source disposes its
        /// own original copy after notifying of clients.</note></para>
        /// </remarks>
        /// 
        public event NewFrameEventHandler NewFrame;

        /// <summary>
        /// Video source error event.
        /// </summary>
        /// 
        /// <remarks>This event is used to notify clients about any type of errors occurred in
        /// video source object, for example internal exceptions.</remarks>
        /// 
        public event VideoSourceErrorEventHandler VideoSourceError;

        /// <summary>
        /// Video playing finished event.
        /// </summary>
        /// 
        /// <remarks><para>This event is used to notify clients that the video playing has finished.</para>
        /// </remarks>
        /// 
        public event PlayingFinishedEventHandler PlayingFinished;

        /// <summary>
        /// Use or not separate connection group.
        /// </summary>
        /// 
        /// <remarks>The property indicates to open web request in separate connection group.</remarks>
        /// 
        public bool SeparateConnectionGroup
		{
			get { return useSeparateConnectionGroup; }
			set { useSeparateConnectionGroup = value; }
		}

        /// <summary>
        /// Video source.
        /// </summary>
        /// 
        /// <remarks>URL, which provides MJPEG stream.</remarks>
        /// 
        public string Source
		{
			get { return source; }
			set
			{
				source = value;
				// signal to reload
				if ( thread != null )
					reloadEvent.Set( );
			}
		}

        /// <summary>
        /// Received frames count.
        /// </summary>
        /// 
        /// <remarks>Number of frames the video source provided from the moment of the last
        /// access to the property.
        /// </remarks>
        /// 
        public int FramesReceived
		{
			get
			{
				int frames = framesReceived;
				framesReceived = 0;
				return frames;
			}
		}

        /// <summary>
        /// Received bytes count.
        /// </summary>
        /// 
        /// <remarks>Number of bytes the video source provided from the moment of the last
        /// access to the property.
        /// </remarks>
        /// 
        public long BytesReceived
		{
			get
			{
				long bytes = bytesReceived;
				bytesReceived = 0;
				return bytes;
			}
		}

        /// <summary>
        /// Request timeout value.
        /// </summary>
        /// 
        /// <remarks>The property sets timeout value in milliseconds for web requests.
        /// Default value is 10000 milliseconds.</remarks>
        /// 
        public int RequestTimeout
        {
            get { return requestTimeout; }
            set { requestTimeout = value; }
        }

        /// <summary>
        /// State of the video source.
        /// </summary>
        /// 
        /// <remarks>Current state of video source object - running or not.</remarks>
        /// 
        public bool IsRunning
		{
			get
			{
				if ( thread != null )
				{
                    // check thread status
					if ( thread.Join( 0 ) == false )
						return true;

					// the thread is not running, so free resources
					Free( );
				}
				return false;
			}
		}

        /// <summary>
        /// Initializes a new instance of the <see cref="MJPEGStream"/> class.
        /// </summary>
        /// 
        public MjpegStream( ) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="MJPEGStream"/> class.
        /// </summary>
        /// 
        /// <param name="source">URL, which provides MJPEG stream.</param>
        /// 
        public MjpegStream( string source )
        {
            this.source = source;
        }

        /// <summary>
        /// Start video source.
        /// </summary>
        /// 
        /// <remarks>Starts video source and return execution to caller. Video source
        /// object creates background thread and notifies about new frames with the
        /// help of <see cref="NewFrame"/> event.</remarks>
        /// 
        /// <exception cref="ArgumentException">Video source is not specified.</exception>
        /// 
        public void Start( )
		{
			if ( !IsRunning )
			{
                // check source
                if ((source == null) || (source == string.Empty))
                {
                    throw new ArgumentException("Video source is not specified.");
                }
                
                
                framesReceived = 0;
				bytesReceived = 0;

				// create events
				stopEvent	= new ManualResetEvent( false );
				reloadEvent	= new ManualResetEvent( false );

				// create and start new thread
				thread = new Thread( new ThreadStart( WorkerThread ) );
				thread.Name = source;
				thread.Start( );
			}
		}

        /// <summary>
        /// Signal video source to stop its work.
        /// </summary>
        public void SignalToStop( )
		{
			// stop thread
			if ( thread != null )
			{
				// signal to stop
				stopEvent.Set( );
			}
		}

        /// <summary>
        /// Wait for video source has stopped.
        /// </summary>
        /// 
        /// <remarks>Waits for source stopping after it was signalled to stop using
        /// <see cref="SignalToStop"/> method.</remarks>
        /// 
        public void WaitForStop( )
		{
			if ( thread != null )
			{
				// wait for thread stop
				thread.Join( );

				Free( );
			}
		}

        /// <summary>
        /// Stop video source.
        /// </summary>
        /// 
        /// <remarks><para>Stops video source aborting its thread.</para>
        /// 
        /// <para><note>Since the method aborts background thread, its usage is highly not preferred
        /// and should be done only if there are no other options. The correct way of stopping camera
        /// is <see cref="SignalToStop">signaling it stop</see> and then
        /// <see cref="WaitForStop">waiting</see> for background thread's completion.</note></para>
        /// </remarks>
        /// 
        public void Stop( )
		{
			if ( this.IsRunning )
			{
                stopEvent.Set( );
                thread.Abort();
				WaitForStop( );
			}
		}

        /// <summary>
        /// Free resource.
        /// </summary>
        /// 
        private void Free( )
		{
			thread = null;

			// release events
			stopEvent!.Close( );
			stopEvent = null;
			reloadEvent!.Close( );
			reloadEvent = null;
		}

        // Worker thread
        private async void WorkerThread( )
		{
            // buffer to read stream
            byte[] buffer = new byte[bufSize];
            // JPEG magic number
            byte[] jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF };
            int jpegHeaderLength = jpegHeader.Length;

            ASCIIEncoding encoding = new ASCIIEncoding( );

            while ( !stopEvent.WaitOne( 0, false ) )
			{
				// reset reload event
				reloadEvent.Reset( );

                HttpClient httpClient = new HttpClient();
                Stream? streamAsync = null;
                HttpResponseMessage httpClientResponse = await httpClient.GetAsync(source, HttpCompletionOption.ResponseHeadersRead);

                // boundary betweeen images (string and binary versions)
                byte[] boundary = null;
                string boudaryStr = null;
                // length of boundary
				int boundaryLen;
                // flag signaling if boundary was checked or not
                bool boundaryIsChecked = false;
                // read amounts and positions
				int read, todo = 0, total = 0, pos = 0, align = 1;
				int start = 0, stop = 0;

				// align
				//  1 = searching for image start
				//  2 = searching for image end

				try
				{
					// check content type
                    string contentType = httpClientResponse.Content.Headers.ContentType!.ToString();
                    string[] contentTypeArray = contentType.Split( '/' );

                    // "application/octet-stream"
                    if ( ( contentTypeArray[0] == "application" ) && ( contentTypeArray[1] == "octet-stream" ) )
                    {
                        boundaryLen = 0;
                        boundary = new byte[0];
                    }
                    else if ( ( contentTypeArray[0] == "multipart" ) && ( contentType.Contains( "mixed" ) ) )
                    {
                        // get boundary
                        int boundaryIndex = contentType.IndexOf( "boundary", 0 );
                        if ( boundaryIndex != -1 )
                        {
                            boundaryIndex = contentType.IndexOf( "=", boundaryIndex + 8 );
                        }

                        if ( boundaryIndex == -1 )
                        {
                            // try same scenario as with octet-stream, i.e. without boundaries
                            boundaryLen = 0;
                            boundary = new byte[0];
                        }
                        else
                        {
                            boudaryStr = contentType.Substring( boundaryIndex + 1 );
                            // remove spaces and double quotes, which may be added by some IP cameras
                            boudaryStr = boudaryStr.Trim( ' ', '"' ); // Ba4oTvQMY8ew04N8dcnM

                            boundary = encoding.GetBytes( boudaryStr ); // In HEx is 66 97 52 111 84 118 81 77 89 56 101 119 48 52 78 56 100 99 110 77 
                            boundaryLen = boundary.Length;
                            boundaryIsChecked = false;
                        }
                    }
                    else
                    {
                        throw new Exception( "Invalid content type." );
                    }

					// get response stream
                    //stream = response.GetResponseStream( );
                    //stream.ReadTimeout = requestTimeout;
                    streamAsync = await httpClient.GetStreamAsync(source); 
                    
                    
                    // loop
                    while ( ( !stopEvent.WaitOne( 0, false ) ) && ( !reloadEvent.WaitOne( 0, false ) ) )
					{
						// check total read
						if ( total > bufSize - readSize )
						{
							total = pos = todo = 0;
						}

						// read next portion from stream
						if ( ( read = streamAsync.Read( buffer, total, readSize ) ) == 0 )
							throw new ApplicationException( );

						total += read; //Todo o que foi lido ate agora
						todo += read; //O que tem que ser processado

						// increment received bytes counter
						bytesReceived += read;

                        // do we need to check boundary ?
                        if ( ( boundaryLen != 0 ) && ( !boundaryIsChecked ) )
                        {
                            // some IP cameras, like AirLink, claim that boundary is "myboundary",
                            // when it is really "--myboundary". this needs to be corrected.

                            pos = ByteArrayUtils.Find( buffer, boundary, 0, todo );
                            // continue reading if boudary was not found
                            if ( pos == -1 )
                                continue;

                            for ( int i = pos - 1; i >= 0; i-- )
                            {
                                byte ch = buffer[i];

                                if ( ( ch == (byte) '\n' ) || ( ch == (byte) '\r' ) )
                                {
                                    break;
                                }

                                boudaryStr = (char) ch + boudaryStr;
                            }

                            boundary = encoding.GetBytes( boudaryStr! );
                            boundaryLen = boundary.Length;
                            boundaryIsChecked = true;
                        }
				
						// search for image start
						if ( ( align == 1 ) && ( todo >= jpegHeaderLength) )
						{
							start = ByteArrayUtils.Find( buffer, jpegHeader, pos, todo );
							if ( start != -1 )
							{
								// found JPEG start
								pos		= start + jpegHeaderLength;
								todo	= total - pos;
								align	= 2;
							}
							else
							{
								// delimiter not found
                                todo    = jpegHeaderLength - 1;
								pos		= total - todo;
							}
						}

                        // search for image end ( boundaryLen can be 0, so need extra check )
						while ( ( align == 2 ) && ( todo != 0 ) && ( todo >= boundaryLen ) )
						{
							stop = ByteArrayUtils.Find( buffer,
                                ( boundaryLen != 0 ) ? boundary : jpegHeader,
                                pos, todo );

							if ( stop != -1 )
							{
								pos		= stop;
								todo	= total - pos;

								// increment frames counter
								framesReceived ++;

								// image at stop
								if ( ( NewFrame != null ) && ( !stopEvent.WaitOne( 0, false ) ) )
								{
									Bitmap? bitmap = (Bitmap) Bitmap.FromStream ( new MemoryStream( buffer, start, stop - start ) );
									// notify client
                                    NewFrame( this, new NewFrameEventArgs( bitmap ) );
									// release the image
                                    bitmap.Dispose( );
                                    bitmap = null;
								}

								// shift array
								pos		= stop + boundaryLen;
								todo	= total - pos;
								Array.Copy( buffer, pos, buffer, 0, todo );

								total	= todo;
								pos		= 0;
								align	= 1;
							}
							else
							{
								// boundary not found
                                if ( boundaryLen != 0 )
                                {
                                    todo = boundaryLen - 1;
                                    pos = total - todo;
                                }
                                else
                                {
                                    todo = 0;
                                    pos = total;
                                }
							}
						}
					}
				}
				catch ( ApplicationException )
				{
                    // do nothing for Application Exception, which we raised on our own
                    // wait for a while before the next try
                    Thread.Sleep( 250 );
                }
                catch ( ThreadAbortException )
                {
                    break;
                }
                catch ( Exception exception )
                {
                    // provide information to clients
                    if ( VideoSourceError != null )
                    {
                        VideoSourceError( this, new VideoSourceErrorEventArgs( exception.Message ) );
                    }
                    // wait for a while before the next try
                    Thread.Sleep( 250 );
                }
				finally
				{
                    // close response stream
                    if (httpClient != null)
                    {
                        httpClient.Dispose();
                        httpClient = null;
                    }

                    // close response stream
                    if (httpClientResponse != null)
                    {
                        httpClientResponse.Dispose();
                        httpClientResponse = null;
                    }

                    // close response stream
                    if ( streamAsync != null )
					{
						streamAsync.Close( );
						streamAsync = null;
					}

				}

				// need to stop ?
				if ( stopEvent.WaitOne( 0, false ) )
					break;
			}

            if ( PlayingFinished != null )
            {
                PlayingFinished( this, ReasonToFinishPlaying.StoppedByUser );
            }
		}
	}
}
