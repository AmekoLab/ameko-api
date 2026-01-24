namespace FPTU.Capstone.AMKCollective.Application.DTOs.User
{
    /// <summary>
    /// DTO thông tin user cơ bản
    /// </summary>
    public class UserDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
