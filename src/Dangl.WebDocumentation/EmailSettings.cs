namespace Dangl.WebDocumentation
{
    public class EmailSettings
    {
        public string FromAddress { get; set; }
        public string ServerAddress { get; set; }
        public int ServerPort { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool RequiresAuthentication { get; set; }
        public bool UseTls { get; set; }
    }
}