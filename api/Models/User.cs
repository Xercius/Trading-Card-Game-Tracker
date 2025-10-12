namespace api.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = null!;
        public string DisplayName { get; set; } = null!;
        public string? PasswordHash { get; set; }
        public bool IsAdmin { get; set; } = false;
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public ICollection<UserCard> UserCards { get; set; } = new List<UserCard>();
    }
}
