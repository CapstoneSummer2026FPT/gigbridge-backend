using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Common.Interfaces.IRepository
{
    public interface IUserRepository : IRepository<User>
    {
        void update(User user);
    }
}
