namespace LibraryMPT.Models
{
    public class LoginUserDto
    {
        public int UserID { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string PasswordSalt { get; set; }
        public int RoleID { get; set; }
        public string RoleName { get; set; }
        public bool IsBlocked { get; set; }
        public bool IsTwoFactorEnabled { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
    }
}
