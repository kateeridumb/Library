namespace LibraryMPT.Models
{
    public class StaffCreateDto
    {
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Username { get; set; } = null!;
        public int RoleID { get; set; }
        public int? FacultyID { get; set; }
    }

}
