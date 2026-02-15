namespace LibraryMPT.Models
{
    public class SubscriptionRequestDto
    {
        public int SubscriptionID { get; set; }
        public int? FacultyID { get; set; }
        public string SubscriptionName { get; set; }
        public int? DurationDays { get; set; }
        public string? Status { get; set; }
        public int? RequestedByUserID { get; set; }
        public string? RequestedByUsername { get; set; }
        public string? RequestedByEmail { get; set; }
        public string? FacultyName { get; set; }
        public int StudentsCount { get; set; }
    }
}

