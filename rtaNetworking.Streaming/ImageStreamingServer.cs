using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using System.IO;

// -------------------------------------------------
// Developed By : Ragheed Al-Tayeb
// e-Mail       : ragheedemail@gmail.com
// Date         : April 2012
// -------------------------------------------------

namespace rtaNetworking.Streaming
{

    /// <summary>
    /// Provides a streaming server that can be used to stream any images source
    /// to any client.
    /// </summary>
    public class ImageStreamingServer:IDisposable
    {
        private List<Socket> _Clients;
        private Thread _Thread;

        private IWebServerCallback callback;

        public ImageStreamingServer()
        {
            _Clients = new List<Socket>();
            _Thread = null;

            ImagesSource = new Dictionary<Socket, IEnumerable<Image>> { };

            this.Interval = 50;
        }

        /// <summary>
        /// Gets or sets the source of images that will be streamed to the 
        /// any connected client.
        /// </summary>
        public Dictionary<Socket, IEnumerable<Image>> ImagesSource { get; set; }

        /// <summary>
        /// Gets or sets the interval in milliseconds (or the delay time) between 
        /// the each image and the other of the stream (the default is . 
        /// </summary>
        public int Interval { get; set; }

        /// <summary>
        /// Gets a collection of client sockets.
        /// </summary>
        public IEnumerable<Socket> Clients { get { return _Clients; } }

        /// <summary>
        /// Returns the status of the server. True means the server is currently 
        /// running and ready to serve any client requests.
        /// </summary>
        public bool IsRunning { get { return (_Thread != null && _Thread.IsAlive); } }

        /// <summary>
        /// Starts the server to accepts any new connections on the specified port.
        /// </summary>
        /// <param name="port"></param>
        public void Start(int port, IWebServerCallback callback)
        {
            this.callback = callback;
            lock (this)
            {
                _Thread = new Thread(new ParameterizedThreadStart(ServerThread));
                _Thread.IsBackground = true;
                _Thread.Start(port);
            }
        }

        /// <summary>
        /// Starts the server to accepts any new connections on the default port (8080).
        /// </summary>
        public void Start()
        {
            this.Start(8080, null);
        }

        public void Stop()
        {
            if (this.IsRunning)
            {
                try
                {
                    _Thread.Join();
                    _Thread.Abort();
                }
                finally
                {
                    lock (_Clients)
                    {                        
                        foreach (var s in _Clients)
                        {
                            try
                            {
                                s.Close();
                            }
                            catch { }
                        }
                        _Clients.Clear();
                    }
                    _Thread = null;
                }
            }
        }

        /// <summary>
        /// This the main thread of the server that serves all the new 
        /// connections from clients.
        /// </summary>
        /// <param name="state"></param>
        private void ServerThread(object state)
        {
            try
            {
                Socket Server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                Server.Bind(new IPEndPoint(IPAddress.Any,(int)state));
                Server.Listen(10);

                System.Diagnostics.Debug.WriteLine(string.Format("Server started on port {0}.", state));

                foreach (Socket client in Server.IncommingConnectoins())
                {
                    byte[] bytes = new byte[1500];
                    client.Receive(bytes);
                    var str = System.Text.Encoding.Default.GetString(bytes);
                    String path = GetLine(str, 1);
                    if (path.IndexOf("GET /favicon.ico") == 0)
                    {
                        client.Close();
                    }
                    else if (path.IndexOf("GET /index.html") == 0)
                    {
                        using (Stream stream = new NetworkStream(client, true))
                        {
                            string html = "<html><head></head><body>Life on http://<HOST:8080>/?chanel=X<br /> Shots on http://<HOST:8080>/?chanel_shot=X</body></html>";
                            StringBuilder sb = new StringBuilder();
                            sb.AppendLine("HTTP/1.1 200 OK");
                            sb.AppendLine("content-type: text/html");
                            sb.AppendLine("connection: keep-alive");
                            sb.AppendLine("content-length: " + html.Length.ToString());
                            sb.AppendLine();
                            sb.AppendLine(html);

                            byte[] data_byte = Encoding.ASCII.GetBytes(sb.ToString());
                            stream.Write(data_byte, 0, data_byte.Length);
                        }

                        client.Close();
                    }
                    else if (path.IndexOf("?chanel=") >= 0)
                    {
                        string param = "?chanel=";
                        string chanel = path.Substring(path.IndexOf(param) + param.Length, path.IndexOf(" ", path.IndexOf(param)) - (path.IndexOf(param) + param.Length));

                        callback.OnClientConnect(Int32.Parse(chanel));

                        ImagesSource.Add(client, Dvr.Snapshots(chanel));
                        ClientData clientdata = new ClientData();
                        clientdata.client = client;
                        clientdata.chanel = Int32.Parse(chanel);
                        ThreadPool.QueueUserWorkItem(new WaitCallback(ClientThread), clientdata);

                    }
                    else if (path.IndexOf("?chanel_shot=") >= 0)
                    {
                        try
                        {
                            using (Stream stream = new NetworkStream(client, true))
                            {
                                string param = "?chanel_shot=";
                                string chanel = path.Substring(path.IndexOf(param) + param.Length, path.IndexOf(" ", path.IndexOf(param)) - (path.IndexOf(param) + param.Length));

                                callback.OnClientRequestShot(Int32.Parse(chanel));

                                string strLink = String.Format(@"Z:\Pictures\jpeg\{0}.jpg", chanel);

                                var data = File.ReadAllBytes(strLink);
                                var ms = new MemoryStream(data);

                                StringBuilder sb = new StringBuilder();
                                sb.AppendLine("HTTP/1.1 200 OK");
                                sb.AppendLine("Server: cray_server");
                                sb.AppendLine("Connection: keep-alive");
                                sb.AppendLine("Content-Type: image/jpeg");
                                sb.AppendLine("Content-Length: " + ms.Length.ToString());
                                sb.AppendLine();

                                byte[] data_byte = Encoding.ASCII.GetBytes(sb.ToString());
                                stream.Write(data_byte, 0, data_byte.Length);

                                ms.WriteTo(stream);

                                stream.Flush();
                            }
                        }
                        catch (Exception e)
                        {
                            client.Close();
                        }
                        finally
                        {
                            client.Close();
                        }
                    }
                    else
                    {
                        using (Stream stream = new NetworkStream(client, true))
                        {
                            StringBuilder sb = new StringBuilder();
                            sb.AppendLine("HTTP/1.1 301 Moved Permanently");
                            sb.AppendLine("Location: /index.html");
                            sb.AppendLine();

                            byte[] data_byte = Encoding.ASCII.GetBytes(sb.ToString());
                            stream.Write(data_byte, 0, data_byte.Length);
                        }

                        client.Close();
                    }
                }            
            }
            catch { }
            this.Stop();
        }

