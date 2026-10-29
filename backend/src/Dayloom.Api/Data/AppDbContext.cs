using Microsoft.EntityFrameworkCore;

namespace Dayloom.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}
