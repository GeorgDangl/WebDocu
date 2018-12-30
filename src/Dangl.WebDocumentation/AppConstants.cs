namespace Dangl.WebDocumentation
{
    public static class AppConstants
    {
        /// <summary>
        /// The container name under which project files are saved
        /// </summary>
        public const string PROJECTS_CONTAINER = "projects";

        /// <summary>
        /// The container name under which persistent data protection values are stored.
        /// This is required to share data protection keys between different environments, e.g.
        /// between the Production and Staging slots to allow interruption free swapping of the
        /// apps.
        /// </summary>
        public const string DATA_PROTECTION_KEYS_CONTAINER = "data-protection-container";
    }
}
