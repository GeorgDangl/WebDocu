using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Services
{
    public interface IProjectChangelogService
    {
        Task<string> GetChangelogInMarkdownFormat(string projectName, string version);
        Task<string> GetChangelogInHtmlFormat(string projectName, string version);
    }
}
