namespace Sentana.API.Constants
{
    public class SignalREvents
    {
        public const string MAINTENANCE_REQUEST = "ReceiveNewMaintenanceRequest"; // Gửi cho Manager
        public const string MAINTENANCE_ASSIGNEDTASK = "ReceiveAssignedTask";        // Gửi cho Technician
        public const string MAINTENANCE_TASKPROCESSING = "TaskProcessing";           // Gửi cho Manager & Resident
        public const string MAINTENANCE_TASKFIXED = "ReceiveFixedTask";              // Gửi cho Manager & Resident
        public const string MAINTENANCE_TASKCLOSED = "TaskClosed"; // Gửi cho Resident
        public const string MAINTENANCE_TASKREJECTED = "TaskRejectedByManager";
    }
}
