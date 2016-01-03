using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.ViewModels.Admin
{
    public class ManageUsersViewModel
    {
        public IEnumerable<UserAdminRole> Users { get; set; }
    }


    public class UserAdminRole
    {
        public string Name { get; set; }

        public bool IsAdmin { get; set; }
    }

}
