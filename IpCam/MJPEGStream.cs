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
        private string IpCamUrl { get; }
        // received frames count
        private int framesReceived = 0;

        // buffer size used to download MJPEG stream
        private const int bufferLength = 1024 * 1024;
        // size of portion to read at once
        private const int readSize = 1024;

        private Task? task = null;

        public event NewFrameEventHandler? NewFrame;
        public event VideoSourceErrorEventHandler? VideoSourceError;
        public event NewByteArrayEventHandler? NewByteArray;

        private CancellationTokenSource cts;

        private MjpegStreamState state;

        //Camera variables
        byte[] buffer;
        byte[] jpegHeader;
        int jpegHeaderLength;
        ASCIIEncoding encoding;

        HttpClient httpClient;
        Stream? streamAsync;
        HttpResponseMessage? httpClientResponse;

        // boundary betweeen images (string and binary versions)
        byte[]? boundary;
        string? boudaryStr;
        // length of boundary
        int boundaryLen;
        // flag signaling if boundary was checked or not
        bool boundaryIsChecked;
        int boundaryIndex;
        // read amounts and positions
        int read, todo, total, pos, align;
        int start, stop;

        // align
        //  1 = searching for image start
        //  2 = searching for image end
        string contentType;
        string[] contentTypeArray;


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
        /// Initializes a new instance of the <see cref="MJPEGStream"/> class.
        /// </summary>
        /// 
        /// <param name="source">URL, which provides MJPEG stream.</param>
        /// 
        public MjpegStream(string source)
        {
            this.IpCamUrl = source;
            this.cts = new CancellationTokenSource();

            this.state = MjpegStreamState.Connecting;

            this.InitializeCameraVariables();
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
        public void Start()
        {
            framesReceived = 0;

            //task = new Task(() => WorkerThread(this.cts.Token));
            task = new Task(() => TreatFsmCameraAsync(this.cts.Token));
            task.Start();

        }

        private void InitializeCameraVariables() 
        {
            this.buffer = new byte[bufferLength];
            this.jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF };
            this.jpegHeaderLength = jpegHeader.Length;
            this.encoding = new ASCIIEncoding();
        }
        // Worker thread
        private async void WorkerThread(CancellationToken token)
        {
            byte[] buffer = new byte[bufferLength];
            byte[] jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF };
            int jpegHeaderLength = jpegHeader.Length;
            ASCIIEncoding encoding = new ASCIIEncoding();

            while (!token.IsCancellationRequested)
            {
                HttpClient httpClient = new HttpClient();
                Stream? streamAsync = null;
                HttpResponseMessage httpClientResponse = await httpClient.GetAsync(this.IpCamUrl, HttpCompletionOption.ResponseHeadersRead);

                // boundary betweeen images (string and binary versions)
                byte[]? boundary = null;
                string? boudaryStr = null;
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
                    string[] contentTypeArray = contentType.Split('/');

                    // "application/octet-stream"
                    if ((contentTypeArray[0] == "application") && (contentTypeArray[1] == "octet-stream"))
                    {
                        boundaryLen = 0;
                        boundary = new byte[0];
                    }
                    else if ((contentTypeArray[0] == "multipart") && (contentType.Contains("mixed")))
                    {
                        // get boundary
                        int boundaryIndex = contentType.IndexOf("boundary", 0);
                        if (boundaryIndex != -1)
                        {
                            boundaryIndex = contentType.IndexOf("=", boundaryIndex + 8);
                        }

                        if (boundaryIndex == -1)
                        {
                            // try same scenario as with octet-stream, i.e. without boundaries
                            boundaryLen = 0;
                            boundary = new byte[0];
                        }
                        else
                        {
                            boudaryStr = contentType.Substring(boundaryIndex + 1);
                            // remove spaces and double quotes, which may be added by some IP cameras
                            boudaryStr = boudaryStr.Trim(' ', '"'); // Ba4oTvQMY8ew04N8dcnM

                            boundary = encoding.GetBytes(boudaryStr); // In HEx is 66 97 52 111 84 118 81 77 89 56 101 119 48 52 78 56 100 99 110 77 
                            boundaryLen = boundary.Length;
                            boundaryIsChecked = false;
                        }
                    }
                    else
                    {
                        throw new Exception("Invalid content type.");
                    }

                    // get response stream
                    //stream = response.GetResponseStream( );
                    //stream.ReadTimeout = requestTimeout;
                    streamAsync = await httpClient.GetStreamAsync(this.IpCamUrl);


                    // loop
                    while (!token.IsCancellationRequested)
                    {
                        // check total read
                        if (total > bufferLength - readSize)
                        {
                            total = pos = todo = 0;
                        }

                        // read next portion from stream
                        if ((read = streamAsync.Read(buffer, total, readSize)) == 0)
                            throw new ApplicationException();

                        total += read; //Todo o que foi lido ate agora
                        todo += read; //O que tem que ser processado

                        // do we need to check boundary ?
                        if ((boundaryLen != 0) && (!boundaryIsChecked))
                        {
                            // some IP cameras, like AirLink, claim that boundary is "myboundary",
                            // when it is really "--myboundary". this needs to be corrected.

                            pos = ByteArrayUtils.Find(buffer, boundary, 0, todo);
                            // continue reading if boudary was not found
                            if (pos == -1)
                                continue;

                            for (int i = pos - 1; i >= 0; i--)
                            {
                                byte ch = buffer[i];

                                if ((ch == (byte)'\n') || (ch == (byte)'\r'))
                                {
                                    break;
                                }

                                boudaryStr = (char)ch + boudaryStr;
                            }

                            boundary = encoding.GetBytes(boudaryStr!);
                            boundaryLen = boundary.Length;
                            boundaryIsChecked = true;
                        }

                        // search for image start
                        if ((align == 1) && (todo >= jpegHeaderLength))
                        {
                            start = ByteArrayUtils.Find(buffer, jpegHeader, pos, todo);
                            if (start != -1)
                            {
                                // found JPEG start
                                pos = start + jpegHeaderLength;
                                todo = total - pos;
                                align = 2;
                            }
                            else
                            {
                                // delimiter not found
                                todo = jpegHeaderLength - 1;
                                pos = total - todo;
                            }
                        }

                        // search for image end ( boundaryLen can be 0, so need extra check )
                        while ((align == 2) && (todo != 0) && (todo >= boundaryLen))
                        {
                            stop = ByteArrayUtils.Find(buffer,
                                (boundaryLen != 0) ? boundary : jpegHeader,
                                pos, todo);

                            if (stop != -1)
                            {
                                pos = stop;
                                todo = total - pos;

                                // increment frames counter
                                framesReceived++;

                                // image at stop
                                if ((NewFrame != null) && !token.IsCancellationRequested)
                                {
                                    Bitmap? bitmap = (Bitmap)Bitmap.FromStream(new MemoryStream(buffer, start, stop - start));

                                    // notify the subscribrer using event
                                    NewFrame(this, new NewFrameEventArgs(bitmap));
                                    // release the image
                                    bitmap.Dispose();
                                    bitmap = null;

                                    byte[]? frameAsByteArray = new byte[stop - start];
                                    Array.Copy(buffer, start, frameAsByteArray, 0, stop - start);

                                    NewByteArray(this, new NewByteArrayEventArgs(frameAsByteArray));
                                }

                                // shift array
                                pos = stop + boundaryLen;
                                todo = total - pos;
                                Array.Copy(buffer, pos, buffer, 0, todo);

                                total = todo;
                                pos = 0;
                                align = 1;
                            }
                            else
                            {
                                // boundary not found
                                if (boundaryLen != 0)
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
                catch (ApplicationException)
                {
                    // do nothing for Application Exception, which we raised on our own
                    // wait for a while before the next try
                    Thread.Sleep(250);
                }
                catch (ThreadAbortException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    // provide information to clients
                    if (VideoSourceError != null)
                    {
                        VideoSourceError(this, new VideoSourceErrorEventArgs(exception.Message));
                    }
                    // wait for a while before the next try
                    Thread.Sleep(250);
                }
                finally
                {
                    // close response stream
                    if (httpClient != null)
                    {
                        httpClient.Dispose();
                    }

                    // close response stream
                    if (httpClientResponse != null)
                    {
                        httpClientResponse.Dispose();
                    }

                    // close response stream
                    if (streamAsync != null)
                    {
                        streamAsync.Close();
                    }
                }

                // need to stop ?
                if (token.IsCancellationRequested)
                {
                    break;
                }

            }
        }

        private async void TreatFsmCameraAsync(CancellationToken cancellationToken)
        {
            while(!cancellationToken.IsCancellationRequested)
            {
                switch (this.state)
                {
                    case MjpegStreamState.Connecting:
                        await this.ConnectToIpCamera();
                        break;
                    case MjpegStreamState.Working:
                        this.Working();
                        break;
                    case MjpegStreamState.ErrorConnecting:
                        this.HandleErrorConnecting();
                        break;
                    case MjpegStreamState.ErrorWorking:
                        this.HandleErrorWorking();
                        break;
                    case MjpegStreamState.Pause:
                        await Task.Delay(1000);
                        break;
                }
            }        
        }

        private async Task ConnectToIpCamera()
        {
            Console.WriteLine(" - ConnectToIpCamera...");

            try
            {
                this.httpClient = new HttpClient();
                this.streamAsync = null;
                this.httpClientResponse = await httpClient.GetAsync(this.IpCamUrl, HttpCompletionOption.ResponseHeadersRead);

                // boundary betweeen images (string and binary versions)
                this.boundary = null;
                this.boudaryStr = null;

                // flag signaling if boundary was checked or not
                this.boundaryIsChecked = false;
                // read amounts and positions
                this.todo = 0;
                this.total = 0;
                this.pos = 0;
                this.align = 1;
                this.start = 0;
                this.stop = 0;

                // align
                //  1 = searching for image start
                //  2 = searching for image end

                this.contentType = httpClientResponse.Content.Headers.ContentType!.ToString();
                this.contentTypeArray = contentType.Split('/');

                // "application/octet-stream"
                if ((this.contentTypeArray[0] == "application") && (this.contentTypeArray[1] == "octet-stream"))
                {
                    this.boundaryLen = 0;
                    this.boundary = new byte[0];
                }
                else if ((this.contentTypeArray[0] == "multipart") && this.contentType.Contains("mixed"))
                {
                    // get boundary
                    this.boundaryIndex = contentType.IndexOf("boundary", 0);

                    if (boundaryIndex != -1)
                    {
                        boundaryIndex = contentType.IndexOf("=", boundaryIndex + 8);
                    }

                    if (boundaryIndex == -1)
                    {
                        // try same scenario as with octet-stream, i.e. without boundaries
                        this.boundaryLen = 0;
                        this.boundary = new byte[0];
                    }
                    else
                    {
                        this.boudaryStr = contentType.Substring(boundaryIndex + 1);
                        // remove spaces and double quotes, which may be added by some IP cameras
                        this.boudaryStr = this.boudaryStr.Trim(' ', '"'); // Ba4oTvQMY8ew04N8dcnM

                        this.boundary = encoding.GetBytes(boudaryStr); // In HEx is 66 97 52 111 84 118 81 77 89 56 101 119 48 52 78 56 100 99 110 77 
                        this.boundaryLen = this.boundary.Length;
                        this.boundaryIsChecked = false;
                    }
                }
                else
                {
                    throw new Exception("Invalid content type.");
                }
                
                streamAsync = await httpClient.GetStreamAsync(this.IpCamUrl);
                
                this.state = MjpegStreamState.Working;
                Console.WriteLine(" - ConnectToIpCamera: status changed to Working");
            }
            catch 
            {
                this.state = MjpegStreamState.ErrorConnecting;
                Console.WriteLine(" - ConnectToIpCamera: status changed to ErrorConnecting");
            }
        }

        private void Working()
        {
            Console.WriteLine(" - Working...");

            try
            {
                // check total read
                if (this.total > bufferLength - readSize)
                {
                    this.total = 0;
                    this.pos = 0;
                    this.todo = 0;
                }

                // read next portion from stream
                if ((this.read = this.streamAsync!.Read(buffer, total, readSize)) == 0)
                {
                    throw new ApplicationException();
                }                    

                this.total += this.read; //Todo o que foi lido ate agora
                this.todo += this.read; //O que tem que ser processado

                // do we need to check boundary ?
                if ((this.boundaryLen != 0) && (!this.boundaryIsChecked))
                {
                    // some IP cameras, like AirLink, claim that boundary is "myboundary",
                    // when it is really "--myboundary". this needs to be corrected.

                    this.pos = ByteArrayUtils.Find(this.buffer, this.boundary!, 0, this.todo);
                    // continue reading if boudary was not found
                    if (this.pos == -1)
                    {
                        return;
                    }                        

                    for (int i = this.pos - 1; i >= 0; i--)
                    {
                        byte ch = this.buffer[i];

                        if ((ch == (byte)'\n') || (ch == (byte)'\r'))
                        {
                            break;
                        }

                        this.boudaryStr = (char)ch + this.boudaryStr;
                    }

                    this.boundary = encoding.GetBytes(this.boudaryStr!);
                    this.boundaryLen = this.boundary.Length;
                    this.boundaryIsChecked = true;
                }

                // search for image start
                if ((this.align == 1) && (this.todo >= this.jpegHeaderLength))
                {
                    this.start = ByteArrayUtils.Find(this.buffer, this.jpegHeader, this.pos, this.todo);
                    if (this.start != -1)
                    {
                        // found JPEG start
                        this.pos = this.start + this.jpegHeaderLength;
                        this.todo = this.total - this.pos;
                        this.align = 2;
                    }
                    else
                    {
                        // delimiter not found
                        this.todo = this.jpegHeaderLength - 1;
                        this.pos = this.total - this.todo;
                    }
                }

                // search for image end ( boundaryLen can be 0, so need extra check )
                while ((this.align == 2) && (this.todo != 0) && (this.todo >= this.boundaryLen))
                {
                    this.stop = ByteArrayUtils.Find(this.buffer, (this.boundaryLen != 0) ? this.boundary! : this.jpegHeader, this.pos, this.todo);

                    if (this.stop != -1)
                    {
                        this.pos = this.stop;
                        this.todo = this.total - this.pos;

                        // increment frames counter
                        this.framesReceived++;

                        // image at stop
                        if (NewFrame != null)
                        {
                            //Bitmap? bitmap = (Bitmap)Bitmap.FromStream(new MemoryStream(this.buffer, this.start, this.stop - this.start));

                            // notify the subscribrer using event
                            //NewFrame(this, new NewFrameEventArgs(bitmap));
                            // release the image
                            //bitmap.Dispose();
                            //bitmap = null;

                            byte[]? frameAsByteArray = new byte[this.stop - this.start];
                            Array.Copy(this.buffer, this.start, frameAsByteArray, 0, this.stop - this.start);

                            NewByteArray!(this, new NewByteArrayEventArgs(frameAsByteArray));
                        }

                        // shift array
                        this.pos = this.stop + this.boundaryLen;
                        this.todo = this.total - this.pos;
                        Array.Copy(this.buffer, this.pos, this.buffer, 0, this.todo);

                        this.total = this.todo;
                        this.pos = 0;
                        this.align = 1;
                    }
                    else
                    {
                        // boundary not found
                        if (this.boundaryLen != 0)
                        {
                            this.todo = this.boundaryLen - 1;
                            this.pos = this.total - this.todo;
                        }
                        else
                        {
                            this.todo = 0;
                            this.pos = this.total;
                        }
                    }
                }
            }
            catch (ApplicationException)
            {
                this.state = MjpegStreamState.ErrorWorking;
                Console.WriteLine(" - Working: status changed to ErrorWorking");
            }
            catch (Exception exception)
            {
                this.state = MjpegStreamState.ErrorConnecting;
                if (VideoSourceError != null)
                {
                    VideoSourceError(this, new VideoSourceErrorEventArgs(exception.Message));
                }
                Console.WriteLine(" - Working: status changed to ErrorConnecting");
            }
        }

        private void HandleErrorConnecting()
        {            
            this.state = MjpegStreamState.Connecting;
            Console.WriteLine(" - HandleErrorConnecting: status changed to Connecting");
        }

        private void HandleErrorWorking()
        {
            this.state = MjpegStreamState.Connecting;
            Console.WriteLine(" - HandleErrorWorking: status changed to Connecting");
        }

        public void Unpause()
        {
            if (this.state != MjpegStreamState.Pause)
            {
                this.state = MjpegStreamState.Pause;
                Console.WriteLine(" - Unpause: status changed to Pause");
            }
            else 
            {
                this.state = MjpegStreamState.Connecting;
                Console.WriteLine(" - Unpause: status changed to Connecting");
            }            
        }
    }
}
