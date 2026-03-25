using Microsoft.EntityFrameworkCore;

namespace WNCAirline.Models;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Flight> Flights => Set<Flight>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
}
