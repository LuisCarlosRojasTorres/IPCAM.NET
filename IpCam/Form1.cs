using AForge.Video;
using QRCodeDecoderLibrary;
using QRCodeEncoderLibrary;
using System.Text;
using System.Windows.Forms;


namespace IpCam
{
    public partial class Form1 : Form
    {
        MJPEGStream videostream;
        QRDecoder qRDecoder;
        Bitmap bmp;
        Bitmap bmp2;

        public Form1()
        {
            InitializeComponent();
            this.videostream = new MJPEGStream("http://192.168.15.100:8080/video");
            this.videostream.NewFrame += GetNewFrameAtSomeFPS;
            qRDecoder = new QRDecoder();
            videostream.Start();
        }

        private void GetNewFrameAtSomeFPS(object sender, NewFrameEventArgs e)
        {
            Bitmap bmp = (Bitmap)e.Frame.Clone();

            ImageConverter converter = new ImageConverter();
            byte[] bmpAsArrayOfBytes = (byte[])converter.ConvertTo((Bitmap)bmp!, typeof(byte[]))!;

            
        }

        private void QR_DECODE(object sender, EventArgs e)
        {
            if (pictureBox1.Image is not null)
            {
                QRCodeResult[] result = qRDecoder.ImageDecoder((Bitmap)pictureBox1.Image);

                if (result != null)
                {
                    Console.WriteLine(result.ToString());
                }
            }
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