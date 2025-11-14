using System.Collections.Generic;
using ST10442835_CLDV6212_POE.Models;
using Microsoft.EntityFrameworkCore;

namespace ST10442835_CLDV6212_POE.Data
{
    public class AuthDbContext : DbContext
    {
        public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Cart> Cart => Set<Cart>();
        public DbSet<Order> Orders => Set<Order>();
    }
}
