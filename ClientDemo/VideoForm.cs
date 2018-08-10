using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.Drawing.Imaging;
using System.Diagnostics;
using Tao.FFmpeg;
using System.Threading;
using SharpFFmpeg;
using System.Threading.Tasks;

namespace DVR2Mjpeg
{
    public partial class VideoForm : UserControl
    {
        private String TAG = "VideoForm";

        ////////////////////////////////////////////////////////
        //DVR block
        public bool m_bSaveImageStart;

        int m_nIndex;   //index	
        bool m_bRecord; //is recording or not
        bool m_bSound;

        public int m_iPlayhandle;   //play handle
        public int m_lLogin; //login handle
        public int m_iChannel; //play channel
        public int m_iTalkhandle;

        private XMSDK.fRealDataCallBack_V2 realDataCallBack_V2; //recive data callback

        ////////////////////////////////////////////////////////
        //h.264 deocde block
        IntPtr pCodecCtx_pt;
        FFmpeg.AVCodecContext pCodecCtx;

        ImageCodecInfo ici;
        EncoderParameters ep;

        ////////////////////////////////////////////////////////
        //Thread decode block
        byte[] data = new byte[0];
        int width;
        int height;
        bool decodeRuning, decodeFrameRuning;
        Thread decodeThread;

        public VideoForm()
        {
            Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss - ") + TAG + ".VideoForm()", "DVR INFO");

            InitializeComponent();

            ////////////////////////////////////////////////////////
            IntPtr pCodec_pt = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(FFmpeg.AVCodec)));
            pCodec_pt = FFmpeg.avcodec_find_decoder(FFmpeg.CodecID.CODEC_ID_H264);

            FFmpeg.AVCodec pCodec = (FFmpeg.AVCodec)Marshal.PtrToStructure((IntPtr)((UInt32)pCodec_pt), typeof(FFmpeg.AVCodec));

