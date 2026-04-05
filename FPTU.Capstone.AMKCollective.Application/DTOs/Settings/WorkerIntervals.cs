using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Settings
{
    public class WorkerIntervals
    {
        public int AbandonedOrderCleanupMinutes { get; set; }
        public int FundsReleaseHours { get; set; }
        public int OrderCancellationTimeoutMinutes { get; set; }
        public int CommissionReminderMinutes { get; set; }
        public int AssemblyTrackingTimeoutMinutes { get; set; }
        public int UnverifiedAccountCleanupMinutes { get; set; } = 60;
    }
}
