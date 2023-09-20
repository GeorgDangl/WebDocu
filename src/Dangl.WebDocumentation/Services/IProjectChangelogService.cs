using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Services
{
    public interface IProjectChangelogService
    {
        Task<string> GetChangelogInMarkdownFormatAsync(string projectName, string version);
        Task<string> GetChangelogInHtmlFormatAsync(string projectName, string version);
    }
}
