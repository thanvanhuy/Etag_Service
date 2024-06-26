using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Timers;

namespace EtagService
{

    public partial class clsEtagService : ServiceBase
    {
        private const int servicePort = 10079;
        System.Timers.Timer timer = new System.Timers.Timer();
        clsVvaEtcApi vvaEtc = new clsVvaEtcApi();
        private object syncObject = new object();
        public clsEtagService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            //vvaEtc.PostAPILogin();
            timer.Elapsed += new ElapsedEventHandler(OnElapsedTime);
            timer.Interval = 5000;
            timer.Enabled = true;
            Thread thr = new Thread(() => KhoiTaoApi());
            thr.Priority = ThreadPriority.Highest;
            thr.IsBackground = true;
            thr.Start();
        }
        private void OnElapsedTime(object source, ElapsedEventArgs e)
        {
            try
            {
                if (vvaEtc.PostAPILogin())
                {
                    timer.Interval = 360 * 60 * 1000;
                    WriteToFile("Service is recall at " + DateTime.Now);
                }
            }
            catch (Exception)
            {
            }

        }
        protected override void OnStop()
        {
            WriteToFile("Service is stopped at " + DateTime.Now);
        }
        private void KhoiTaoApi()
        {
            try
            {
                var Winsock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                Winsock.Blocking = true;
                IPHostEntry ipHost = Dns.Resolve(Dns.GetHostName());
                IPEndPoint ipepServer = new IPEndPoint(IPAddress.Any, servicePort);
                Winsock.Bind(ipepServer);
                Winsock.Listen(10);
                while (true)
                {
                    try
                    {
                        Socket sock = Winsock.Accept();
                        Thread thr = new Thread(() => SocketConnected(sock));
                        thr.Priority = ThreadPriority.Highest;
                        thr.IsBackground = true;
                        thr.Start();
                        WriteToFile(String.Format("Client {0} connected at {1}", IPAddress.Parse(((IPEndPoint)sock.RemoteEndPoint).Address.ToString()), DateTime.Now.ToString()));

                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch (Exception)
            {
            }
        }
        public static bool IsNumeric(string value)
        {
            return value.All(char.IsNumber);
        }
        private void SocketConnected(Socket sock)
        {
            try
            {
                string ms = "";
                int count;
                string bienSoIn = "";
                string vehicleType = "";
                while (sock.Connected)
                {
                    sock.ReceiveTimeout = 5000;
                    sock.SendTimeout  = 500;
                    byte[] DataReceive = new byte[1025];
                    count = 0;
                    count = sock.Receive(DataReceive, DataReceive.Length, SocketFlags.None);
                    ms = System.Text.Encoding.ASCII.GetString(DataReceive, 0, DataReceive.Length);
                    string Str = ms.Substring(0, count);
                    bienSoIn = Str;
                    DateTime start = DateTime.Now;
                    string etag = "A";
                    string bienSo = "A";
                    string info = "";
                    if (bienSoIn.Length > 6)
                    {
                        if (bienSo.Length >20) // check vằng ETAG 
                        {                        
                            if (vvaEtc.PostAPI(bienSoIn, "", ref etag, ref bienSo, ref info))
                            {
                            }
                            else
                            {
                                etag = "A";
                                bienSo = "A";
                            }
                        }
                        else // check bằng biển số
                        {
                            if (IsNumeric(bienSoIn[bienSoIn.Length - 1].ToString()))
                            {
                                vehicleType = "";
                            }
                            else
                            {
                                vehicleType = bienSoIn[bienSoIn.Length - 1].ToString();
                                bienSoIn = bienSoIn.Substring(0, bienSoIn.Length - 1);
                            }
                            if (vvaEtc.PostAPI(bienSoIn, vehicleType, ref etag, ref bienSo, ref info))
                            {
                            }
                            else
                            {
                                etag = "A";
                                bienSo = "A";
                            }
                        }
                       
                    }
                    else
                    {
                        etag = "A";
                        bienSo = "A";
                    }
                    byte[] Data = System.Text.Encoding.ASCII.GetBytes(etag + "#" + bienSo + "#" + info );
                    sock.Send(Data, Data.Length, SocketFlags.None);
                    if (bienSo .Length >6 )
                    {
                        WriteToFile(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff") + "-" + IPAddress.Parse(((IPEndPoint)sock.RemoteEndPoint).Address.ToString())  +  ": " + bienSoIn + vehicleType + "#" + etag + "#" + info );
                    }
                }
            }
            catch (Exception)
            {
                WriteToFile(String.Format("Client {0} disconnected at {1}", IPAddress.Parse(((IPEndPoint)sock.RemoteEndPoint).Address.ToString()), DateTime.Now.ToString()));
                try
                {
                    sock.Disconnect (false );
                    sock.Close();
                }
                catch (Exception)
                {
                }
            }
        }

        public void WriteToFile(string Message)
        {
            new Thread(() =>
            {
                lock (syncObject)
                {
                    try
                    {
                        string path = AppDomain.CurrentDomain.BaseDirectory + "\\Logs";
                        if (!Directory.Exists(path))
                        {
                            Directory.CreateDirectory(path);
                        }
                        string filepath = AppDomain.CurrentDomain.BaseDirectory + "\\Logs\\ServiceLog_" + DateTime.Now.ToString("yyMMdd") + ".txt";
                        if (!File.Exists(filepath))
                        {
                            using (StreamWriter sw = File.CreateText(filepath))
                            {
                                sw.WriteLine(Message);
                            }
                        }
                        else
                        {
                            using (StreamWriter sw = File.AppendText(filepath))
                            {
                                sw.WriteLine(Message);
                            }
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }).Start();

        }
    }
}
