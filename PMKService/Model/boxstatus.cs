using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PMKService.Model
{
    public class boxstatus
    {
        public int Id { get; set; }
        public string Probe_Name { get; set; }
        public int RPI_Id { get; set; }
        public int TMS_Id { get; set; }
        public DateTime Stamp_Date { get; set; }
        public DateTime Updated { get; set; }
        public string ToString()
        {
            return String.Format("{0},{1},{2},{3},{4},{5}", Id, Probe_Name, RPI_Id, TMS_Id, Stamp_Date,Updated);
        }
    }
}
