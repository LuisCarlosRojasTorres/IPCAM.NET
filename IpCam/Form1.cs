using System.Text;
using System.Windows.Forms;
using IpCam;

namespace IpCam
{
    public partial class Form1 : Form
    {
        MjpegStream videostream;
        Bitmap bmp;
        QRDecoder QRCodeDecoder;
        QRCodeResult[]? QRCodeResultArray;

        public Form1()
        {
            InitializeComponent();
            this.videostream = new MjpegStream("http://192.168.15.100:8080/video");
            this.videostream.NewFrame += GetNewFrame;
            this.videostream.NewByteArray += GetNewByteArray;
            videostream.Start();

            QRCodeDecoder = new QRDecoder();
            QRCodeResultArray = null;
        }

        private void GetNewFrame(object sender, NewFrameEventArgs e)
        {
            bmp = (Bitmap)e.Frame.Clone();

            //pictureBox1.Image = bmp;
            if(bmp != null ) 
            {
                QRCodeResultArray = QRCodeDecoder.ImageDecoder(bmp);
            }           

        }

        private void GetNewByteArray(object sender, NewByteArrayEventArgs e)
        {
            byte[] newFrame = e.ByteArrayFrame;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            videostream.Start();
        }

        private void button_OFF_Click(object sender, EventArgs e)
        {
            QRCodeResultArray = QRCodeDecoder.ImageDecoder(bmp);
            //videostream.Unpause();
        }
    }
}