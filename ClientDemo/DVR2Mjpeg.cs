using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;
using rtaNetworking.Streaming;

namespace DVR2Mjpeg
{
    public partial class DVR2Mjpeg : Form, IWebServerCallback
    {
        //Service variable
        private String TAG = "DVR2Mjpeg";

        //HTTP server variable
        private ImageStreamingServer _Server;
        private int[] n_clientConnected = new int[32];

        //DVR variable
        public PTZForm m_formPTZ;
        public DevConfigForm m_formCfg;
        public VideoForm[] m_videoform = new VideoForm[32];

        public int m_nCurIndex = -1;
        public int m_nTotalWnd = 32;
        public DEV_INFO m_devInfo = new DEV_INFO();        
        //public bool m_bArray;
        public static Dictionary<int , DEV_INFO> dictDevInfo = new Dictionary<int , DEV_INFO>();
        public static Dictionary<int, DEV_INFO> dictDiscontDev = new Dictionary<int, DEV_INFO>();

        private System.Timers.Timer timerDisconnect = new System.Timers.Timer(30000);
        private System.Timers.ElapsedEventHandler reconnect;
        private XMSDK.fDisConnect disCallback;
        private XMSDK.fMessCallBack msgcallback;        

        bool  MessCallBack(int  lLoginID, string pBuf,uint dwBufLen, IntPtr dwUser)
        {
            DVR2Mjpeg form = new DVR2Mjpeg();
            Marshal.PtrToStructure(dwUser, form);
	        return form.DealwithAlarm(lLoginID,pBuf,dwBufLen);
        }

        void DisConnectBackCallFunc(int lLoginID, string pchDVRIP, int nDVRPort, IntPtr dwUser)
        {
            Debug.WriteLine(TAG + ".DisConnectBackCallFunc(" + lLoginID.ToString() + "," + pchDVRIP + "," + nDVRPort.ToString() + ",dwUser" + ")");

            for (int i = 0; i < 16; i++)
            {
                if (lLoginID == m_videoform[i].GetLoginHandle())
                {
                    m_videoform[i].OnDisconnct();
                }
            }           
          
            foreach (DEV_INFO devinfo in dictDevInfo.Values)
            {
                if (devinfo.lLoginID == lLoginID)
                {
                    XMSDK.H264_DVR_Logout(lLoginID);
                    dictDevInfo.Remove(devinfo.lLoginID);
			        dictDiscontDev.Add(devinfo.lLoginID,devinfo);
                    break;
                }
            }

            if ( dictDiscontDev.Count > 0 )
            {

                timerDisconnect.Enabled = true;
                timerDisconnect.Start();
            }
        }

        public DVR2Mjpeg()
        {
            Debug.WriteLine(TAG + ".DVR2Mjpeg()");

            InitializeComponent();

            for (int i = 0; i < 32; i++)
            {
                m_videoform[i] = new VideoForm();
                this.Controls.Add(this.m_videoform[i]);
                m_videoform[i].SetWndIndex(i);
            }            
            devForm = new DevForm();
            this.Controls.Add(devForm);
            devForm.Location = new Point(880, 10);
            devForm.Anchor = (AnchorStyles.Top | AnchorStyles.Right);  
            this.comboBoxCamCount.SelectedIndex = 4;

            InitSDK();
            devForm.ReadXML();

            reconnect = new System.Timers.ElapsedEventHandler(ReConnect);
            GC.KeepAlive(reconnect);
            timerDisconnect.Elapsed += new System.Timers.ElapsedEventHandler(reconnect); 
       
            ArrayWindow(32);
            SetActiveWnd(0);   
        }

        public DVR2Mjpeg(bool noInit) { }

        private void OpenChanel(int indexWind, int chanelID, bool savePicture)
        {
            Debug.WriteLine(TAG + ".OpenChanel(" + indexWind.ToString() + "," + chanelID + "," + savePicture.ToString() + ")");

            TreeNode nodeDev = devForm.DevTree.Nodes[0];
            DEV_INFO devinfo = (DEV_INFO)nodeDev.Tag;
            CHANNEL_INFO chanInfo = (CHANNEL_INFO)nodeDev.Nodes[chanelID].Tag;
            int iRealHandle = m_videoform[indexWind].ConnectRealPlay(ref devinfo, chanInfo.nChannelNo);
            if (iRealHandle > 0)
            {
                chanInfo.nWndIndex = indexWind;
                nodeDev.Nodes[chanelID].Tag = chanInfo;

                if (savePicture) m_videoform[indexWind].setSavePicture(savePicture);
            }
        }

