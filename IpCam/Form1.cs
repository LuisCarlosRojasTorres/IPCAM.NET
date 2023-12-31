using System.Text;
using System.Windows.Forms;
using IpCam;


namespace IpCam
{
    public partial class Form1 : Form
    {
        MjpegStream videostream;
        Bitmap bmp;
        Bitmap bmp2;

        public Form1()
        {
            InitializeComponent();
            this.videostream = new MjpegStream("http://192.168.15.100:8080/video");
            this.videostream.NewFrame += GetNewFrame;
            this.videostream.NewByteArray += GetNewByteArray;
            videostream.Start();
         }

        private void GetNewFrame(object sender, NewFrameEventArgs e)
        {
            Bitmap bmp = (Bitmap)e.Frame.Clone();

            ImageConverter converter = new ImageConverter();
            byte[] bmpAsArrayOfBytes = (byte[])converter.ConvertTo((Bitmap)bmp!, typeof(byte[]))!;

            pictureBox1.Image = bmp;

        }

        private void GetNewByteArray(object sender, NewByteArrayEventArgs e)
        {
            byte[] newFrame = e.ByteArrayFrame;            
        }

        private void button1_Click(object sender, EventArgs e)
        {
            videostream.Start();
        }
    }
}