            pCodecCtx_pt = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(FFmpeg.AVCodecContext)));
            pCodecCtx_pt = FFmpeg.avcodec_alloc_context();

            pCodecCtx = (FFmpeg.AVCodecContext)Marshal.PtrToStructure((IntPtr)((UInt32)pCodecCtx_pt), typeof(FFmpeg.AVCodecContext));

            int open_en = FFmpeg.avcodec_open(pCodecCtx_pt, pCodec_pt);

            ////////////////////////////////////////////////////////
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            ici = null;
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.MimeType == "image/jpeg") ici = codec;
            }
            ep = new EncoderParameters();
            ep.Param[0] = new EncoderParameter(Encoder.Quality, (long)50);
        }

        private int DataCallBack_V2(int lRealHandle, ref PACKET_INFO_EX pFrame, int dwUser)
        {
            //https://github.com/peidongbin/git/blob/b597b13707c7f865b01062318dc048b92f985226/Monitorsever/Monitorsever/video.cs

            if (pFrame.nPacketType == 0 || pFrame.nPacketType == 10) return 1; //if not image frame            

            IntPtr yuvdata = Marshal.AllocHGlobal(200);

            int decode_result = 0;
            int len = FFmpeg.avcodec_decode_video(pCodecCtx_pt, yuvdata, ref decode_result, pFrame.pPacketBuffer, (int)pFrame.dwPacketSize);
            pCodecCtx = (FFmpeg.AVCodecContext)Marshal.PtrToStructure(pCodecCtx_pt, typeof(FFmpeg.AVCodecContext));

            if (len > 0 && decodeFrameRuning == false)
            {
                decodeFrameRuning = true;

                width = pCodecCtx.width;
                height = pCodecCtx.height;

                FFmpeg.AVPicture avpicture = (FFmpeg.AVPicture)Marshal.PtrToStructure(yuvdata, typeof(FFmpeg.AVPicture));
                data = new byte[avpicture.linesize[0] * height * 2];
                Marshal.Copy(avpicture.data[0], data, 0, avpicture.linesize[0] * height);
                Marshal.Copy(avpicture.data[1], data, (width + 32) * height, avpicture.linesize[1] * (height / 2));
                Marshal.Copy(avpicture.data[2], data, (width + 32) * height * 5 / 4, avpicture.linesize[1] * (height / 2));

                Task.Factory.StartNew(DecodeFarme);          
            }

            return 1;
        }

        public void DecodeFarme()
        {
            if (decodeFrameRuning == false) return;

            ///////////////////////////////////////////////////////////////////
            int imgSize = width * height;
            int frameSize = imgSize + (imgSize >> 1);
            byte[] yuvframe = new byte[frameSize];
            byte[] rgbframe = new byte[3 * imgSize];

            for (int l = 0; l < height; l++) { Array.Copy(data, l * (width + 32), yuvframe, l * width, width); }
            for (int l = 0; l < (height / 2); l++) { Array.Copy(data, (width + 32) * height + l * (width + 32) / 2, yuvframe, imgSize + l * (width / 2), (width / 2)); }
            for (int l = 0; l < (height / 2); l++) { Array.Copy(data, (width + 32) * height * 5 / 4 + l * (width + 32) / 2, yuvframe, imgSize * 5 / 4 + l * (width / 2), (width / 2)); }

            Bitmap bm = AVConverter.ConvertYUV2Bitmap(yuvframe, rgbframe, width, height);

            /////////////////////////////////////////////////////////////////// 
            updateImageOnServer(bm);
            bm.Dispose();

            decodeFrameRuning = false;
        }

        public void SetWndIndex(int nIndex)
        {
            Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss - ") + TAG + ".SetWndIndex(" + nIndex.ToString() + ")", "DVR INFO");
            m_nIndex = nIndex;
        }

        public int ConnectRealPlay(ref DEV_INFO pDev, int nChannel, int nStream = 1)
        {
            Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss - ") + TAG + ".ConnectRealPlay(" + pDev.szDevName + "," + nChannel.ToString() + ")", "DVR INFO");

            if (m_iPlayhandle != -1)
            {
                if (0 != XMSDK.H264_DVR_StopRealPlay(m_iPlayhandle, (uint)panelVideo.Handle))
                {
                    //TODO: Здесь необходимо доработать
                }
                if (m_bSound)
                {
                    OnCloseSound();
                }
            }

            H264_DVR_CLIENTINFO playstru = new H264_DVR_CLIENTINFO();

            playstru.nChannel = nChannel;
            playstru.nStream = nStream;
            playstru.nMode = 0;
            playstru.hWnd = panelVideo.Handle;

            m_iPlayhandle = XMSDK.H264_DVR_RealPlay(pDev.lLoginID, ref playstru);
            if (m_iPlayhandle <= 0)
            {
                Int32 dwErr = XMSDK.H264_DVR_GetLastError();
            }
            else
            {
                XMSDK.H264_DVR_MakeKeyFrame(pDev.lLoginID, nChannel, 0);
                realDataCallBack_V2 = new XMSDK.fRealDataCallBack_V2(DataCallBack_V2);
                XMSDK.H264_DVR_SetRealDataCallBack_V2(m_iPlayhandle, realDataCallBack_V2, Handle.ToInt32());
            }
            m_lLogin = pDev.lLoginID;
            m_iChannel = nChannel;

            return m_iPlayhandle;
        }

        public void GetColor(out int nBright, out int nContrast, out int nSaturation, out int nHue)
        {
            if (m_iPlayhandle <= 0)
            {
                nBright = -1;
                nContrast = -1;
                nSaturation = -1;
                nHue = -1;
                return;
            }
            uint nRegionNum = 0;
            XMSDK.H264_DVR_LocalGetColor(m_iPlayhandle, nRegionNum, out nBright, out nContrast, out nSaturation, out nHue);
        }

        public void SetColor(int nBright, int nContrast, int nSaturation, int nHue)
        {
            XMSDK.H264_DVR_LocalSetColor(m_iPlayhandle, 0, nBright, nContrast, nSaturation, nHue);
        }

        public int GetHandle()
        {
            return m_iPlayhandle;
        }

        public bool OnOpenSound()
        {
            if (XMSDK.H264_DVR_OpenSound(m_iPlayhandle))
            {
                m_bSound = true;
                return true;
            }
            return false;
        }

        public bool OnCloseSound()
        {
            if (XMSDK.H264_DVR_CloseSound(m_iPlayhandle))
            {
                m_bSound = false;
                return true;
            }
            return false;
        }

        public bool SaveRecord()
        {
            if (m_iPlayhandle <= 0)
            {
                return false;
            }

            DateTime time = DateTime.Now;
            String cFilename = String.Format(@"{0}\\Record\\{1}{2}{3}_{4}{5}{6}.h264",
                                                        "Z:",
                                                        time.Year,
                                                        time.Month,
                                                        time.Day,
                                                        time.Hour,
                                                        time.Minute,
                                                        time.Second);
            if (m_bRecord)
            {
                if (XMSDK.H264_DVR_StopLocalRecord(m_iPlayhandle))
                {
                    m_bRecord = false;
                    MessageBox.Show(@"stop record OK.");
                }
            }
            else
            {
                int nTemp = 0;
                string strPr = "\\";
                for (; ; )
                {
                    int nIndex = cFilename.IndexOfAny(strPr.ToCharArray(), nTemp);
                    if (nIndex == -1)
                    {
                        break;
                    }
                    String str = cFilename.Substring(0, nIndex + 1);
                    nTemp = nIndex + 1; nTemp = nIndex + 1;
                    DirectoryInfo dir = new DirectoryInfo(str);
                    if (!dir.Exists)
                    {
                        dir.Create();
                    }
                }

                if (XMSDK.H264_DVR_StartLocalRecord(m_iPlayhandle, cFilename, (int)MEDIA_FILE_TYPE.MEDIA_FILE_NONE))
                {
                    m_bRecord = true;
                    MessageBox.Show(@"start record OK.");
                }
                else
                {
                    MessageBox.Show(@"start record fail.");
                }
            }
            return true;
        }

        public int GetLoginHandle()
        {
            return m_lLogin;
        }

        public void OnDisconnct()
        {
            if (m_iPlayhandle > 0)
            {
                decodeRuning = false;
                BeginInvoke((MethodInvoker)(() =>
                {
                    XMSDK.H264_DVR_DelRealDataCallBack_V2(m_iPlayhandle, realDataCallBack_V2, Handle.ToInt32());
                    XMSDK.H264_DVR_StopRealPlay(m_iPlayhandle, (uint)panelVideo.Handle);
                }));
                m_iPlayhandle = -1;

            }
            if (m_bSound)
            {
                OnCloseSound();
            }
            m_lLogin = -1;
        }

        public void drawOSD(int nPort, IntPtr hDc)
        {
            if (m_strInfoFrame[nPort] != "")
            {
                FontFamily fontfamily = new FontFamily(@"Arial");
                Font newFont = new Font(fontfamily, 16, FontStyle.Bold);
                SolidBrush brush = new SolidBrush(Color.Red);

                Graphics graphic = Graphics.FromHdc(hDc);
                graphic.DrawString("TEST", newFont, brush, 10, 10);
            }
        }

        public int SetDevChnColor(ref SDK_CONFIG_VIDEOCOLOR pVideoColor)
        {
            IntPtr ptr = new IntPtr();
            Marshal.StructureToPtr(pVideoColor, ptr, true);
            return XMSDK.H264_DVR_SetDevConfig(m_lLogin, (uint)SDK_CONFIG_TYPE.E_SDK_VIDEOCOLOR, m_iChannel, ptr, (uint)Marshal.SizeOf(pVideoColor), 3000);
        }

        static void videoInfoFramCallback(int nPort, int nType, string pBuf, int nSize, IntPtr nUser)
        {
            if (nType == 0x03)
            {
                VideoForm form = new VideoForm();
                Marshal.PtrToStructure(nUser, form);
                form.m_strInfoFrame[nPort] = pBuf;
            }
        }

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 100)]
        public string[] m_strInfoFrame;

        private void VideoForm_Click(object sender, EventArgs e)
        {
            DVR2Mjpeg DVR2Mjpeg = (DVR2Mjpeg)this.Parent;
            DVR2Mjpeg.SetActiveWnd(m_nIndex);
            DVR2Mjpeg.comboBoxCamCount.Focus();
        }
