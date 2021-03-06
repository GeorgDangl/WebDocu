﻿using System;
using System.Collections.Generic;

namespace Dangl.WebDocumentation.ViewModels.ProjectVersions
{
    public class IndexViewModel
    {
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string PathToIndex { get; set; }
        public IList<ProjectVersionViewModel> Versions { get; set; }
    }
}