        private void CloseChanel(int indexWind)
        {
            m_videoform[indexWind].Close();
            m_videoform[indexWind].setSavePicture(false);
        }

        public int InitSDK()
        {
            Debug.WriteLine(TAG + ".InitSDK()");

            //initialize
            disCallback = new XMSDK.fDisConnect(DisConnectBackCallFunc);
            GC.KeepAlive(disCallback);
            int bResult = XMSDK.H264_DVR_Init(disCallback, this.Handle);

            msgcallback  = new XMSDK.fMessCallBack(MessCallBack);
            XMSDK.H264_DVR_SetDVRMessCallBack(msgcallback, this.Handle);
            XMSDK.H264_DVR_SetConnectTime(5000, 3);

            return bResult;
        }

        public bool ExitSDk()
        {
            Debug.WriteLine(TAG + ".ExitSDk()");

            return XMSDK.H264_DVR_Cleanup();
        }

        public void ArrayWindow(int iNumber)
        {
            m_nTotalWnd = iNumber;

            Rectangle rect = this.ClientRectangle;
            int  iWidth, iHeight;
            int nFullWidth = rect.Width;
            int nFullHeight = rect.Height;
            iWidth = (int)(nFullWidth * 0.75515625);
            iHeight = (int)(nFullHeight * 0.91);

            int i = 0;
            for (i = 0; i < 32; i++)
            {
                m_videoform[i].Hide();
            }
            
            int nNull = 3;
            int nCount;

            switch (iNumber)
            {
                case 1:
                    m_videoform[0].SetBounds(3+0,0,iWidth,iHeight);
                    m_videoform[0].Show();
                    break;
                case 4:
                    for (i = 0; i < 2; i++)
                    {
                        m_videoform[i].SetBounds(3 + i * (iWidth / 2) + i * nNull, 0, (iWidth / 2), iHeight / 2);
                        m_videoform[i].Show();
                    }
                    for (i = 2; i < 4; i++)
                    {
                        m_videoform[i].SetBounds(3 + (i - 2) * (iWidth / 2) + (i - 2) * nNull, iHeight / 2 + nNull, (iWidth / 2), iHeight / 2);
                        m_videoform[i].Show();
                    }
                    break;
                case 9:
                    for (i = 0; i < 3; i++)
                    {
                        m_videoform[i].SetBounds(3 + i * (iWidth / 3) + i * nNull, 0, (iWidth / 3), iHeight / 3);
                        m_videoform[i].Show();
                    }
                    for (i = 3; i < 6; i++)
                    {
                        m_videoform[i].SetBounds(3 + (i - 3) * (iWidth / 3) + (i - 3) * nNull, iHeight / 3 + nNull, (iWidth / 3), iHeight / 3);
                        m_videoform[i].Show();
                    }
                    for (i = 6; i < 9; i++)
                    {
                        m_videoform[i].SetBounds(3 + (i - 6) * (iWidth / 3) + (i - 6) * nNull, 2 * iHeight / 3 + 2 * nNull, (iWidth / 3), iHeight / 3);
                        m_videoform[i].Show();
                    }
                    break;
                case 16:
                    for (i = 0; i < 4; i++)
                    {
                        m_videoform[i].SetBounds(
                            3 + i * (iWidth / 4) + (i) * nNull, 
                            0, 
                            (iWidth / 4), 
                            iHeight / 4
                        );
                        m_videoform[i].Show();
                    }
                    for (i = 4; i < 8; i++)
                    {
                        m_videoform[i].SetBounds(
                            3 + (i - 4) * (iWidth / 4) + (i - 4) * nNull, 
                            iHeight / 4 + nNull, 
                            (iWidth / 4), 
                            iHeight / 4
                        );
                        m_videoform[i].Show();
                    }
                    for (i = 8; i < 12; i++)
                    {
                        m_videoform[i].SetBounds(
                            3 + (i - 8) * (iWidth / 4) + (i - 8) * nNull, 
                            iHeight / 2 + 2 * nNull, 
                            (iWidth / 4), 
                            iHeight / 4
                        );
                        m_videoform[i].Show();
                    }
                    for (i = 12; i < 16; i++)
                    {
                        m_videoform[i].SetBounds(
                            3 + (i - 12) * (iWidth / 4) + (i - 12) * nNull, 
                            3 * iHeight / 4 + 3 * nNull, 
                            (iWidth / 4), 
                            iHeight / 4
                        );
                        m_videoform[i].Show();
                    }
                    break;
                case 32:
                    nCount = 6;
                    for (i = 0; i < 6; i++)
                    {
                        m_videoform[i].SetBounds(
                            3 + i * (iWidth / 6) + (i) * nNull,
                            0, 
                            iWidth / 6, 
                            iHeight / 6
                        );
                        m_videoform[i].Show();
                    }
                    for (i = 6; i < 12; i++)
                    {
                        m_videoform[i].SetBounds(
                            3 + (i - 6) * (iWidth / 6) + (i - 6) * nNull, 
                            iHeight / 6 + nNull, 
                            iWidth / 6, 
                            iHeight / 6
                        );
                        m_videoform[i].Show();
                    }
                    for (i = 12; i < 18; i++)
                    {
                        m_videoform[i].SetBounds(
                            3 + (i - 12) * (iWidth / 6) + (i - 12) * nNull,
                            2 * iHeight / 6 + 2 * nNull,
                            iWidth / 6,
                            iHeight / 6
                        );
                        m_videoform[i].Show();
                    }
                    for (i = 18; i < 24; i++)
                    {
                        m_videoform[i].SetBounds(
                            3 + (i - 18) * (iWidth / 6) + (i - 18) * nNull,
                            3 * iHeight / 6 + 3 * nNull,
                            iWidth / 6,
                            iHeight / 6
                        );
                        m_videoform[i].Show();
                    }
                    for (i = 24; i < 30; i++)
                    {
                        m_videoform[i].SetBounds(
                            3 + (i - 24) * (iWidth / 6) + (i - 24) * nNull,
                            4 * iHeight / 6 + 4 * nNull,
                            iWidth / 6,
                            iHeight / 6
                        );
                        m_videoform[i].Show();
                    }
                    for (i = 30; i < 32; i++)
                    {
                        m_videoform[i].SetBounds(
                            3 + (i - 30) * (iWidth / 6) + (i - 30) * nNull,
                            5 * iHeight / 6 + 5 * nNull,
                            iWidth / 6,
                            iHeight / 6
                        );
                        m_videoform[i].Show();
                    }
                    break;
                default:
                    break;
            }
        }

