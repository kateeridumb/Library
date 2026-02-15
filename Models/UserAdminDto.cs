namespace LibraryMPT.Models
{
    public class UserAdminDto
    {
        public int UserID { get; set; }
        public string Email { get; set; } = null!;

        public int RoleID { get; set; }
        public string RoleName { get; set; } = null!;

        public int? FacultyID { get; set; }
        public string? FacultyName { get; set; }

        public bool IsBlocked { get; set; }
    }

}
