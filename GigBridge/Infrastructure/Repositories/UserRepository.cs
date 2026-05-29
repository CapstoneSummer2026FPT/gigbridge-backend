using Application.Common.Interfaces.IRepository;
using Domain.Entities;
using Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class UserRepository : Repository<User>, IUserRepository
    {
        private readonly GigbridgeDbContext _context;

        public UserRepository(GigbridgeDbContext context) : base(context)
        {
            _context = context;
        }

        public void update(User user)
        {
            _context.Users.Update(user);
        }
    }
}
