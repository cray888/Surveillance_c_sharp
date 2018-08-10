using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

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
    public class ImageStreamingServer : IDisposable
    {
        private String TAG = "ImageStreamingServer";

        private List<Socket> _Clients;
        private Thread _Thread;

        public ImageStreamingServer()
        {
            _Clients = new List<Socket>();
            _Thread = null;

            ImagesSource = new Dictionary<Socket, IEnumerable<Image>> { };
            imageData = new Dictionary<string, byte[]>() { };

            Interval = 100;
        }

        /// <summary>
        /// Gets or sets the source of images that will be streamed to the 
        /// any connected client.
        /// </summary>
        public Dictionary<Socket, IEnumerable<Image>> ImagesSource { get; set; }

        /// <summary>
        /// Caching image data
        /// </summary>
        public Dictionary<string, byte[]> imageData { get; set; }

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
        /// Server callback
        /// </summary>
        private IWebServerCallback callback { get; set; }

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
            Start(8080, null);
        }

        public void Stop()
        {
            if (IsRunning)
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

                    lock (ImagesSource)
                        ImagesSource.Clear();

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
            Socket Server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            Server.Bind(new IPEndPoint(IPAddress.Any, (int)state));
            Server.Listen(50);

            Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss - ") + TAG + string.Format(".ServerThread({0})", state), "WEB INFO");

            foreach (Socket client in Server.IncommingConnectoins())
            {
                try
                {
                    byte[] bytes = new byte[1500];
                    client.Receive(bytes);
                    var str = Encoding.Default.GetString(bytes);
                    String path = GetLine(str, 1);

                    if (path.IndexOf("GET /favicon.ico", StringComparison.CurrentCultureIgnoreCase) == 0)
                    {
                        client.Close();
                    }
                    else if (path.IndexOf("GET /index.html", StringComparison.CurrentCultureIgnoreCase) == 0)
                    {
                        using (Stream stream = new NetworkStream(client, true))
                        {
                            string html = @"<html><head></head><body>Life on http://HOST:8080/?chanel=X<br /> Shots on http://HOST:8080/?chanel_shot=X </body></html>";
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
                    else if (path.IndexOf("GET /?chanel=", StringComparison.CurrentCultureIgnoreCase) == 0)
                    {
                        string param = "?chanel=";
                        string chanel = path.Substring(path.IndexOf(param, StringComparison.CurrentCultureIgnoreCase) + param.Length, path.IndexOf(" ", path.IndexOf(param, StringComparison.CurrentCultureIgnoreCase)) - (path.IndexOf(param, StringComparison.CurrentCultureIgnoreCase) + param.Length));

                        lock (ImagesSource)
                            ImagesSource.Add(client, Dvr.Snapshots(chanel, imageData));
                        ClientData clientdata = new ClientData();
                        clientdata.client = client;
                        clientdata.chanel = Int32.Parse(chanel);
                        ThreadPool.QueueUserWorkItem(new WaitCallback(ClientThread), clientdata);

                    }
                    else if (path.IndexOf("GET /?chanel_shot=", StringComparison.CurrentCultureIgnoreCase) == 0)
                    {
                        try
                        {
                            using (Stream stream = new NetworkStream(client, true))
                            {
                                string param = "?chanel_shot=";
                                string chanel = path.Substring(path.IndexOf(param, StringComparison.CurrentCultureIgnoreCase) + param.Length, path.IndexOf(" ", path.IndexOf(param, StringComparison.CurrentCultureIgnoreCase)) - (path.IndexOf(param, StringComparison.CurrentCultureIgnoreCase) + param.Length));

                                if (callback != null) callback.OnClientRequestShot(Int32.Parse(chanel));

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
                            Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss - ") + TAG + ".ServerThread.Exception(" + e.Message + ")", "WEB ERROR");
                        }
                        finally
                        {
                            client.Close();
                        }
                    }
                    else
                    {
                        /*using (Stream stream = new NetworkStream(client, true))
                        {
                            StringBuilder sb = new StringBuilder();
                            sb.AppendLine("HTTP/1.1 301 Moved Permanently");
                            sb.AppendLine("Location: /index.html");
                            sb.AppendLine();

                            byte[] data_byte = Encoding.ASCII.GetBytes(sb.ToString());
                            stream.Write(data_byte, 0, data_byte.Length);
                        }*/

                        client.Close();
                    }
                }
                catch (SocketException e)
                {
                    Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss - ") + TAG + ".ServerThread.SocketException(" + e.Message + ")", "WEB ERROR");
                    Debug.WriteLine(e.ToString());
                }
                catch (Exception e)
                {
                    Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss - ") + TAG + ".ServerThread.Exception(" + e.Message + ")", "WEB ERROR");
                    Debug.WriteLine(e.ToString());
                }
            }

            Stop();
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

            string apAdress = socket.RemoteEndPoint.ToString();

            Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss - ") + TAG + string.Format(".ClientThread.Connect({0})", apAdress), "WEB INFO");

            lock (_Clients)
                _Clients.Add(socket);

            try
            {
                if (callback != null) callback.OnClientConnect(clientdata.chanel);

                using (MjpegWriter wr = new MjpegWriter(new NetworkStream(socket, true)))
                {
                    // Writes the response header to the client.
                    wr.WriteHeader();

                    // Streams the images from the source to the client.
                    foreach (var imgStream in Dvr.Streams(ImagesSource[socket]))
                    {
                        if (Interval > 0)
                            Thread.Sleep(Interval);

                        wr.Write(imgStream);
                    }
                }
            }
            catch (IOException e)
            {
                Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss - ") + TAG + ".ClientThread.IOException(" + e.Message + ")", "WEB ERROR");
            }
            catch (Exception e)
            {
                Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss - ") + TAG + ".ClientThread.Exception(" + e.Message + ")", "WEB ERROR");
            }
            finally
            {
                if (callback != null) callback.OnClientDisconnect(clientdata.chanel);

                Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss - ") + TAG + string.Format(".ClientThread.Disconnect({0})", apAdress), "WEB INFO");

                socket.Close();

                lock (_Clients)
                    _Clients.Remove(socket);

                lock (ImagesSource)
                    ImagesSource.Remove(socket);
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
            while (true)
                yield return server.Accept();
        }
    }

    static class Dvr
    {
        static private String TAG = "DVR";
        /// <summary>
        /// Returns a 
        /// </summary>
        /// <param name="delayTime"></param>
        /// <returns></returns>
        public static IEnumerable<Image> Snapshots(string chanel, Dictionary<string, byte[]> imageData)
        {
            MemoryStream ms;
            Image image = null;

            while (true)
            {
                int counter = 0;
                while (counter < 50)
                {
                    if (imageData.ContainsKey(chanel)) break;
                    counter++;
                    Thread.Sleep(100);
                }

                if (counter == 50)
                {
                    image = Image.FromFile("no_video.jpg");
                }
                else
                {
                    try
                    {
                        ms = new MemoryStream(imageData[chanel]);
                        image = Image.FromStream(ms);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss - ") + TAG + ".Snapshots.Exception(" + e.Message + ")", "WEB ERROR");
                    }
                }

                yield return image;
            }
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
            ms.Dispose();
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
