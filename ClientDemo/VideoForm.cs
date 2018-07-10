using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.Drawing.Imaging;
using System.Diagnostics;

namespace DVR2Mjpeg
{
    public partial class VideoForm : UserControl
    {
        private String TAG = "VideoForm";

        public bool m_bSaveImageStart;
        private System.Timers.Timer m_tTimer = new System.Timers.Timer(100);

	    int m_nIndex;	//index	
	    bool m_bRecord;	//is recording or not
	    bool m_bSound;

	    public int m_iPlayhandle;	//play handle
	    public int m_lLogin; //login handle
	    public int m_iChannel; //play channel
        public int m_iTalkhandle;

        public bool saveImageIsStarting = false;

        public VideoForm()
        {
            Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss - ") + TAG + ".VideoForm()", "DVR INFO");

            InitializeComponent();
            m_tTimer.Elapsed += M_tTimer_Elapsed;
            m_tTimer.AutoReset = true;
            m_tTimer.Start();
        }

        public void SetWndIndex(int nIndex)
	    {
            Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss - ") + TAG + ".SetWndIndex(" + nIndex.ToString() + ")", "DVR INFO");
            m_nIndex = nIndex;
	    }

	    public int ConnectRealPlay( ref DEV_INFO pDev, int nChannel, int nStream = 1)
        {
            Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss - ") + TAG + ".ConnectRealPlay(" + pDev.szDevName + "," + nChannel.ToString() + ")", "DVR INFO");

            if (m_iPlayhandle != -1)
	        {

                if (0 != XMSDK.H264_DVR_StopRealPlay(m_iPlayhandle, (uint)panelVideo.Handle))
		        {

		        }
		        if(m_bSound)
		        {
			        OnCloseSound();
		        }
	        }
            
            H264_DVR_CLIENTINFO playstru = new H264_DVR_CLIENTINFO();

	        playstru.nChannel = nChannel;
	        playstru.nStream = nStream;
	        playstru.nMode = 0;
            playstru.hWnd= panelVideo.Handle;
            /*if (InvokeRequired)
                BeginInvoke((MethodInvoker)(() => playstru.hWnd = this.panelVideo.Handle));
            else playstru.hWnd = this.panelVideo.Handle;*/
            m_iPlayhandle = XMSDK.H264_DVR_RealPlay(pDev.lLoginID, ref playstru);	
	        if(m_iPlayhandle <= 0 )
	        {
                Int32 dwErr = XMSDK.H264_DVR_GetLastError();
                    //StringBuilder sTemp = new StringBuilder("");
			        //sTemp.AppendFormat("access {0} channel{1} fail, dwErr = {2}",pDev.szDevName,nChannel, dwErr);
			        //MessageBox.Show(sTemp.ToString());
	        }
	        else
	        {
                XMSDK.H264_DVR_MakeKeyFrame(pDev.lLoginID, nChannel, 0);		
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
            if ( m_iPlayhandle <= 0 )
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
	        if ( m_bRecord )
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
		        for(;;)
		        {
                    int nIndex = cFilename.IndexOfAny(strPr.ToCharArray(), nTemp);
                    if (nIndex == -1)
                    {
                        break;
                    }
                    String str = cFilename.Substring(0,nIndex+1);
                    nTemp = nIndex + 1; nTemp = nIndex + 1;
                    DirectoryInfo dir = new DirectoryInfo(str);
                    if ( !dir.Exists )
                    {
                        dir.Create();
                    }
		        }

                if (XMSDK.H264_DVR_StartLocalRecord(m_iPlayhandle, cFilename, (int)MEDIA_FILE_TYPE.MEDIA_FILE_NONE))
		        {
			        m_bRecord = true ;
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
                this.BeginInvoke((MethodInvoker)(() => XMSDK.H264_DVR_StopRealPlay(m_iPlayhandle, (uint)this.panelVideo.Handle)));                
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
                Font newFont = new Font(fontfamily, 16,FontStyle.Bold);
                SolidBrush brush =  new SolidBrush(Color.Red);       

                Graphics graphic = Graphics.FromHdc(hDc);
                graphic.DrawString("TEST", newFont,brush,10,10);            
            }
        }

        public int SetDevChnColor(ref SDK_CONFIG_VIDEOCOLOR pVideoColor)
        {
            IntPtr ptr = new IntPtr();
            Marshal.StructureToPtr(pVideoColor, ptr, true);
            return XMSDK.H264_DVR_SetDevConfig(m_lLogin, (uint)SDK_CONFIG_TYPE.E_SDK_VIDEOCOLOR, m_iChannel, ptr, (uint)Marshal.SizeOf(pVideoColor), 3000);
         
        }

        static void videoInfoFramCallback(int nPort, int nType, string pBuf,int nSize, IntPtr nUser)
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

        private void M_tTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (checkBoxSaveImage.Checked && !m_bSaveImageStart)
            {
                m_bSaveImageStart = true;
                catchPictureToolStripMenuItem_Click(null, null);
                m_bSaveImageStart = false;
            }
        }

