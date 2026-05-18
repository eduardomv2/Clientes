using Clientes.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clientes.Api.Data;

public class ClientesDbContext : DbContext
{
    public ClientesDbContext(DbContextOptions<ClientesDbContext> options)
        : base(options) { }

    public DbSet<CLI_Cat_Estado> Estados => Set<CLI_Cat_Estado>();
    public DbSet<CLI_Cat_Ciudad> Ciudades => Set<CLI_Cat_Ciudad>();
    public DbSet<CLI_Usuario> Usuarios => Set<CLI_Usuario>();
    public DbSet<CLI_Direccion> Direcciones => Set<CLI_Direccion>();
    public DbSet<CLI_PerfilCredito> PerfilesCredito => Set<CLI_PerfilCredito>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<CLI_Cat_Estado>(e =>
        {
            e.ToTable("CLI_Cat_Estado");
            e.HasKey(x => x.Id);
            e.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
        });

        m.Entity<CLI_Cat_Ciudad>(e =>
        {
            e.ToTable("CLI_Cat_Ciudad");
            e.HasKey(x => x.Id);
            e.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            e.HasOne(x => x.Estado)
             .WithMany()
             .HasForeignKey(x => x.IdEstado)
             .OnDelete(DeleteBehavior.Restrict);
        });

        m.Entity<CLI_Usuario>(e =>
        {
            e.ToTable("CLI_Usuario");
            e.HasKey(x => x.Id);
            e.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            e.Property(x => x.ApellidoPaterno).IsRequired().HasMaxLength(100);
            e.Property(x => x.ApellidoMaterno).HasMaxLength(100);
            e.Property(x => x.Email).IsRequired().HasMaxLength(200);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).IsRequired().HasMaxLength(200);
        });

        m.Entity<CLI_Direccion>(e =>
        {
            e.ToTable("CLI_Direccion");
            e.HasKey(x => x.Id);
            e.Property(x => x.CalleNumero).IsRequired().HasMaxLength(200);
            e.Property(x => x.Colonia).IsRequired().HasMaxLength(100);
            e.Property(x => x.CodigoPostal).IsRequired().HasMaxLength(10);
            e.HasOne(x => x.Usuario)
             .WithMany(u => u.Direcciones)
             .HasForeignKey(x => x.IdUsuario)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Ciudad)
             .WithMany()
             .HasForeignKey(x => x.IdCiudad)
             .OnDelete(DeleteBehavior.Restrict);
        });

        m.Entity<CLI_PerfilCredito>(e =>
        {
            e.ToTable("CLI_PerfilCredito");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.IdUsuario).IsUnique();
            e.Property(x => x.LimiteCredito).HasColumnType("decimal(18,2)");
            e.Property(x => x.SaldoUsado).HasColumnType("decimal(18,2)");
            e.Property(x => x.TasaInteresAnual).HasColumnType("decimal(5,4)");
            e.HasOne(x => x.Usuario)
             .WithOne(u => u.PerfilCredito)
             .HasForeignKey<CLI_PerfilCredito>(x => x.IdUsuario)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}