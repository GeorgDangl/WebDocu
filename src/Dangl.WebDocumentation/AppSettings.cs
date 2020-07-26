namespace Dangl.WebDocumentation
{
    public class AppSettings
    {
        public string SiteTitlePrefix { get; set; }
        public string SiteTitlePostfix { get; set; }

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

        public string BaseUrl { get; set; }

        public string DanglIdentityBaseUrl { get; set; }

        public string DanglIconsBaseUrl { get; set; }

        public string DanglIdentityClientId { get; set; }

        public string DanglIdentityClientSecret { get; set; }

        public string DanglIdentityRequiredScope { get; set; }
        public string AzureBlobStorageLogConnectionString { get; set; }
    }
}
