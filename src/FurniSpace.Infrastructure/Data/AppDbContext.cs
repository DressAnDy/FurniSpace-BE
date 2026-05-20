using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}
