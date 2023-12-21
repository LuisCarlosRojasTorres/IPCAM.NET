using AForge.Video;

namespace IpCam
{
    public partial class Form1 : Form
    {
        MJPEGStream videostream;

        public Form1()
        {
            InitializeComponent();
            this.videostream = new MJPEGStream("http://192.168.15.100:8080/video");
        
            //this.videostream = new MJPEGStream("https://learn.microsoft.com/en-us/dotnet/api/system.net.webresponse.contenttype?view=net-8.0");
            this.videostream.NewFrame += GetNewFrameAtSomeFPS;

        }

        private void GetNewFrameAtSomeFPS(object sender, NewFrameEventArgs e)
        {
            Bitmap bmp = (Bitmap)e.Frame.Clone();
            pictureBox1.Image = bmp;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            videostream.Start();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            videostream.Stop();
        }


    }
}