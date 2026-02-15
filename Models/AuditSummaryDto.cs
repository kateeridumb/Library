namespace LibraryMPT.Models
{
    /// <summary>
    /// DTO для агрегации популярных действий в журнале аудита
    /// </summary>
    public class AuditSummaryDto
    {
        public string? TableName { get; set; }
        public string? ActionType { get; set; }
        public int EventsCount { get; set; }
    }
}


