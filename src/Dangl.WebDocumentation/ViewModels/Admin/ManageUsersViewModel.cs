using System.Collections.Generic;

namespace Dangl.WebDocumentation.ViewModels.Admin
{
    public class ManageUsersViewModel
    {
        public IEnumerable<UserAdminRoleViewModel> Users { get; set; }
    }
}
