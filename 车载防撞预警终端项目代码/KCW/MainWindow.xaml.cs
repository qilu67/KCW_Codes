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
            //Vehicle1.Stroke = Brushes.Red;
            //Vehicle1.Fill = Brushes.Red;
            
           
            
            // dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            // dispatcherTimer.Interval = new TimeSpan(0,5,0);
            //  dispatcherTimer.Start();

        }

        /*
        #region ---DoEvents_Subtitution---
           public static void DoEvents()
        {
             Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background,
                                         new Action(delegate { }));
        }
        #endregion
        */
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
        
    }
}
