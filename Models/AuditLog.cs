namespace LibraryMPT.Models
{
    public class AuditLog
    {
        public int AuditLogID { get; set; }
        public string? TableName { get; set; }
        public string? ActionType { get; set; }
        public string? UserName { get; set; }
        public string? OldData { get; set; }
        public string? NewData { get; set; }
        public DateTime AuditDate { get; set; }
    }
}