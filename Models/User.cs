namespace LibraryMPT.Models
{
    public class User
    {
        public int UserID { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string PasswordSalt { get; set; }
        public string FirstName { get; set; }
        public byte[] LastName { get; set; }
        public string Email { get; set; }
        public int RoleID { get; set; }
        public int? FacultyID { get; set; }
        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetTokenExpiry { get; set; }
        public bool IsTwoFactorEnabled { get; set; }
        public string? TwoFactorCode { get; set; }
        public DateTime? TwoFactorCodeExpiry { get; set; }

        public Role Role { get; set; }
        public Faculty Faculty { get; set; }
    }
}