        string GetLine(string text, int lineNo)
        {
            string[] lines = text.Replace("\r", "").Split('\n');
            return lines.Length >= lineNo ? lines[lineNo - 1] : null;
        }

        /// <summary>
        /// Each client connection will be served by this thread.
        /// </summary>
        /// <param name="client"></param>
        private void ClientThread(object camobject)
        {
            ClientData clientdata = (ClientData)camobject;

            Socket socket = clientdata.client;

            System.Diagnostics.Debug.WriteLine(string.Format("New client from {0}",socket.RemoteEndPoint.ToString()));

            lock (_Clients)
                _Clients.Add(socket);

            try
            {
                using (MjpegWriter wr = new MjpegWriter(new NetworkStream(socket, true)))
                {
                    // Writes the response header to the client.
                    wr.WriteHeader();

                    // Streams the images from the source to the client.
                    foreach (var imgStream in Screen.Streams(this.ImagesSource[clientdata.client]))
                    {
                        if (this.Interval > 0)
                            Thread.Sleep(this.Interval);

                        wr.Write(imgStream);
                    }
                }
            }
            catch { }
            finally
            {
                callback.OnClientDisconnect(clientdata.chanel);
                lock (_Clients)
                    _Clients.Remove(socket);
            }
        }

        private class ClientData
        {
            public Socket client;
            public int chanel;
        }

        #region IDisposable Members

        public void Dispose()
        {
            this.Stop();
        }

        #endregion
    }

    static class SocketExtensions
    {
        public static IEnumerable<Socket> IncommingConnectoins(this Socket server)
        {
            while(true)
                yield return server.Accept();
        }
    }

    static class Dvr
    {
        /// <summary>
        /// Returns a 
        /// </summary>
        /// <param name="delayTime"></param>
        /// <returns></returns>
        public static IEnumerable<Image> Snapshots(string chanel)
        {
            string strLink = String.Format(@"Z:\Pictures\bmp\{0}.bmp", chanel);

            var data = File.ReadAllBytes(strLink);
            var ms = new MemoryStream(data);
            Image image = Image.FromStream(ms);

            while (true)
            {
                try
                {
                    data = File.ReadAllBytes(strLink);
                    ms = new MemoryStream(data);
                    image = Image.FromStream(ms);
                }
                catch {}

                yield return image;
            }

            ms.Dispose();
            image.Dispose();

            yield break;
        }

        internal static IEnumerable<MemoryStream> Streams(this IEnumerable<Image> source)
        {
            MemoryStream ms = new MemoryStream();

            foreach (var img in source)
            {
                ms.SetLength(0);
                img.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                yield return ms;
            }

            ms.Close();
            ms = null;

            yield break;
        }
    }

    static class Screen
    {
        /// <summary>
        /// Returns a 
        /// </summary>
        /// <param name="delayTime"></param>
        /// <returns></returns>
        public static IEnumerable<Image> Snapshots(int width, int height, bool showCursor)
        {
            Size size = new Size(System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width, System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height);

            Bitmap srcImage = new Bitmap(size.Width, size.Height);
            Graphics srcGraphics = Graphics.FromImage(srcImage);

            bool scaled = (width != size.Width || height != size.Height);

            Bitmap dstImage = srcImage;
            Graphics dstGraphics = srcGraphics;

            if (scaled)
            {
                dstImage = new Bitmap(width, height);
                dstGraphics = Graphics.FromImage(dstImage);
            }

            Rectangle src = new Rectangle(0, 0, size.Width, size.Height);
            Rectangle dst = new Rectangle(0, 0, width, height);
            Size curSize = new Size(32, 32);

            while (true)
            {
                srcGraphics.CopyFromScreen(0, 0, 0, 0, size);

                if (showCursor)
                    Cursors.Default.Draw(srcGraphics, new Rectangle(Cursor.Position, curSize));

                if (scaled)
                    dstGraphics.DrawImage(srcImage, dst, src, GraphicsUnit.Pixel);

                yield return dstImage;

            }

            srcGraphics.Dispose();
            dstGraphics.Dispose();

            srcImage.Dispose();
            dstImage.Dispose();

            yield break;
        }

        internal static IEnumerable<MemoryStream> Streams(this IEnumerable<Image> source)
        {
            MemoryStream ms = new MemoryStream();

            foreach (var img in source)
            {
                ms.SetLength(0);
                img.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                yield return ms;
            }

            ms.Close();
            ms = null;

            yield break;
        }        
    }
}