        private void VideoForm_Click(object sender, EventArgs e)
        {
            DVR2Mjpeg DVR2Mjpeg = (DVR2Mjpeg)this.Parent;
            DVR2Mjpeg.SetActiveWnd(m_nIndex);
            DVR2Mjpeg.comboBoxCamCount.Focus();
        }

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
                XMSDK.H264_DVR_StopRealPlay(m_iPlayhandle, (uint)this.panelVideo.Handle);
                DVR2Mjpeg DVR2Mjpeg = (DVR2Mjpeg)this.Parent;
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
            }
        }

        private void catchPictureToolStripMenuItem_Click(object sender, EventArgs e)
        {
            /*if (InvokeRequired)
                BeginInvoke((MethodInvoker)(() =>
                {
                    IntPtr videoHandle = panelVideo.Handle;
                    Bitmap b = new Bitmap(panelVideo.Width, panelVideo.Height); ;
                    panelVideo.DrawToBitmap(b, new Rectangle(0, 0, b.Width, b.Height));
                    b.Save(@"Z:\123.bmp");
                }));*/

            if ( m_nIndex > -1 && m_iPlayhandle > 0 && saveImageIsStarting == false)
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

                                    saveImageIsStarting = true;

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

                                    saveImageIsStarting = false;

                                    if (bCatch)
                                    {
                                        try
                                        {
                                            Bitmap bitmap = new Bitmap(strPath);
                                            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
                                            ImageCodecInfo ici = null;

                                            foreach (ImageCodecInfo codec in codecs)
                                            {
                                                if (codec.MimeType == "image/jpeg")
                                                    ici = codec;
                                            }

                                            EncoderParameters ep = new EncoderParameters();
                                            ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)50);

                                            var stream = new MemoryStream();
                                            bitmap.Save(stream, ici, ep);
                                            bitmap.Dispose();

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

                                            
                                            /*if (InvokeRequired) BeginInvoke((MethodInvoker)(() => {
                                                Bitmap bitmap = GetBitmapFromHBitmap(panelVideo.Handle);

                                                ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
                                                ImageCodecInfo ici = null;

                                                foreach (ImageCodecInfo codec in codecs)
                                                {
                                                    if (codec.MimeType == "image/jpeg")
                                                        ici = codec;
                                                }

                                                EncoderParameters ep = new EncoderParameters();
                                                ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)50);

                                                var stream = new MemoryStream();
                                                bitmap.Save(stream, ici, ep);
                                                bitmap.Dispose();

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
                                            }));*/
                                            
                                        }
                                        catch (Exception ex)
                                        {

                                        }

                                    }
                                    else
                                    {

                                    }
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
            if ( m_iPlayhandle <= 0 )
            {
                return;
            }
            ToolStripMenuItem menuSound = (ToolStripMenuItem)sender;
            if ( menuSound.Checked )   
            {
                if (  XMSDK.H264_DVR_CloseSound(m_iPlayhandle) )
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

        private void VideoForm_Load(object sender, EventArgs e)
        {
            
        }

        public void setSavePicture(bool state)
        {
            checkBoxSaveImage.Checked = state;
        }

        /*private static Bitmap GetBitmapFromHBitmap(IntPtr nativeHBitmap)
        {
            Bitmap bmp = Bitmap.FromHbitmap(nativeHBitmap);

            if (Bitmap.GetPixelFormatSize(bmp.PixelFormat) < 32)
                return bmp;

            BitmapData bmpData;

            if (IsAlphaBitmap(bmp, out bmpData))
                return GetlAlphaBitmapFromBitmapData(bmpData);

            return bmp;
        }

        private static Bitmap GetlAlphaBitmapFromBitmapData(BitmapData bmpData)
        {
            return new Bitmap(
                    bmpData.Width,
                    bmpData.Height,
                    bmpData.Stride,
                    PixelFormat.Format32bppArgb,
                    bmpData.Scan0);
        }

        private static bool IsAlphaBitmap(Bitmap bmp, out BitmapData bmpData)
        {
            Rectangle bmBounds = new Rectangle(0, 0, bmp.Width, bmp.Height);

            bmpData = bmp.LockBits(bmBounds, ImageLockMode.ReadOnly, bmp.PixelFormat);

            try
            {
                for (int y = 0; y <= bmpData.Height - 1; y++)
                {
                    for (int x = 0; x <= bmpData.Width - 1; x++)
                    {
                        Color pixelColor = Color.FromArgb(
                            Marshal.ReadInt32(bmpData.Scan0, (bmpData.Stride * y) + (4 * x)));

                        if (pixelColor.A > 0 & pixelColor.A < 255)
                        {
                            return true;
                        }
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }

            return false;
        }*/
    }
}