        public void DrawActivePage(bool bActive)
        {
            Rectangle rt = new Rectangle(m_videoform[m_nCurIndex].Left,m_videoform[m_nCurIndex].Top, m_videoform[m_nCurIndex].Width, m_videoform[m_nCurIndex].Height);
            if (!bActive)
            {
                Rectangle rtInvalidate = new Rectangle(rt.X - 1, rt.Y - 1, rt.Width + 2, rt.Height + 2);
                Invalidate(rtInvalidate, true);
            }
            else
            {
                Graphics graphic = Graphics.FromHwnd(this.Handle);
                Pen pen = new Pen(Color.Red, 2);
                graphic.DrawRectangle(pen, rt);
            }
        }

        private void SetColor(int nIndex)
        {
            int nBright = 0;
            int nHue = 0;
            int nSaturation = 0;
            int nContrast = 0;

            IntPtr lPlayHandle = m_videoform[m_nCurIndex].Handle;
            if (lPlayHandle.ToInt32() <=0 )
            {
                return;
            }
            m_videoform[nIndex].GetColor(out nBright, out nContrast, out nSaturation, out nHue);
        }

        public bool DealwithAlarm(int lDevcID, string pBuf, uint dwLen)
        {
            return true;
        }

        public bool SetDevChnColor(uint nBright, uint nContrast, uint nSaturation, uint nHue)
        {
            SDK_CONFIG_VIDEOCOLOR videocolor = new SDK_CONFIG_VIDEOCOLOR();

            for (int i = 0; i < 2; i++)
            {
                videocolor.dstVideoColor[i].tsTimeSection.enable = 1;
                videocolor.dstVideoColor[i].tsTimeSection.startHour = 0;
                videocolor.dstVideoColor[i].tsTimeSection.startMinute = 0;
                videocolor.dstVideoColor[i].tsTimeSection.startSecond = 0;
                videocolor.dstVideoColor[i].tsTimeSection.endHour = 24;
                videocolor.dstVideoColor[i].tsTimeSection.endMinute = 0;
                videocolor.dstVideoColor[i].tsTimeSection.endSecond = 0;
                videocolor.dstVideoColor[i].iEnable = 1;
                videocolor.dstVideoColor[i].dstColor.nBrightness = (int)nBright * 100 / 128;
                videocolor.dstVideoColor[i].dstColor.nHue = (int)nHue * 100 / 128;
                videocolor.dstVideoColor[i].dstColor.nSaturation = (int)nSaturation * 100 / 128;
                videocolor.dstVideoColor[i].dstColor.nContrast = (int)nContrast * 100 / 128;
                videocolor.dstVideoColor[i].dstColor.mGain = 0;
                videocolor.dstVideoColor[i].dstColor.mWhitebalance = 0;
            }
            m_videoform[m_nCurIndex].SetDevChnColor(ref videocolor);

            return true;
        }

