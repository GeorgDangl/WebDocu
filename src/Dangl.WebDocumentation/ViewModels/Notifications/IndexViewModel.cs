using Dangl.WebDocumentation.Models;
using System;
using System.Collections.Generic;

namespace Dangl.WebDocumentation.ViewModels.Notifications
{
    public class IndexViewModel
    {
        public IEnumerable<DocumentationProject> Projects { get; set; }
        public Dictionary<Guid, NotificationLevel> NotificationLevelsByProject { get; set; }
    }
}
