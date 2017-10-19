using System.IO;

namespace Dangl.WebDocumentation.Dtos
{
    public class ProjectFileDto
    {
        public string MimeType { get; set; }
        public Stream FileStream { get; set; }
    }
}