        public void SetActiveWnd(int nIndex)
        {
            if (-1 != m_nCurIndex && m_nCurIndex != nIndex)
            {
                DrawActivePage(false);
            }
            if (m_nCurIndex!=nIndex)
            {
                   m_nCurIndex = nIndex;
            }    
            DrawActivePage(true);
            SetColor(m_nCurIndex);
        }

        public int Connect(ref DEV_INFO pDev, int nChannel, int nWndIndex)
        {
            Debug.WriteLine(TAG + ".Connect(" + pDev.szDevName + "," + nChannel.ToString() + "," + nWndIndex.ToString() + ")");

            int nRet = 0;

            //if device did not login,login first
            if (pDev.lLoginID <= 0)
            {
                H264_DVR_DEVICEINFO OutDev;
                int nError = 0;
                int lLogin = XMSDK.H264_DVR_Login(pDev.szIpaddress, (ushort)pDev.nPort, pDev.szUserName, pDev.szPsw, out OutDev, out nError, SocketStyle.TCPSOCKET);
                if (lLogin <= 0)
                {
                    int nErr = XMSDK.H264_DVR_GetLastError();
                    if (nErr == (int)SDK_RET_CODE.H264_DVR_PASSWORD_NOT_VALID)
                    {
                        MessageBox.Show(("Error.PwdErr"));
                    }
                    else
                    {
                        MessageBox.Show(("Error.NotFound"));
                    }

                    return nRet;
                }

                pDev.lLoginID = lLogin;
                XMSDK.H264_DVR_SetupAlarmChan(lLogin);
            }

            int nWnd = m_nCurIndex;
            if (nWndIndex >= 0)
            {
                nWnd = nWndIndex;
            }

            if (nWnd >= m_nTotalWnd)
            {
                return nRet;
            }

            return m_videoform[nWnd].ConnectRealPlay(ref pDev, nChannel);	
        }

        public void SetColor(uint nBright, uint nContrast, uint nSaturation, uint nHue)
        {
            IntPtr lPlayHandle = m_videoform[m_nCurIndex].Handle;
            unsafe
            {
                if (lPlayHandle.ToPointer() == null)
                {
                    return;
                }
            }
         
            m_videoform[m_nCurIndex].SetColor((int)nBright, (int)nContrast, (int)nSaturation, (int)nHue);
            SetDevChnColor(nBright, nContrast, nSaturation, nHue);
        }

        public void PtzControl(uint dwBtn, bool dwStop)
        {
            long lPlayHandle = m_videoform[m_nCurIndex].GetHandle();
            if (lPlayHandle <= 0)
            {
                return;
            }
        }

        public void KeyBoardMsg(uint dwValue, uint dwState)
        {
            IntPtr lPlayHandle = m_videoform[m_nCurIndex].Handle;
            unsafe
            {
                if (lPlayHandle.ToPointer() == null)
                {
                    return;
                }
            }
          
            SDK_NetKeyBoardData vKeyBoardData;
            vKeyBoardData.iValue = (int)dwValue;
            vKeyBoardData.iState = (int)dwState;
            m_nCurIndex = m_nCurIndex < 0 ? 0 : m_nCurIndex;
            if (!XMSDK.H264_DVR_ClickKey(m_videoform[m_nCurIndex].m_lLogin, ref vKeyBoardData))
               MessageBox.Show("AccountMSG.Failed");
        }

