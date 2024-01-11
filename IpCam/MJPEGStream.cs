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
                        break;
                }

                await Task.Delay(1000);
            }

            this.DisposeHttpClients();
        }

        private void DisposeHttpClients() 
        {
            if (httpClient != null)
            {
                httpClient.Dispose();
            }
            
            if (httpClientResponse != null)
            {
                httpClientResponse.Dispose();
            }

            if (streamAsync != null)
            {
                streamAsync.Close();
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
                        if (NewByteArray != null)
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
