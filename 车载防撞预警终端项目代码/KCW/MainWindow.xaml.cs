﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ADSDK.Bases;
using ADSDK.Device;
using ADSDK.Device.Reader;
using ADSDK.Device.Reader.Passive;
using System.IO;
using System.IO.Ports;
using System.Windows.Threading;
using System.Threading;
using System.Timers;
using System.Runtime.InteropServices;

namespace WpfApplication4
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    /// 

    public partial class MainWindow : Window
    {
        //public bool IsDisposed { get; private set; }

        public MainWindow()
        {
            InitializeComponent();

            SystemPub.ADRcp = new PassiveRcp();
            SystemPub.ADRcp.RcpLogEventReceived += RcpLogEventReceived;
            SystemPub.ADRcp.RxRspParsed += RxRspEventReceived;
            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 20);
            dispatcherTimer.Start();

            InitCommunication();
            BtnStartRead.IsEnabled = false;
            //Vehicle1.Stroke = Brushes.Red;
            //Vehicle1.Fill = Brushes.Red;
            //DrawPoint();


            // dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            // dispatcherTimer.Interval = new TimeSpan(0,5,0);
            //  dispatcherTimer.Start();

        }


        #region ---RFID Reader---

        #region ---ThreadReadInfo----
        private bool m_bStopComm = true;
        private bool m_bAlive = false;

        private System.Threading.Thread monThread = null;
        private int mReadInfoCount = 0;
        private void StartReadInfo()
        {
            //ClearReaderInfo();

            if (monThread == null || monThread.IsAlive == false)
            {
                monThread = new System.Threading.Thread(new System.Threading.ThreadStart(ThreadReadInfo));
                monThread.Start();
            }
        }

        private void ThreadReadInfo()
        {
            while (!m_bAlive && SystemPub.ADSio.bConnected)
            {
                System.Threading.Thread.Sleep(20);

                // Protocol Parameters
                if (!PassiveCommand.GetInformation(SystemPub.ADRcp))
                //if (!SystemPub.ADRcp.SendBytePkt(SystemPub.ADRcp.BuildCmdPacketByte(PassiveRcp.RCP_MSG_GET, PassiveRcp.RCP_CMD_INFO, null)))
                {
                    mReadInfoCount++;
                    if (mReadInfoCount > 3)
                    {
                        mReadInfoCount = 0;
                        if (m_bStopComm) break;
                        SwitchPortAndBaud();
                    }
                    continue;
                }

                mReadInfoCount = 0;
                System.Threading.Thread.Sleep(100);

                // this.tsStatusInfo.Text = "  Ready..";
            }

            System.Console.WriteLine(" End ThreadReadInfo()");
        }
        /*
                int mPortIndex = 0;
                string[] mPortNameArray;
                private void GetPortName()
                {
                    mPortNameArray = SerialPort.GetPortNames();
                    for (int i = 0; i < mPortNameArray.Length; i++)
                    {
                        if (IniSettings.PortName == mPortNameArray[i])
                        {
                            mPortIndex = i;
                            return;
                        }
                    }
                    mPortIndex = 0;
                }

                private string GetNextPortName()
                {
                    if (mPortNameArray.Length <= 0) return "";
                    mPortIndex++;
                    if (mPortIndex >= mPortNameArray.Length) mPortIndex = 0;
                    return mPortNameArray[mPortIndex];
                }
        */
        private void SwitchPortAndBaud()
        {
            if (IniSettings.Communication == IniSettings.CommType.SERIAL)
            {
                this.Dispatcher.Invoke(new Action(delegate ()
                {
                    SystemPub.ADSio.DisConnect();
                    /*
                    if (IniSettings.HostPort == 9600)
                    {
                        IniSettings.BaudRate = 115200;
                    }
                    else
                    {
                        IniSettings.PortName = GetNextPortName();
                        IniSettings.BaudRate = 9600;
                    }
                    */
                    LoadCommType(IniSettings.Communication);
                    SystemPub.ADSio.Connect(IniSettings.HostName, IniSettings.HostPort);
                }));
            }
        }
        #endregion

        #region ---Communication---
        bool cAddNew = false;
        public string ReaderMode = "";

        private void LoadCommType(IniSettings.CommType type)
        {
            IniSettings.CommType localtemp = IniSettings.Communication;
            IniSettings.Communication = type;
            InitCommunication();
        }
        private void InitCommunication()
        {
            UnInitCommunication();
            //Serial Port Initialization
            IniSettings.Communication = IniSettings.CommType.SERIAL;
            IniSettings.PortName = "COM10";
            IniSettings.BaudRate = 9600;
            SystemPub.ADSio = new ADCom();
            SystemPub.ADSio.StatusConnected += Instance_Connected;
            SystemPub.ADRcp.Sio = SystemPub.ADSio;
            cAddNew = true;
        }
        #endregion
        private void UnInitCommunication()
        {
            if (!cAddNew) return;
            SystemPub.ADSio.StatusConnected -= Instance_Connected;
            cAddNew = false;
        }

        #region ---DoEvents_Subtitution---
        public void DoEvents()
        {
            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                new DispatcherOperationCallback(ExitFrame), frame);
            Dispatcher.PushFrame(frame);
        }

        public object ExitFrame(object f)
        {
            ((DispatcherFrame)f).Continue = false;

            return null;
        }
        #endregion

        void RcpLogEventReceived(object sender, StringEventArg e)
        {
            //DisplayMsgString(e.Data);
            //MessageBox.Show(e.Data);
        }

        private void DisplayMsgString(string data)
        {
            //throw new NotImplementedException();
            MessageBox.Show(data);
            //Status.Text = data;
        }

        void RxRspEventReceived(object sender, ProtocolEventArg e)
        {
            //if (this.IsDisposed)
            //    return;

            if (this.Dispatcher.CheckAccess())
            {
                __ParseRsp(e.Protocol);
                return;
            }

            Dispatcher.Invoke(new Action(delegate ()
             {
                 __ParseRsp(e.Protocol);
             }));
        }

        // ucPassive ucPassive1 = new ucPassive();
        private void __ParseRsp(ProtocolStruct Data)
        {
            switch (Data.Code)
            {
                case PassiveRcp.RCP_CMD_INFO:
                    if (Data.Length > 0 && Data.Type == 0)
                    {
                        m_bAlive = true;
                        #region ---Parameter---
                        string strInfo = Encoding.ASCII.GetString(Data.Payload, 0, Data.Length);

                        SystemPub.ADRcp.Type = strInfo.Substring(17, 1);
                        SystemPub.ADRcp.Mode = strInfo.Substring(18, 1);
                        SystemPub.ADRcp.Version = strInfo.Substring(19, 5);
                        //   MessageBox.Show(strInfo.Substring(29, 5));
                        bool canConvert = int.TryParse(strInfo.Substring(29, 5), out SystemPub.ADRcp.Address);
                        if (canConvert == true)
                        { }
                        else {
                            SystemPub.ADSio.DisConnect();
                            InitCommunication();
                            m_bStopComm = true;
                            BtnConnect.Content = "Connect";
                            BtnConnect.Foreground = Brushes.Black;
                            BtnStartRead.Content = "Start Read";
                        }

                        //SystemPub.ADRcp.Address = Convert.ToInt32(strInfo.Substring(29, 5));

                        if (SystemPub.ADRcp.Type != "W" && SystemPub.ADRcp.Type != "T")
                            SystemPub.ADRcp.Type = "C";
                        ReaderMode = SystemPub.ADRcp.Mode + SystemPub.ADRcp.Type;

                        // tsFWVersion.Text = IniSettings.AppsLanguage == IniSettings.LngType.ENG ?
                        //     "Type:" + ReaderMode + " - Version:" + SystemPub.ADRcp.Version + " - Address: " + SystemPub.ADRcp.Address :
                        //     "类型:" + ReaderMode + " - 版本:" + SystemPub.ADRcp.Version + " - 地址: " + SystemPub.ADRcp.Address;
                        #endregion

                        switch (ReaderMode.Substring(0, 1))
                        {
                            case "P":
                                //   ucPassive1.Show();
                                //  ucPassive1.Parent = pnlInformation;
                                //  ucPassive1.Dock = DockStyle.Fill;
                                SystemPub.ADRcp.Sio = SystemPub.ADSio;
                                break;
                        }
                    }
                    break;
            }

            switch (ReaderMode.Substring(0, 1))
            {
                case "P":
                    ParseRsp(Data);
                    // pDisplayStatusInfo(Data.Code, Data.Type, Data.Length);
                    break;
            }
        }
        void ParseRsp(ProtocolStruct Data) {
            switch (Data.Code)
            {
                case PassiveRcp.RCP_CMD_INFO:
                    if (Data.Length > 0 && Data.Type == 0)
                    {
                        #region ---Parameter---
                        string strInfo = Encoding.ASCII.GetString(Data.Payload, 0, Data.Length);

                        /*
                        if (Data.Payload[17] == 'W')
                        {
                            if (!this.tabPassive.TabPages.Contains(this.tabWifiSettings))
                            {
                                this.tabPassive.TabPages.Add(this.tabWifiSettings);
                                this.tabPassive.Refresh();
                            }
                        }
                        else
                        {
                            if (this.tabPassive.TabPages.Contains(this.tabWifiSettings))
                            {
                                this.tabPassive.TabPages.Remove(this.tabWifiSettings);
                                this.tabPassive.Refresh();
                            }
                        }
                        */

                        SystemPub.ADRcp.Mode = strInfo.Substring(18, 1);
                        SystemPub.ADRcp.Version = strInfo.Substring(19, 5);
                        SystemPub.ADRcp.Address = Convert.ToInt32(strInfo.Substring(29, 5));

                        //ucWifiSettings1.mADRcp = SystemPub.ADRcp;
                        #endregion

                        ResetOperation();
                    }
                    break;
                case PassiveRcp.RCP_CMD_EPC_IDEN:
                    if (Data.Length > 0 && Data.Type == 0)
                    {
                        //utxtCard.InputMask = GetUserTextBoxMask(Convert.ToInt32(Data.Length - 1));
                        CardID.Text = ConvertData.ByteArrayToHexString(Data.Payload, 1, Data.Length - 1);

                    }
                    break;
                    /*
                    case PassiveRcp.RCP_CMD_EPC_MULT:
                    case PassiveRcp.RCP_CMD_ISO6B_IDEN:
                        if (Data.Length > 0 && (Data.Type == 0 || Data.Type == 0x32))
                        {
                            CardID.Text = ConvertData.ByteArrayToHexString(Data.Payload, 1, Data.Length - 1);
                        }
                        break;
                    case 0x22:
                        Data.Code = 0x10;
                        Data.Type = 0x32;
                        List<CardParameters> tempArray2 = new List<CardParameters>();
                        List<byte> bytTempArray2 = new List<byte>(Data.ToArray());
                        if (PDataManage.InputManage(ref bytTempArray2, ref tempArray2))
                        {
                            //cdgvShow.Add(tempArray2);
                            MessageBox.Show("Not Handled");

                        }
                        break;
                            */


            }

        }

        private bool processing = false;
        private System.Threading.Thread SyncThread = null;
        private void ResetOperation()
        {
            DoEvents();
            System.Threading.Thread.Sleep(200);
            if (processing) return;

            if (SyncThread == null || SyncThread.IsAlive == false)
            {
                SyncThread = new System.Threading.Thread(new System.Threading.ThreadStart(syncParameters));
                SyncThread.Start();
            }
        }

        private void syncParameters()
        {
            processing = true;
            System.Threading.Thread.Sleep(20);

            // Protocol Parameters
            PassiveCommand.GetConfig(SystemPub.ADRcp);
            //if (!SystemPub.ADRcp.SendBytePkt(PassiveRcp.GetConfig(SystemPub.ADRcp.Address))) { }

            System.Threading.Thread.Sleep(20);

            if (IniSettings.Communication != IniSettings.CommType.USB)
            {
                PassiveCommand.GetTcpip(SystemPub.ADRcp);
                //if (!SystemPub.ADRcp.SendBytePkt(PassiveRcp.GetTcpip(SystemPub.ADRcp.Address))) { }
            }
            processing = false;
        }

        void Instance_Connected(object sender, ConnectEventArg e)
        {

            try
            {
                this.Dispatcher.Invoke(new Action(delegate ()
                {
                    if (e.Status == CommState.CONNECT_OK || e.Status == CommState.CONNECT_FAIL)
                    {
                        DoEvents();
                        if (e.Status == CommState.CONNECT_OK)
                        {
                            // tsStatusPortOpen.Text = "CONNECT";
                            // DisplayMsgString("CONNECT> Connect Succeed...   " + "(" + SystemPub.ADSio.ToString() + ")\r\n");
                            //tsmiConnect.Text = IniSettings.GetLanguageString("DIS&CONNECT", "断开(&C)");
                            StartReadInfo();
                            //RFID_Com_Label.Foreground = Brushes.Red;
                            BtnConnect.Content = "Disconnect";
                            BtnConnect.Foreground = Brushes.Red;
                            BtnStartRead.IsEnabled = true;
                        }
                        else if (e.Status == CommState.CONNECT_FAIL)
                        {
                            //m_bAlive = false;
                            DisplayMsgString("ERROR> " + e.Msg + "(" + SystemPub.ADSio.ToString() + ")\r\n");
                            // tsmiConnect.Text = IniSettings.GetLanguageString("&CONNECT", "联机(&C)");
                        }

                        //tsmiComm.Enabled = !tsmiRCPLogging.Visible;
                    }
                    else if (e.Status == CommState.DISCONNECT_OK || e.Status == CommState.DISCONNECT_FAIL || e.Status == CommState.DISCONNECT_EXCEPT)
                    {
                        //tsmiConnect.Enabled = true;
                        if (e.Status == CommState.DISCONNECT_OK)
                        {
                            m_bAlive = false;
                            DisplayMsgString("CONNECT> DisConnect succeed...  " + "(" + SystemPub.ADSio.ToString() + ")\r\n");
                        }
                        else if (e.Status == CommState.DISCONNECT_EXCEPT)
                        {
                            DisplayMsgString("ERROR> Error communication to disconnect...  " + "(" + SystemPub.ADSio.ToString() + ")\r\n");
                        }
                        else
                        {
                            DisplayMsgString("ERROR> " + e.Msg + "(" + SystemPub.ADSio.ToString() + ")\r\n");
                        }

                        //   tsmiConnect.Text = IniSettings.GetLanguageString("&CONNECT", "联机(&C)");

                        //  if (ucPassive1 != null) ucPassive1.Hide();

                        //   pnlInformation.Controls.Clear();

                        if (IniSettings.Communication != IniSettings.CommType.USB && m_bAlive)
                        {
                            m_bAlive = false;
                            //tsmiConnect_Click(new object(), new EventArgs());
                        }
                    }
                }));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }




        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            BtnClose.IsEnabled = false;
            /*
             if (SystemPub.ADSio.bConnected)
             {
                 SystemPub.ADSio.DisConnect();
             }
             */
            //if (IsLidarRuning) {
            //    BtnLidarConnect_Click(sender, e);
            //}
            System.Environment.Exit(0);
            System.Windows.Application.Current.Shutdown();
        }

        //private void BtnReadCard_Click(object sender, RoutedEventArgs e)
        //{
        //    BtnReadCard.IsEnabled = false;
        //    CardID.Text = "";
        //    //Application.DoEvents();
        //    PassiveCommand.Identify6C(SystemPub.ADRcp);
        //    //if (!SystemPub.ADRcp.SendBytePkt(PassiveRcp.Identify6C(SystemPub.ADRcp.Address))) { }
        //    BtnReadCard.IsEnabled = true;
        //}

        private void CardID_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            // BtnConnect.IsEnabled = false;
            DoEvents();
            if (SystemPub.ADSio.bConnected)
            {
                SystemPub.ADSio.DisConnect();
                InitCommunication();
                m_bStopComm = true;
                BtnConnect.Content = "Connect";
                BtnConnect.Foreground = Brushes.Black;
                BtnStartRead.Content = "Start Read";
            }
            else
            {

                m_bStopComm = false;
                SystemPub.ADRcp.Sio = SystemPub.ADSio;
                SystemPub.ADSio.Connect(IniSettings.HostName, IniSettings.HostPort);
                DoEvents();
                //if (!SystemPub.ADSio.bConnected && IniSettings.Communication == IniSettings.CommType.NET) fwt.ShowDialog();
                //DoEvents();
            }
            /*
            //tsmiConnect.Enabled = false;
            DoEvents();            
            if (SystemPub.ADSio.bConnected)
            {
                SystemPub.ADSio.DisConnect();
                // m_bStopComm = true;
            }
            else
            {
                //  m_bStopComm = false;
                //SystemPub.ADRcp.Sio = SystemPub.ADSio;
                SystemPub.ADSio.Connect(IniSettings.HostName, IniSettings.HostPort);
             //   DoEvents();
                //if (!SystemPub.ADSio.bConnected && IniSettings.Communication == IniSettings.CommType.NET) RFID_Com_Label.Foreground = Brushes.Red;
                //DoEvents();
            }
            */


        }

        private void Status_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
        bool IsStart = false;
        private void BtnStartRead_Click(object sender, RoutedEventArgs e)
        {
            if (!m_bStopComm) IsStart = !IsStart;
            else MessageBox.Show("Please Connect First");
            if (!IsStart)
            {
                BtnStartRead.Content = "Start Read";
            }
            else
            {
                BtnStartRead.Content = "Stop Read";
            }
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            if (IsStart & !m_bStopComm)
            {
                DoEvents();
                //CardID.Text = "";
                //Application.DoEvents();
                PassiveCommand.Identify6C(SystemPub.ADRcp);
                //PassiveCommand.Identify6CMult(SystemPub.ADRcp);
            }
        }
        #endregion


        //private void Btn_Add_Click(object sender, RoutedEventArgs e)
        //{
        //    Test_Function fun = new Test_Function();
        //    MessageBox.Show(Convert.ToString(fun.menberFuncAdd(4, 5)));
        //}

        #region ---LS210 Lidar---
        [DllImport("Osight_LS210_DLL.dll", EntryPoint = "LS_connect", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int LS_connect(string hostPC, int portPC);
        [DllImport("Osight_LS210_DLL.dll", EntryPoint = "isConnected", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern bool isConnected();
        [DllImport("Osight_LS210_DLL.dll", EntryPoint = "ParaSync", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int ParaSync(ref PARA_SYNC_RSP g_stRealPara);
        [DllImport("Osight_LS210_DLL.dll", EntryPoint = "ParaConfiguration", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int ParaConfiguration(ref PARA_SYNC_RSP g_stRealPara);
        [DllImport("Osight_LS210_DLL.dll", EntryPoint = "StartMeasureTransmission", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void StartMeasureTransmission();
        [DllImport("Osight_LS210_DLL.dll", EntryPoint = "GetLidarMeasData", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int GetLidarMeasData(ref PARA_SYNC_RSP g_stRealPara, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] int[] Distance);

        //LSxxx laser = new LSxxx();
        private void LS210()
        {
            PARA_SYNC_RSP g_stRealPara = new PARA_SYNC_RSP();
            //[System.Runtime.InteropServices.MarshalAs(UnmanagedType.ByValArray, SizeConst = 2000)]
            int[] Distance = new int[2000];

            //MEAS_DATA_NO_INTENSITY g_stMeasDataNoIntensity = new MEAS_DATA_NO_INTENSITY();

            //int[] res = new int[10];
            //POINT0[] DataIntensity0 = new POINT0[2000];

            int err;
            //byte i = 0;
            string hostPC = "192.168.1.100";

            Int32 portPC = 5500;
            byte IntensityStatus = 0;

            // Create a different thread
            while (true)
            {
                LS_connect(hostPC, portPC);

                if (!isConnected())
                {
                    continue;
                }

                this.Dispatcher.Invoke(new Action(delegate ()
                {
                    LidarConnectionStatus.Text = "Network connection OK";
                    DoEvents();
                }));


                do
                {
                    err = ParaSync(ref g_stRealPara);
                } while (err != 0);
                this.Dispatcher.Invoke(new Action(delegate ()
                {
                    LidarConnectionStatus.Text = "Parameter synchronization OK";
                    DoEvents();
                }));

                //g_stRealPara.ucIntensityStatus = 0;
                //g_stRealPara.ucIntensityStatus = 1;
                //g_stRealPara.ucIntensityStatus = 2;

                g_stRealPara.ucIntensityStatus = IntensityStatus;

                do
                {
                    err = ParaConfiguration(ref g_stRealPara);
                } while (0 != err);
                this.Dispatcher.Invoke(new Action(delegate ()
                {
                    LidarConnectionStatus.Text = "Parameter configuration OK";
                    DoEvents();
                }));

                StartMeasureTransmission();
                this.Dispatcher.Invoke(new Action(delegate ()
                {
                    LidarConnectionStatus.Text = "Start getting the Measurements ...";
                    DoEvents();
                }));

                while (true)
                {
                    this.Dispatcher.Invoke(new Action(delegate ()
                    {
                        LidarConnectionStatus.Text = "Start getting the Measurements ...";
                        DoEvents();
                    }));

                    err = GetLidarMeasData(ref g_stRealPara, Distance);
                    if (0 == err)
                    {

                        bool warning = false;
                        double[] angle_deg = Enumerable.Range(0, 1080).Select(x => x * 0.25 - 45).ToArray();
                        /*test: Print receiving ridar data */
                        for (int i = 0; i < 1080; i++)
                        {

                            double x_cor = Convert.ToInt16(0.01 * Distance[i] * Math.Cos(angle_deg[i] / 180 * Math.PI));
                            double y_cor = Convert.ToInt16(0.01 * Distance[i] * Math.Sin(angle_deg[i] / 180 * Math.PI));                   
                            

                            this.Dispatcher.Invoke(new Action(delegate ()
                            {
                                // LidarData.Text = Convert.ToString(Distance[i]);
                                //LidarData.Text = Convert.ToString(angle_deg[i]); 
                                Ellipse dataEllipse = new Ellipse();
                                dataEllipse.Fill = new SolidColorBrush(Color.FromRgb(0xff, 0, 0));
                                dataEllipse.Width = 4;
                                dataEllipse.Height = 4;

                                Canvas.SetLeft(dataEllipse, 215 + x_cor - 2);//-2是为了补偿圆点的大小，到精确的位置
                                Canvas.SetTop(dataEllipse, 250 - y_cor - 2);

                                Point point_cloud = new Point(215 + x_cor, 250 - y_cor);
                                if (pt_poly.Count>=2)
                                {
                                    if (pnpoly(point_cloud, pt_poly.ToArray()))
                                    {

                                        warning = true;

                                    }
                                    else
                                    {
                                        //polyline.Fill = null;

                                    }
                                }
                                
                                //将数据点在画布中的位置保存下来
                                PointCloudCanvas.Children.Add(dataEllipse);

                            }));
                            

                        }

                        this.Dispatcher.Invoke(new Action(delegate ()
                        {
                            if (warning) {
                                polyline.Fill = Brushes.Red;
                            }
                            else
                            {
                                polyline.Fill = null;
                            }
                            for (int index = PointCloudCanvas.Children.Count - 1; index >= 0; index--)
                            {

                                if (PointCloudCanvas.Children[index] is Ellipse)
                                {

                                    PointCloudCanvas.Children.RemoveAt(index);

                                }

                            }
                        }));

                    }
                    else
                    {
                        //break;
                    }

                }

            }

        }

            
        private bool pnpoly(Point pt_test, Point[] pt_poly) {

            bool flag = false;
            double px;
            int n_vert = pt_poly.Length;

            if (n_vert <= 2) MessageBox.Show("Setting Warning Area");

            for (int i = 0; i < n_vert; i++) {

                int j = i + 1;
                if (j >= n_vert-1) j = 0;

                if (pt_test.X == pt_poly[i].X && pt_test.Y == pt_poly[i].Y || pt_test.X == pt_poly[j].X && pt_test.Y == pt_poly[j].Y)
                {
                    flag = true;
                    return flag;
                }

                if ((pt_poly[i].Y < pt_test.Y && pt_poly[j].Y >= pt_test.Y) || (pt_poly[i].Y >= pt_test.Y && pt_poly[j].Y < pt_test.Y))
                {
                    px = pt_poly[i].X + (pt_test.Y - pt_poly[i].Y) * (pt_poly[j].X - pt_poly[i].X) / (pt_poly[j].Y - pt_poly[i].Y);
                    if (pt_test.X < px)
                    {
                        flag = !flag;
                    }
                    else if (pt_test.X == px) {
                        flag = true;
                        return flag;

                    }
                }
            }

            return flag;
        }
      

        //private void test(){
        //    while(true)
        //    MessageBox.Show("Hello World");
        //}

        bool IsLidarRuning = false; 
        private void BtnLidarConnect_Click(object sender, RoutedEventArgs e)
        {
            LidarConnectionStatus.Text = "";
            System.Threading.Thread LidarThread = new System.Threading.Thread(new System.Threading.ThreadStart(LS210));
            if (!IsLidarRuning) {
                LidarThread.Start();
                //BtnLidarConnect.Content = "Disconnect Lidar";
                BtnLidarConnect.IsEnabled = false;
            }
            else {
                LidarThread.Abort();
                DoEvents();
                BtnLidarConnect.Content = "Connect Lidar";                
            }
            IsLidarRuning = !IsLidarRuning;
            //LS210("192.168.1.100", 5500, 0);
        }


        List<Point> pt_poly = new List<Point>();
        List<Ellipse> polyEllipseList = new List<Ellipse>();
        bool IsSettingWarningArea = false;
        //private void PointCloudCanvas_MouseMove(object sender, MouseEventArgs e)
        //{
        //    if (flag == false)
        //        return;
        //    polyline.Points[polyline.Points.Count - 1] = e.GetPosition(PointCloudCanvas);
        //    Console.Write (polyline.Points[polyline.Points.Count - 1]);
        //}

        private void PointCloudCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsSettingWarningArea && polyline.Points.Count <= 5)
            {
               
                Ellipse polyEllipse = new Ellipse();
                polyEllipse.Fill = new SolidColorBrush(Color.FromRgb(0xff, 0, 0));
                polyEllipse.Width = 4;
                polyEllipse.Height = 4;
                Point p = Mouse.GetPosition(PointCloudCanvas);
                Canvas.SetLeft(polyEllipse, p.X - 2);//-2是为了补偿圆点的大小，到精确的位置
                Canvas.SetTop(polyEllipse, p.Y - 2);
                PointCloudCanvas.Children.Add(polyEllipse);
                //MessageBox.Show("Left Down");          
                polyEllipseList.Add(polyEllipse);
                pt_poly.Add(p);
                polyline.Points.Add(p);
                //if (polyline.Points.Count == 1)  polyline.Points.Add(p);
                Console.WriteLine(p);

                if (polyline.Points.Count == 5)
                {
                    polyline.Points.Add(polyline.Points[0]);
                    IsSettingWarningArea = false;
                    BtnSetWarningArea.IsEnabled = true;                  

                }
                
                //PointCloudCanvas_MouseLeftButtonDown.IsEnabled = false;
            }
            else
            {
                
            }           
            
            //Console.WriteLine("Left Down: " + e.GetPosition(PointCloudCanvas));
            //Console.WriteLine(polyline.Points.Count);
           // pt_poly.ForEach(i => Console.Write("{0}\t", i));
            //Console.WriteLine(polyline.Points);
            //Console.WriteLine(pt_poly[1].X);
            //Console.WriteLine(pt_poly[1].Y);
        }

        private void PointCloudCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
          
            Console.WriteLine("Right Down: " + e.GetPosition(PointCloudCanvas));
        }
        private void BtnSetWarningArea_Click(object sender, RoutedEventArgs e)
        {
            BtnSetWarningArea.IsEnabled = false;
            IsSettingWarningArea = true;
            polyline.Points.Clear();
            foreach (Ellipse ellipse in polyEllipseList) {
                PointCloudCanvas.Children.Remove(ellipse);
            }
            pt_poly.Clear();
            polyline.Fill = null;
        }


        #endregion
        [System.Runtime.InteropServices.StructLayout(LayoutKind.Sequential)]
        public struct IGR{
            public int igr_a;
            public int igr_b;
        };
        [System.Runtime.InteropServices.StructLayout(LayoutKind.Sequential)]
        public struct IGR_GEN_T {
            public int aa_disable; /*/< authentiation adjust checking disable */
            public int badtag_rej; /*/< reject packet if it is bypassed due to badtag */
            public int pad_en; /*/< pad non-rejected packets up to 64B */
            public int byp_ctl_sl; /*/< bypass packet if SL field does not correspond to packet len */
            public int byp_ctl_v; /*/< bypass packet if V bit is set */
            public int byp_ctl_sc; /*/< bypass packet if SC bit and either ES or SCB bits are set */
            public int byp_ctl_ec; /*/< bypass packet if DC bits are not 00 or 11 */
            public int sectag_flag; /*/< select which flag bit indicates that a SEC tag was present in pkt */
            public IGR sis;
        }
        
        [DllImport("Osight_LS210_DLL.dll", EntryPoint = "struct_test", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int struct_test(int port, ref IGR_GEN_T igr_gen);
        IGR_GEN_T igr_gen = new IGR_GEN_T();
        //IGR igr = new IGR();
        [DllImport("Osight_LS210_DLL.dll", EntryPoint = "char_test", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void char_test(string readchar, StringBuilder returnchar, int len);

        private void BtnTestStruct_Click(object sender, RoutedEventArgs e)
        {


            Point p = new Point(215,250);
            

            if (pnpoly(p, pt_poly.ToArray()))
            {
                
                polyline.Fill = Brushes.Red;
                BtnTestStruct.IsEnabled = true;
            }
            else
            {
                polyline.Fill = null;               
                BtnTestStruct.IsEnabled = true;
            }


            //StringBuilder returnchar = new StringBuilder();
            //StringBuilder returnchar1 = new StringBuilder();
            //StringBuilder returnchar2 = new StringBuilder();
            //string readchar = "Hello World";
            //string readchar1 = "Hello";
            //string readchar2 = "World";
            //char_test(readchar, returnchar, 10);
            //MessageBox.Show(Convert.ToString(returnchar));

            //char_test(readchar1, returnchar1, 10);
            //MessageBox.Show(Convert.ToString(returnchar1));


            //// NOT WORKING WITH EXPORT CLASS !!!!!
            //char_test(readchar2, returnchar2, 10);
            //MessageBox.Show(Convert.ToString(returnchar2));
            //// string hostPC = "192.168.1.100";

            //// //Int32 portPC = 5500;
            ////// byte IntensityStatus = 0;

            //// // Create a different thread
            //// //while (true)
            //// //{
            //// LSxxx.connect(hostPC, 5500);

            //char readchar = 'a';
            //char readchar1 = 'b';
            //char outchar = char_test(readchar);
            //MessageBox.Show(Convert.ToString(outchar));
            ////int a = 300;
            ////string readchar1 = "Hello1";
            //char outchar1 = char_test(readchar1);
            //MessageBox.Show(Convert.ToString(outchar1));
            //int a = 3000;
            //string_test st = new string_test();
            //string_test.read_char_int(readchar, a);

            //int port = 1;
            //igr_gen.aa_disable = 1;
            //igr_gen.byp_ctl_v = 2;
            //igr_gen.sis.igr_a = 3;
            //int ret = struct_test(port, ref igr_gen);
            //MessageBox.Show(Convert.ToString(ret));
            //igr_gen.aa_disable = 3;
            //igr_gen.byp_ctl_v = 4;
            //igr_gen.sis.igr_a = 5;
            //int ret2 = struct_test(port, ref igr_gen);  
            //MessageBox.Show(Convert.ToString(ret2));
        }

     
    }

}