        public void NetAlarmMsg(uint dwValue, uint dwState)
        {
            if (m_devInfo.lLoginID > 0)
            {
                SDK_NetAlarmInfo vAlarmInfo;
                vAlarmInfo.iEvent = 0;
                vAlarmInfo.iState = (int)(dwState << (int)dwValue);
                m_nCurIndex = m_nCurIndex < 0 ? 0 : m_nCurIndex;
                if (!XMSDK.H264_DVR_SendNetAlarmMsg(m_devInfo.lLoginID, ref vAlarmInfo))
                    MessageBox.Show("AccountMSG.Failed");
            }
        }

        public void SetDevInfo(ref DEV_INFO pDev)
        {
            m_devInfo = pDev;
        }

        public void ReConnect(object source, System.Timers.ElapsedEventArgs e)
        {
            Debug.WriteLine(TAG + ".ReConnect(source,e)");

            Dictionary<int, DEV_INFO> dictDiscontDevCopy = new Dictionary<int, DEV_INFO>(dictDiscontDev);
            foreach (DEV_INFO devinfo in dictDiscontDevCopy.Values)
            {
                H264_DVR_DEVICEINFO OutDev = new H264_DVR_DEVICEINFO();
                int nError = 0;

                int lLogin = XMSDK.H264_DVR_Login(devinfo.szIpaddress, (ushort)devinfo.nPort, devinfo.szUserName, devinfo.szPsw, out OutDev, out nError, SocketStyle.TCPSOCKET);
                if (lLogin <= 0)
                {
                    int nErr = XMSDK.H264_DVR_GetLastError();
                    if (nErr == (int)SDK_RET_CODE.H264_DVR_PASSWORD_NOT_VALID)
                    {
                        MessageBox.Show(("Password Error"));
                    }
                    else if (nErr == (int)SDK_RET_CODE.H264_DVR_LOGIN_USER_NOEXIST)
                    {
                        MessageBox.Show(("User Not Exist"));
                    }

                    return;
                }
                dictDiscontDev.Remove(devinfo.lLoginID);

                DVR2Mjpeg clientForm = new DVR2Mjpeg(true);

                foreach (Form form in Application.OpenForms)
                {
                    if (form.Name == "ClientDemo")
                    {
                        clientForm = (DVR2Mjpeg)form;
                        break;
                    }
                }
                DEV_INFO devAdd = new DEV_INFO();
                devAdd = devinfo;
                devAdd.lLoginID = lLogin;

                foreach (TreeNode node in clientForm.devForm.DevTree.Nodes)
                {
                    if (node.Name == "Device")
                    {
                        DEV_INFO dev = (DEV_INFO)node.Tag;
                        if (dev.lLoginID == devinfo.lLoginID)
                        {
                            if (this.InvokeRequired)
                                this.BeginInvoke((MethodInvoker)(() =>
                                {
                                    node.Text = devAdd.szDevName;
                                    node.Tag = devAdd;
                                    node.Name = "Device";
                                }));
                            else
                            {
                                node.Text = devAdd.szDevName;
                                node.Tag = devAdd;
                                node.Name = "Device";
                            }
                            foreach (TreeNode channelnode in node.Nodes)
                            {
                                CHANNEL_INFO chInfo = (CHANNEL_INFO)channelnode.Tag;
                                if (chInfo.nWndIndex > -1)
                                {
                                    if (InvokeRequired)
                                    {
                                        BeginInvoke((MethodInvoker)(() =>
                                        {
                                            clientForm.m_videoform[chInfo.nWndIndex].ConnectRealPlay(ref devAdd, chInfo.nChannelNo);
                                        }));
                                    }
                                    else
                                    {
                                        clientForm.m_videoform[chInfo.nWndIndex].ConnectRealPlay(ref devAdd, chInfo.nChannelNo);                                        
                                    }
                                    Thread.Sleep(100);
                                }
                            }
                            break;
                        }
                    }
                }

                dictDevInfo.Add(lLogin, devAdd);
                XMSDK.H264_DVR_SetupAlarmChan(lLogin);
            }

            if (0 == dictDiscontDev.Count)
            {
                timerDisconnect.Enabled = false;
                timerDisconnect.Stop();
            }
        }       

