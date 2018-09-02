namespace Dangl.WebDocumentation
{
    public class AppSettings
    {
        public string SiteTitlePrefix { get; set; }
        public string SiteTitlePostfix { get; set; }
        public bool AllowUserRegistration { get; set; }

        /// <summary>
        /// Use this to configure physical disk storage
        /// </summary>
        public string ProjectsRootFolder { get; set; }

        /// <summary>
        /// Use this to configure Azure blob storage
        /// </summary>
        public string AzureBlobConnectionString { get; set; }

        public EmailSettings EmailSettings { get; set; }
        public string FullTitle => SiteTitlePrefix + SiteTitlePostfix;
    }
}
