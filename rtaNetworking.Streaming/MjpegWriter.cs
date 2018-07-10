using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing;
using System.Net.Sockets;

// -------------------------------------------------
// Developed By : Ragheed Al-Tayeb
// e-Mail       : ragheedemail@gmail.com
// Date         : April 2012
// -------------------------------------------------

namespace rtaNetworking.Streaming
{

    /// <summary>
    /// Provides a stream writer that can be used to write images as MJPEG 
    /// or (Motion JPEG) to any stream.
    /// </summary>
    public class MjpegWriter:IDisposable 
    {

        private static byte[] CRLF = new byte[] { 13, 10 };
        private static byte[] EmptyLine = new byte[] { 13, 10, 13, 10};

        public MjpegWriter(Stream stream) : this(stream, "--boundary") {}

        public MjpegWriter(Stream stream, string boundary)
        {
            Stream = stream;
            Boundary = boundary;
        }

        public string Boundary { get; private set; }
        public Stream Stream { get; private set; }

        public void WriteHeader()
        {
            Write( 
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: multipart/x-mixed-replace; boundary=" +
                    Boundary +
                    "\r\n"
                 );

            Stream.Flush();
       }

        public void Write(Image image)
        {
            MemoryStream ms = BytesOf(image);
            Write(ms);
        }

        public void Write(MemoryStream imageStream)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine();
            sb.AppendLine(this.Boundary);
            sb.AppendLine("Content-Length: " + imageStream.Length.ToString());
            sb.AppendLine("Content-Type: image/jpeg");            
            sb.AppendLine(); 

            Write(sb.ToString());
            imageStream.WriteTo(Stream);
            Write("\r\n");   
            
            Stream.Flush();
        }

        private void Write(byte[] data)
        {
            Stream.Write(data, 0, data.Length);
        }

        private void Write(string text)
        {
            byte[] data = BytesOf(text);
            Stream.Write(data, 0, data.Length);
        }

        private static byte[] BytesOf(string text)
        {
            return Encoding.ASCII.GetBytes(text);
        }

        private static MemoryStream BytesOf(Image image)
        {
            MemoryStream ms = new MemoryStream();
            image.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
            return ms;
        }

        public string ReadRequest(int length)
        {
            byte[] data = new byte[length];
            int count = Stream.Read(data,0,data.Length);

            if (count != 0) return Encoding.ASCII.GetString(data, 0, count);

            return null;
        }

        #region IDisposable Members

        public void Dispose()
        {
            try
            {
                if (Stream != null) Stream.Dispose();
            }
            finally
            {
                Stream = null;
            }
        }

        #endregion
    }
}
