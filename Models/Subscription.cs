namespace LibraryMPT.Models
{
    public class Subscription
    {
        public int SubscriptionID { get; set; }
        public int? FacultyID { get; set; }
        public string Name { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? DurationDays { get; set; }
        public string? Status { get; set; }
        public int? RequestedByUserID { get; set; }

        public bool IsActive => StartDate.HasValue && EndDate.HasValue && 
                               DateTime.UtcNow >= StartDate.Value && DateTime.UtcNow <= EndDate.Value &&
                               (Status == "Approved" || Status == null);

        public bool IsTemplate => !FacultyID.HasValue;

        public bool IsPending => Status == "Pending";

        public Faculty? Faculty { get; set; }
    }
}


