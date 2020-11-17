using MySqlConnector;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
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
using PMKService.Model;
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
            
            //SendNotification(data);
            InsertAlertLog();
            PushNotification();

        }
        public void PushNotification()
        {
            var ConStr = ConfigurationManager.AppSettings["PMK_Connection"];
            var objConn = new MySqlConnection(ConStr);

            objConn.Open();
            string strSQL = "SELECT * FROM tbl_readerbox_status WHERE Status = 'Alert'";
            MySqlCommand cmd = new MySqlCommand(strSQL);
            MySqlDataAdapter sda = new MySqlDataAdapter();
            cmd.Connection = objConn;
            sda.SelectCommand = cmd;
            List<string> token = getToken();
            
            using (DataTable dt = new DataTable())
            {
                sda.Fill(dt);
                eventLog1.WriteEntry("readerbox count: " +dt.Rows.Count+"\n tokens count: "+token.Count, EventLogEntryType.Warning, 900);
                try { 
                    foreach(DataRow r in dt.Rows)
                    {
                        var data = new
                        {
                            registration_ids = token,
                            priority = "high",
                            notification = new
                            {
                                image = "https://i.imgur.com/ejeqK8H.png",
                                title = "PMK Alert: "+r.Field<string>("Probe_Name"),
                                text = "Temparature: " + r.Field<float>("Temperature") + " Status: Alert"
                            }
                        };
                        SendNotification(data);
                    }
                }
                catch (Exception e) { eventLog1.WriteEntry("" + e.Message, EventLogEntryType.Error, 404); }
            }
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
                eventLog1.WriteEntry(""+e.Message,EventLogEntryType.Error,404);
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
        public List<string> getToken()
        {
            var ConStr = ConfigurationManager.AppSettings["PMK_Connection"];
            var objConn = new MySqlConnection(ConStr);
            
            objConn.Open();
            string strSQL = "SELECT * FROM tbl_tokens WHERE Status = 1";
            MySqlCommand cmd = new MySqlCommand(strSQL);
            MySqlDataAdapter sda = new MySqlDataAdapter();
            cmd.Connection = objConn;
            sda.SelectCommand = cmd;
            using(DataTable dt = new DataTable())
            {
                sda.Fill(dt);
                List<string> tokens = new List<string>();
                foreach(DataRow r in dt.Rows)
                {
                    tokens.Add(r.Field<string>("AppToken"));
                }
                eventLog1.WriteEntry(String.Join("\n",tokens), EventLogEntryType.SuccessAudit,320);
                return tokens;
            }
            
        }

        public List<status> GetDataLog()
        {
            var ConStr = ConfigurationManager.AppSettings["PMK_Connection"];
            var objConn = new MySqlConnection(ConStr);
            
            objConn.Open();
            string strSQL = "select a.Id,c.Probe_Name,a.RPI_Id,a.TMS_Id,a.Temperature,a.Stamp_Date,a.Status,a.Acknowledge_By from tbl_datalog a inner join (select max(id) as maxid from tbl_datalog group by TMS_Id,RPI_Id) as b on a.id = b.maxid INNER JOIN tbl_readerbox_status as c ON a.RPI_Id = c.RPI_Id AND a.TMS_Id = c.TMS_Id Order by RPI_Id,TMS_Id";
            MySqlCommand cmd = new MySqlCommand(strSQL);
            MySqlDataAdapter sda = new MySqlDataAdapter();
            cmd.Connection = objConn;
            sda.SelectCommand = cmd;
            using (DataTable dt = new DataTable())
            {
                sda.Fill(dt);
                int i = 0;
                List<status> Status = new List<status>();
                foreach (DataRow r in dt.Rows)
                {
                    status temp = new status();
                    temp.Id = r.Field<int>("Id");
                    temp.Probe_Name = r.Field<string>("Probe_Name");
                    temp.RPI_Id = r.Field<int>("RPI_Id");
                    temp.TMS_Id = r.Field<int>("TMS_Id");
                    temp.Temperature = r.Field<float>("Temperature");
                    temp.Status = r.Field<string>("Status");
                    temp.Acknowledge_By = r.Field<string>("Acknowledge_By");
                    temp.Stamp_Date = r.Field<DateTime>("Stamp_Date");
                    Status.Add(temp);
                }
                eventLog1.WriteEntry(String.Join("\n", Status[0].Probe_Name,Status[1].Probe_Name, Status[3].Probe_Name), EventLogEntryType.SuccessAudit, 320);
                objConn.Close();
                return Status;
            }
            
        }

        public List<boxstatus> GetReaderBoxStatus(int RPI_Id,int TMS_Id)
        {
            var ConStr = ConfigurationManager.AppSettings["PMK_Connection"];
            var objConn = new MySqlConnection(ConStr);

            objConn.Open();
            string strSQL = "SELECT * FROM tbl_readerbox_status WHERE RPI_Id = '"+RPI_Id+"' AND TMS_Id = "+TMS_Id+"";
            eventLog1.WriteEntry(strSQL, EventLogEntryType.Error, 999);
            MySqlCommand cmd = new MySqlCommand(strSQL);
            MySqlDataAdapter sda = new MySqlDataAdapter();
            cmd.Connection = objConn;
            sda.SelectCommand = cmd;
            List<boxstatus> Status = new List<boxstatus>();
            using (DataTable dt = new DataTable())
            {
                sda.Fill(dt);
                int i = 0;
                
                foreach (DataRow r in dt.Rows)
                {
                    boxstatus temp = new boxstatus();
                    temp.Id = r.Field<int>("Id");
                    temp.Probe_Name = r.Field<string>("Probe_Name");
                    temp.RPI_Id = r.Field<int>("RPI_Id");
                    temp.TMS_Id = r.Field<int>("TMS_Id");
                    temp.Stamp_Date = r.Field<DateTime>("Stamp_Date");
                    temp.Updated = r.Field<DateTime>("Updated");
                    Status.Add(temp);
                }
                objConn.Close();
                
            }
            return Status;
        }
        public void InsertAlertLog()
        {
            List<status> items = GetDataLog();
            eventLog1.WriteEntry(items[0].ToString()+"\n"+items[1].ToString()+"\n"+ items[2].ToString() + "\n Count: "+items.Count, EventLogEntryType.SuccessAudit, 900);
            var ConStr = ConfigurationManager.AppSettings["PMK_Connection"];
            MySqlConnection objConn = new MySqlConnection(ConStr);
            
            
            if(items.Count > 0) {
                int i = 0;

                foreach(var result in items) {
                    List<boxstatus> box = GetReaderBoxStatus(result.RPI_Id,result.TMS_Id);
                    eventLog1.WriteEntry(""+box.ElementAt(0).ToString(), EventLogEntryType.SuccessAudit, 200);
                    objConn.Open();
                    try { 
                        if (result.Status.Equals("Alert")) {

                            var d = new DateTime();
                            d.AddDays(result.Stamp_Date.Day);
                            d.AddMonths(result.Stamp_Date.Month);
                            d.AddYears(result.Stamp_Date.Year);
                            d.AddHours(result.Stamp_Date.Hour);
                            d.AddMinutes(result.Stamp_Date.Minute);
                            d.AddSeconds(result.Stamp_Date.Second);
                            d.AddMilliseconds(result.Stamp_Date.Millisecond);
                            
                            string strSQL = "INSERT INTO tbl_alertlog(DTL_Id,Create_Date,Message,Acknowledge_By,Solved_By,Status) VALUES(" + result.Id + ",'" + d.ToString("yyyy-MM-dd HH:mm:ss") + "','หัว " + result.Probe_Name + " มีอุณหภูมิที่ " + result.Temperature + " ซึ่งมากกว่าที่ตั้งค่าไว้','','','" + result.Status + "')";
                            eventLog1.WriteEntry("Insert True: "+result.Probe_Name, EventLogEntryType.SuccessAudit, 900);
                            MySqlCommand objCmd = new MySqlCommand(strSQL, objConn);
                            MySqlDataReader objReader;
                            objReader = objCmd.ExecuteReader();
                            while (objReader.Read())
                            {
                            }
                        }
                    }catch(Exception e)
                    {
                        eventLog1.WriteEntry("ERROR: " + e.Message, EventLogEntryType.Error, 404);
                    }
                    string upSQL = "";
                    if (result.Stamp_Date.ToString("yyyy-MM-dd HH:mm:ss") == box.ElementAt(0).Stamp_Date.ToString("yyyy-MM-dd HH:mm:ss"))
                    {
                        upSQL = "UPDATE tbl_readerbox_status SET Temperature = " + result.Temperature + ",Updated = '" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "',Status = 'Notavailable',Acknowledge_By = 'null',Stamp_Date = '" + result.Stamp_Date.ToString("yyyy-MM-dd HH:mm:ss") + "' WHERE TMS_ID = " + result.TMS_Id + " AND RPI_ID = " + result.RPI_Id + "";
                    }
                    else
                    {
                        upSQL = "UPDATE tbl_readerbox_status SET Temperature = " + result.Temperature + ",Updated = '" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "',Status = '" + result.Status + "',Acknowledge_By = 'null',Stamp_Date = '" + result.Stamp_Date.ToString("yyyy-MM-dd HH:mm:ss") + "' WHERE TMS_ID = " + result.TMS_Id + " AND RPI_ID = " + result.RPI_Id + "";
                    }
                    objConn.Close();
                    eventLog1.WriteEntry(upSQL, EventLogEntryType.SuccessAudit, 707);
                    //eventLog1.WriteEntry(boxstatus.Where(x => x.RPI_Id == result.RPI_Id && x.TMS_Id == result.TMS_Id).FirstOrDefault().Stamp_Date.ToString() + " == "+result.Stamp_Date.ToString(), EventLogEntryType.SuccessAudit, 700);
                    UpdateReaderBoxStatus(upSQL, result.Probe_Name);
                }
            }
            

        }
        public void UpdateReaderBoxStatus(string strSQL,string probe_name)
        {
            try { 
                var ConStr = ConfigurationManager.AppSettings["PMK_Connection"];
                var objConn = new MySqlConnection(ConStr);
                objConn.Open();
                var objCmd = new MySqlCommand(strSQL, objConn);
                eventLog1.WriteEntry("UPDATED: "+ probe_name, EventLogEntryType.SuccessAudit, 500);
                objCmd.ExecuteNonQuery();
                objConn.Close();
            }
            catch (Exception e) { eventLog1.WriteEntry(""+e.Message, EventLogEntryType.Error, 404); }
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