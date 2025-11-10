using Common.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Common.Storage;

public sealed class TwinDbContext : DbContext
{
    private readonly IOptions<PostgresOptions> _opts;
    
    public TwinDbContext(DbContextOptions<TwinDbContext> options, IOptions<PostgresOptions> opts) : base(options) =>  _opts = opts;
    
    public DbSet<PrinterEntity> Printers => Set<PrinterEntity>();
    public DbSet<PrinterEventEntity> PrinterEvents => Set<PrinterEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PrinterEntity>()
            .Property(p => p.DevId).HasColumnName("dev_id");
        modelBuilder.Entity<PrinterEntity>()
            .Property(p => p.LastSeen).HasColumnName("last_seen");

        modelBuilder.Entity<PrinterEventEntity>()
            .Property(p => p.DevId).HasColumnName("dev_id");
        modelBuilder.Entity<PrinterEventEntity>()
            .Property(p => p.PayloadJson).HasColumnName("payload");
        modelBuilder.Entity<PrinterEventEntity>()
            .Property(p => p.Ts).HasColumnName("ts");

        modelBuilder.Entity<PrinterEventEntity>()
            .HasIndex(e => new { e.DevId, e.Ts });
    }
}