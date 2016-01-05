using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.ViewModels.Admin
{
    public class ManageUsersViewModel
    {
        public IEnumerable<UserAdminRoleViewModel> Users { get; set; }
    }
}
