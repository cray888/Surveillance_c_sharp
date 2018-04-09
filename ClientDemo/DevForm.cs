using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Xml;
namespace DVR2Mjpeg
{
     
    public partial class DevForm : UserControl
    {

        Dictionary<int, DEV_INFO> m_devMap;
        Dictionary<int, DEV_INFO> m_devReconnetMap;
        DEV_INFO m_talkDevice;
        IntPtr m_lTalkHandle;		//Talk Handle
        IntPtr m_pTalkDecodeBuf;	//buffer the audio data
       
        public DevForm()
        {
            InitializeComponent();
        }
        public Dictionary<int, DEV_INFO> GetDeviceMap()
        {
            return m_devMap;
        }
        bool StartTalkPlay(int nPort)
        {
            return true;
        }
        bool StartTalk( ref DEV_INFO pDevice)
        {
            IntPtr pdev = new IntPtr();
            Marshal.StructureToPtr(pDevice, pdev, false);

            unsafe
            {
                if (pdev.ToPointer() == null)
                {
                    return false;
                }
             
                if (m_lTalkHandle.ToPointer() == null)
                {
                    return false;
                }
                else
                {
                    m_lTalkHandle = (IntPtr)XMSDK.H264_DVR_StartLocalVoiceCom(pDevice.lLoginID);
                    if (m_lTalkHandle!=(IntPtr)null)
                    {
                        return true;
                    }
                    else
                    {
                        m_lTalkHandle = (IntPtr)null; ;
                        return false;
                    }

                }

            }
          
           
        }
        bool StopTalk( ref DEV_INFO pDevice)
        {
            unsafe
            {
                if (m_lTalkHandle.ToPointer() != null)
                {
                    XMSDK.H264_DVR_StopVoiceCom(m_lTalkHandle.ToInt32());
                    m_lTalkHandle = (IntPtr)null;
                    return true;
                }
                return false;
            }
      
        }
        bool StopTalkPlay(int nPort)
        {
            return true;
        }
        bool SendTalkData(IntPtr pDataBuffer, uint dwDataLength)
        {
            return true;
        }
        bool InputTalkData(IntPtr pBuf, uint nBufLen)
        {
            return true;
        }
        public DEV_INFO ReadXML()
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;
            XmlReader xml = XmlReader.Create(".\\UserInfo.xml", settings);

            DEV_INFO devInfo = new DEV_INFO();

            while (xml.ReadToFollowing("ip"))
            {
                //read the information from XML
                string strIP = "", strUserName = "", strPsw = "", strDevName = "";
                uint nPort = 0;
                int byChanNum = 0, lID = 0;

                uint bSerialID = 0, nSerPort = 0;
                string szSerIP = "", szSerialInfo = "";
                xml = xml.ReadSubtree();

                while (xml.Read())
                {
                    if (xml.NodeType == XmlNodeType.Element)
                    {
                        if (xml.Name == "ip") continue;
                        string name = xml.Name;
                        xml.Read();
                        string value = xml.Value;
                        switch (name)
                        {
                            case "ip2":
                                strIP = value;
                                break;
                            case "DEVICENAME":
                                strDevName = value;
                                break;
                            case "username":
                                strUserName = value;
                                break;
                            case "port":
                                nPort = Convert.ToUInt32(value);
                                break;
                            case "pwd":
                                strPsw = value;
                                break;
                            case "byChanNum":
                                byChanNum = Convert.ToInt32(value);
                                break;
                            case "lID":
                                lID = Convert.ToInt32(value);
                                break;
                            case "bSerialID":
                                bSerialID = Convert.ToUInt32(value);
                                break;
                            case "szSerIP":
                                szSerIP = value;
                                break;
                            case "nSerPort":
                                nSerPort = Convert.ToUInt32(value);
                                break;
                            case "szSerialInfo":
                                szSerialInfo = value;
                                break;
                        }
                    }
                }

                H264_DVR_DEVICEINFO dvrdevInfo = new H264_DVR_DEVICEINFO();
                int nError;
                int nLoginID = XMSDK.H264_DVR_Login(strIP.Trim(), ushort.Parse(nPort.ToString().Trim()), strUserName, strPsw, out dvrdevInfo, out nError, SocketStyle.TCPSOCKET);

                TreeNode nodeDev = new TreeNode();
                nodeDev.Text = strDevName;
                devInfo.szDevName = strDevName;
                devInfo.lLoginID = nLoginID;
                devInfo.nPort = Convert.ToInt32(nPort);
                devInfo.szIpaddress = strIP.Trim();
                devInfo.szUserName = strUserName;
                devInfo.szPsw = strPsw;
                devInfo.NetDeviceInfo = dvrdevInfo;
                nodeDev.Tag = devInfo;
                nodeDev.Name = "Device";
                for (int i = 0; i < devInfo.NetDeviceInfo.byChanNum + devInfo.NetDeviceInfo.iDigChannel; i++)
                {
                    TreeNode nodeChannel = new TreeNode(string.Format("CAM{0}", i));
                    nodeChannel.Name = "Channel";
                    CHANNEL_INFO ChannelInfo = new CHANNEL_INFO();
                    ChannelInfo.nChannelNo = i;
                    ChannelInfo.nWndIndex = -1;
                    nodeChannel.Tag = ChannelInfo;
                    nodeDev.Nodes.Add(nodeChannel);
                }

                DevTree.Nodes.Add(nodeDev);
                DVR2Mjpeg.dictDevInfo.Add(devInfo.lLoginID, devInfo);

            }
            return devInfo;
        }

