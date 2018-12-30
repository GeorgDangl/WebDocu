namespace Dangl.WebDocumentation.ViewModels.Status
{
    /// <summary>
    /// Indicates the status of the Dangl.Docu service
    /// </summary>
    public class StatusGet
    {
        /// <summary>
        /// If any problems in the service health are known, this is set to false
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// The current version of the Dangl.Docu service
        /// </summary>
        public string Version { get; set; }
    }
}