        private void DVR2Mjpeg_Paint(object sender, PaintEventArgs e)
        {
            SetActiveWnd(m_nCurIndex);
        }

        private void comboBoxCamCount_SelectedIndexChanged(object sender, EventArgs e)
        {
            int nWndNum = 4;
            if (comboBoxCamCount.SelectedIndex == 0)
            {
                nWndNum = 1;
            }
            else if (comboBoxCamCount.SelectedIndex == 1)
            {
                nWndNum = 4;
            }
            else if (comboBoxCamCount.SelectedIndex == 2)
            {
                nWndNum = 9;
            }
            else if (comboBoxCamCount.SelectedIndex == 3)
            {
                nWndNum = 16;
            }
            else if (comboBoxCamCount.SelectedIndex == 4)
            {
                nWndNum = 32;
            }
            ArrayWindow(nWndNum);
        }

        private void DVR2Mjpeg_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {                 
    
            }
        }

        private void DVR2Mjpeg_FormClosing(object sender, FormClosingEventArgs e)
        {
            ExitSDk();
        }

        private void btnTransparent_Click(object sender, EventArgs e)
        {
            Form_Transpanrent formTransparent = new Form_Transpanrent();
            formTransparent.Show();
        }

        private void btnplayback_Click(object sender, EventArgs e)
        {
            PlayBackForm formPlayBack = new PlayBackForm();
            formPlayBack.Show();            
        }

        private void DVR2Mjpeg_FormClosed(object sender, FormClosedEventArgs e)
        {

            foreach (DEV_INFO devinfo in dictDevInfo.Values)
            {         
                XMSDK.H264_DVR_Logout(devinfo.lLoginID);  
            }
            timerDisconnect.Stop();
        }

        private void btnPTZ_Click(object sender, EventArgs e)
        {
            m_formPTZ = new PTZForm();
            m_formPTZ.StartPosition = FormStartPosition.CenterScreen;
            m_formPTZ.Owner = this;
            m_formPTZ.Show(); 
        }

        private void btnDevConfig_Click(object sender, EventArgs e)
        {
            m_formCfg = new DevConfigForm();
            m_formCfg.StartPosition = FormStartPosition.CenterScreen;
            m_formCfg.Owner = this;
            m_formCfg.Show();
        }

        private void DVR2Mjpeg_Load(object sender, EventArgs e)
        {
            OpenCams();
            _Server = new ImageStreamingServer();
            _Server.Start(8080, this);
        }

        private void OpenCams()
        {            
            /*OpenChanel(1, 1, true);
            OpenChanel(12, 12, true);
            OpenChanel(18, 18, true);
            OpenChanel(24, 24, true);*/
        }

        public void OnClientConnect(int chanel)
        {
            chanel--;

            n_clientConnected[chanel]++;

            if (n_clientConnected[chanel] == 1)
            {
                if (this.InvokeRequired)
                    this.BeginInvoke((MethodInvoker)(() => OpenChanel(chanel, chanel, true)));
                else OpenChanel(chanel, chanel, true);

                Thread.Sleep(1000);
            }
        }

        public void OnClientDisconnect(int chanel)
        {
            chanel--;

            n_clientConnected[chanel]--;

            if (n_clientConnected[chanel] == 0)
            {
                if (InvokeRequired)
                    BeginInvoke((MethodInvoker)(() => CloseChanel(chanel)));
                else CloseChanel(chanel);
            }
        }

        public void OnClientRequestShot(int chanel)
        {
            chanel--;

            n_clientConnected[chanel]++;
            if (n_clientConnected[chanel] == 1)
            {
                if (this.InvokeRequired)
                    this.BeginInvoke((MethodInvoker)(() => OpenChanel(chanel, chanel, true)));
                else OpenChanel(chanel, chanel, true);
            }

            Thread.Sleep(1000);

            n_clientConnected[chanel]--;
            if (n_clientConnected[chanel] == 0)
            {
                if (InvokeRequired)
                    BeginInvoke((MethodInvoker)(() => CloseChanel(chanel)));
                else CloseChanel(chanel);
            }
        }
    }
}