#region ToolStripMenu
        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // MessageBox.Show("");
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        public void Close()
        {
            if (-1 != m_nIndex)
            {
                decodeRuning = false;
                XMSDK.H264_DVR_DelRealDataCallBack_V2(m_iPlayhandle, realDataCallBack_V2, Handle.ToInt32());

                XMSDK.H264_DVR_StopRealPlay(m_iPlayhandle, (uint)this.panelVideo.Handle);
                DVR2Mjpeg DVR2Mjpeg = (DVR2Mjpeg)Parent;
                DVR2Mjpeg.DrawActivePage(false);

                foreach (TreeNode node in DVR2Mjpeg.devForm.DevTree.Nodes)
                {
                    if (node.Name == "Device")
                    {
                        foreach (TreeNode channelnode in node.Nodes)
                        {
                            if (channelnode.Name == "Channel")
                            {
                                CHANNEL_INFO chInfo = (CHANNEL_INFO)channelnode.Tag;
                                if (chInfo.nWndIndex == m_nIndex)
                                {
                                    chInfo.nWndIndex = -1;
                                    channelnode.Tag = chInfo;
                                    break;
                                }
                            }
                        }
                    }
                }

                deleteImageOnServer();
            }
        }

        private void catchPictureToolStripMenuItem_Click(object sender, EventArgs e)
        {
            return;
            if (m_nIndex > -1 && m_iPlayhandle > 0 && m_bSaveImageStart == false)
            {
                String strPath;
                DVR2Mjpeg DVR2Mjpeg = (DVR2Mjpeg)Parent;

                foreach (TreeNode node in DVR2Mjpeg.devForm.DevTree.Nodes)
                {
                    if (node.Name == "Device")
                    {
                        foreach (TreeNode channelnode in node.Nodes)
                        {
                            if (channelnode.Name == "Channel")
                            {
                                CHANNEL_INFO chInfo = (CHANNEL_INFO)channelnode.Tag;
                                if (chInfo.nWndIndex == m_nIndex)
                                {
                                    strPath = String.Format(@"Z:\Pictures\bmp\{0}.bmp", m_nIndex + 1);

                                    bool bCatch = false;

                                    m_bSaveImageStart = true;

                                    try
                                    {
                                        if (DVR2Mjpeg.isConnected) bCatch = XMSDK.H264_DVR_LocalCatchPic(m_iPlayhandle, strPath);
                                    }
                                    catch (AccessViolationException ex)
                                    {
                                        Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss - ") + TAG + ".catchPictureToolStripMenuItem_Click.AccessViolationException", "DVR ERROR");
                                        Debug.WriteLine(ex.ToString());
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss - ") + TAG + ".catchPictureToolStripMenuItem_Click.Exception", "DVR ERROR");
                                        Debug.WriteLine(ex.ToString());
                                    }

                                    m_bSaveImageStart = false;

                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void soundToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (m_iPlayhandle <= 0)
            {
                return;
            }
            ToolStripMenuItem menuSound = (ToolStripMenuItem)sender;
            if (menuSound.Checked)
            {
                if (XMSDK.H264_DVR_CloseSound(m_iPlayhandle))
                {
                    menuSound.Checked = false;
                }
            }
            else
            {
                if (XMSDK.H264_DVR_OpenSound(m_iPlayhandle))
                {
                    menuSound.Checked = true;
                }
            }
        }

        private void talkToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (m_iPlayhandle <= 0)
            {
                return;
            }
            ToolStripMenuItem menuTalk = (ToolStripMenuItem)sender;
            if (menuTalk.Checked)
            {
                if (XMSDK.H264_DVR_StopVoiceCom(m_iTalkhandle))
                {
                    menuTalk.Checked = false;
                }
            }
            else
            {
                m_iTalkhandle = XMSDK.H264_DVR_StartLocalVoiceCom(m_lLogin);
                if (m_iTalkhandle > 0)
                {
                    menuTalk.Checked = true;
                }
            }
        }
#endregion

        void updateImageOnServer(Bitmap bm)
        {
            var stream = new MemoryStream();
            bm.Save(stream, ici, ep);
            bm.Dispose();

            DVR2Mjpeg parentForm = (DVR2Mjpeg)Parent;
            if (parentForm.HttpServer.imageData.ContainsKey((m_nIndex + 1).ToString()))
            {
                parentForm.HttpServer.imageData[(m_nIndex + 1).ToString()] = stream.GetBuffer();
            }
            else
            {
                parentForm.HttpServer.imageData.Add((m_nIndex + 1).ToString(), stream.GetBuffer());
            }
            stream.Dispose();
        }

        void deleteImageOnServer()
        {
            DVR2Mjpeg parentForm = (DVR2Mjpeg)Parent;
            parentForm.HttpServer.imageData.Remove((m_nIndex + 1).ToString());
        }
    }
}
