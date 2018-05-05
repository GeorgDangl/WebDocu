namespace Dangl.WebDocumentation
{
    public class AppSettings
    {
        public string SiteTitlePrefix { get; set; }
        public string SiteTitlePostfix { get; set; }
        public bool AllowUserRegistration { get; set; }
        public string ProjectsRootFolder { get; set; }
        public EmailSettings EmailSettings { get; set; }
        public string FullTitle => SiteTitlePrefix + SiteTitlePostfix;
    }
}
