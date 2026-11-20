using System;

namespace Dangl.WebDocumentation.ViewModels.Admin
{
    public class ProjectStorageViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public long PackageSizeInBytes { get; set; }
        public long MarkdownPackageSizeInBytes { get; set; }
        public long AssetSizeInBytes { get; set; }
        public int PackagesWithoutSize { get; set; }
        public long TotalSizeInBytes => PackageSizeInBytes + MarkdownPackageSizeInBytes + AssetSizeInBytes;
    }
}