        int DevLogin(ref DEV_INFO pdev)
        {
            if (Convert.ToBoolean(pdev.bSerialID))
            {
                int maxDeviceNum = 100;
                DDNS_INFO[] pDDNSInfo = new DDNS_INFO[maxDeviceNum];
                SearchMode searchmode;
                int nReNum = 0; 		
                searchmode.nType = (int)SearchModeType.DDNS_SERIAL;
                searchmode.szSerIP = pdev.szSerIP;
                searchmode.nSerPort = pdev.nSerPort;
                searchmode.szSerialInfo = pdev.szSerialInfo;
                bool bret = Convert.ToBoolean(XMSDK.H264_DVR_GetDDNSInfo(ref searchmode, out pDDNSInfo, maxDeviceNum, out nReNum));
                if (!bret)
                {
                    return 0;
                }
                pdev.szIpaddress=pDDNSInfo[0].IP;
                pdev.nPort = pDDNSInfo[0].MediaPort;
            }

            H264_DVR_DEVICEINFO OutDev;
            int nError = 0;

            XMSDK.H264_DVR_SetConnectTime(3000, 1);

            int lLogin = XMSDK.H264_DVR_Login(pdev.szIpaddress, Convert.ToUInt16(pdev.nPort), pdev.szUserName,
                pdev.szPsw, out OutDev,  out nError,SocketStyle.TCPSOCKET);
            if (lLogin <= 0)
            {
                int nErr = XMSDK.H264_DVR_GetLastError();
                if (nErr == (int)SDK_RET_CODE.H264_DVR_PASSWORD_NOT_VALID)
                {
                    MessageBox.Show("Error.PwdErr");
                }
                else
                {
                    MessageBox.Show("Error.NotFound");

                }
                return lLogin;
            }
            XMSDK.H264_DVR_SetupAlarmChan(lLogin);
            return lLogin;
        }
        void ReConnect(int lLoginID, string pchDVRIP, int nDVRPort)
        {

        }

        private void addDeviceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddDevForm addDevform = new AddDevForm();
            addDevform.Show();
        }

        private void DevTree_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node.Name == "Channel")
            {
                TreeNode nodeDev = e.Node.Parent;
                DEV_INFO devinfo = (DEV_INFO)nodeDev.Tag;
                CHANNEL_INFO chanInfo = (CHANNEL_INFO)e.Node.Tag;
                int iRealHandle = ((DVR2Mjpeg)this.Parent).m_videoform[((DVR2Mjpeg)this.Parent).m_nCurIndex].ConnectRealPlay(ref devinfo, chanInfo.nChannelNo);
                if ( iRealHandle > 0 )
                {
                    CHANNEL_INFO chInfo = (CHANNEL_INFO)e.Node.Tag;
                    chInfo.nWndIndex = ((DVR2Mjpeg)this.Parent).m_nCurIndex;
                    e.Node.Tag = chInfo;
                }
            }
        }
    }
}
