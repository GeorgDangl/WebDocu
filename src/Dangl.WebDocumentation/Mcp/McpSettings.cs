namespace Dangl.WebDocumentation.Mcp
{
    public class McpSettings
    {
        public bool Enabled { get; set; }
        public string Audience { get; set; }
        public string RequiredScope { get; set; } = "docs:read";
        public string[] AllowedOrigins { get; set; } = System.Array.Empty<string>();
    }
}
