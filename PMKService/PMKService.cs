using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Web.Script.Serialization;

namespace PMKService
{
    public partial class PMKService : ServiceBase
    {
        private int eventId = 1;
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(System.IntPtr handle, ref ServiceStatus serviceStatus);
        public PMKService()
        {
            InitializeComponent();
            eventLog1 = new System.Diagnostics.EventLog();
            if (!System.Diagnostics.EventLog.SourceExists("MySource"))
            {
                System.Diagnostics.EventLog.CreateEventSource(
                    "MySource", "MyNewLog");
            }
            eventLog1.Source = "MySource";
            eventLog1.Log = "MyNewLog";
        }

        protected override void OnStart(string[] args)
        {
            eventLog1.WriteEntry("In OnStart.");
            ServiceStatus serviceStatus = new ServiceStatus();
            serviceStatus.dwCurrentState = ServiceState.SERVICE_START_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);
            Timer timer = new Timer();
            timer.Interval = 60000; // 60 seconds
            timer.Elapsed += new ElapsedEventHandler(this.OnTimer);
            timer.Start();
            // Update the service state to Running.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_RUNNING;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);
        }
        public void OnTimer(object sender, ElapsedEventArgs args)
        {
            // TODO: Insert monitoring activities here.
            eventLog1.WriteEntry("Monitoring the System", EventLogEntryType.Information, eventId++);
            List<string> token = new List<string> { "dFrsz63-S5ewIKh-eHZREw:APA91bET8Ti-fqF2vvdoyfWBQF654v4cHZfgmPNDBU03VjyUjoySBoUt2KYL2LRDEx5H2nF5DPdNTygwno8JUuP3DnzVf8nD5AroZxuPAapu7ciuiP_ncIA2QXgr-S2rQj62dtoK8Hoj", "dFrsz63-S5ewIKh-eHZREw:APA91bET8Ti-fqF2vvdoyfWBQF654v4cHZfgmPNDBU03VjyUjoySBoUt2KYL2LRDEx5H2nF5DPdNTygwno8JUuP3DnzVf8nD5AroZxuPAapu7ciuiP_ncIA2QXgr-S2rQj62dtoK8Hoj" };
            var data = new
            {
                registration_ids = token,
                notification = new
                {
                    title = "Notification Title",
                    text = "The Text of the notification."
                }
            };
            SendNotification(data);
        }
        public void SendNotification(object data)
        {
            var serializer = new JavaScriptSerializer();
            var json = serializer.Serialize(data);
            Byte[] byteArray = Encoding.UTF8.GetBytes(json);
            SendNotification(byteArray);
        }
        public void SendNotification(Byte[] byteArray)
        {
            try {
                string server_api_key = ConfigurationManager.AppSettings["server_api"];
                string sender_id = ConfigurationManager.AppSettings["sender_key"];
                WebRequest tRequest = WebRequest.Create("https://fcm.googleapis.com/fcm/send");
                tRequest.Method = "post";
                tRequest.ContentType = "application/json";
                tRequest.Headers.Add($"Authorization: key={server_api_key}");
                tRequest.Headers.Add($"Sender: id={sender_id}");
                tRequest.ContentLength = byteArray.Length;
                Stream dataStream = tRequest.GetRequestStream();
                dataStream.Write(byteArray, 0, byteArray.Length);
                dataStream.Close();

                WebResponse response = tRequest.GetResponse();
                dataStream = response.GetResponseStream();
                StreamReader sReader = new StreamReader(dataStream);

                string sResponse = sReader.ReadToEnd();
                sReader.Close();
                dataStream.Close();
                eventLog1.WriteEntry("" + sResponse, EventLogEntryType.Information, eventId++);
            }
            catch(Exception e)
            {
                eventLog1.WriteEntry(""+e.ToString());
            }
        }
        protected override void OnStop()
        {
            eventLog1.WriteEntry("In OnStop.");
            // Update the service state to Stop Pending.
            ServiceStatus serviceStatus = new ServiceStatus();
            serviceStatus.dwCurrentState = ServiceState.SERVICE_STOP_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            // Update the service state to Stopped.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_STOPPED;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);
        }

    }
}
public enum ServiceState
{
    SERVICE_STOPPED = 0x00000001,
    SERVICE_START_PENDING = 0x00000002,
    SERVICE_STOP_PENDING = 0x00000003,
    SERVICE_RUNNING = 0x00000004,
    SERVICE_CONTINUE_PENDING = 0x00000005,
    SERVICE_PAUSE_PENDING = 0x00000006,
    SERVICE_PAUSED = 0x00000007,
}

[StructLayout(LayoutKind.Sequential)]
public struct ServiceStatus
{
    public int dwServiceType;
    public ServiceState dwCurrentState;
    public int dwControlsAccepted;
    public int dwWin32ExitCode;
    public int dwServiceSpecificExitCode;
    public int dwCheckPoint;
    public int dwWaitHint;
};