using Clientes.Api.Data;
using Clientes.Api.DTOs;
using Clientes.Domain.Entities;
using Clientes.Domain.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Servicios ─────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Clientes API",
        Version = "v1",
        Description = "Microservicio de gestión de clientes"
    });
});

builder.Services.AddDbContext<ClientesDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration
        .GetConnectionString("ClientesDb")));

var app = builder.Build();

// ── Manejo de errores global ─────────
app.UseExceptionHandler(errApp =>
{
    errApp.Run(async context =>
    {
        var error = context.Features
            .Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();

        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(new
        {
            status = 500,
            error = "Error interno del servidor.",
            detalle = error?.Error.Message,
            timestamp = DateTime.UtcNow
        });
    });
});


// ── Middleware ───
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Clientes API v1");
    c.RoutePrefix = "swagger";
});

// ── Migración automática ───
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ClientesDbContext>();
    db.Database.Migrate();
}


// ENDPOINTS


// GET /health
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "Clientes API",
    timestamp = DateTime.UtcNow
}))
.WithName("Health")
.WithTags("Health")
.WithSummary("Verifica estado del microservicio");

// GET /api/clientes/estados
app.MapGet("/api/clientes/estados", async (ClientesDbContext db) =>
    Results.Ok(await db.Estados
        .Where(e => e.Status == 1)
        .ToListAsync()))
.WithName("ObtenerEstados")
.WithTags("Catalogos")
.WithSummary("Lista todos los estados");

// GET /api/clientes/ciudades/{idEstado}
app.MapGet("/api/clientes/ciudades/{idEstado:int}", async (
    int idEstado,
    ClientesDbContext db) =>
{
    var ciudades = await db.Ciudades
        .Where(c => c.IdEstado == idEstado && c.Status == 1)
        .ToListAsync();

    return ciudades.Any()
        ? Results.Ok(ciudades)
        : Results.NotFound(new { error = "No se encontraron ciudades para ese estado." });
})
.WithName("ObtenerCiudadesPorEstado")
.WithTags("Catalogos")
.WithSummary("Lista ciudades por estado");

// POST /api/clientes/registro
app.MapPost("/api/clientes/registro", async (
    RegistroUsuarioDto dto,
    ClientesDbContext db) =>
{
    // Validar contraseña
    var errores = ValidadorPassword.ObtenerErrores(dto.Password);
    if (errores.Any())
        return Results.BadRequest(new { errores });

    // Validar mayoría de edad
    if (!ValidadorUsuario.EsMayorDeEdad(dto.FechaNacimiento))
        return Results.BadRequest(new
        { error = "El usuario debe ser mayor de edad." });

    // Validar correo único
    var existe = await db.Usuarios
        .AnyAsync(u => u.Email == dto.Email);
    if (existe)
        return Results.Conflict(new
        { error = "Ya existe una cuenta con ese correo." });

    var usuario = new CLI_Usuario
    {
        Nombre = dto.Nombre,
        ApellidoPaterno = dto.ApellidoPaterno,
        ApellidoMaterno = dto.ApellidoMaterno,
        Email = dto.Email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
        FechaNacimiento = dto.FechaNacimiento,
        FechaRegistro = DateOnly.FromDateTime(DateTime.UtcNow),
        Status = 1
    };

    db.Usuarios.Add(usuario);
    await db.SaveChangesAsync();

    return Results.Created(
        $"/api/clientes/{usuario.Id}",
        new { usuario.Id, usuario.Email, usuario.FechaRegistro });
})
.WithName("RegistrarUsuario")
.WithTags("Clientes")
.WithSummary("Registra un nuevo usuario");

// POST /api/clientes/login
app.MapPost("/api/clientes/login", async (
    LoginDto dto,
    ClientesDbContext db) =>
{
    var usuario = await db.Usuarios
        .FirstOrDefaultAsync(u => u.Email == dto.Email
                               && u.Status == 1);

    if (usuario is null ||
        !BCrypt.Net.BCrypt.Verify(dto.Password, usuario.PasswordHash))
        return Results.Unauthorized();

    return Results.Ok(new
    {
        usuario.Id,
        usuario.Nombre,
        usuario.ApellidoPaterno,
        usuario.Email,
        TieneCredito = usuario.PerfilCredito is not null
    });
})
.WithName("Login")
.WithTags("Clientes")
.WithSummary("Valida credenciales del usuario");

// GET /api/clientes/{id}
app.MapGet("/api/clientes/{id:int}", async (
    int id,
    ClientesDbContext db) =>
{
    var usuario = await db.Usuarios
        .Include(u => u.Direcciones)
        .Include(u => u.PerfilCredito)
        .FirstOrDefaultAsync(u => u.Id == id && u.Status == 1);

    return usuario is null
        ? Results.NotFound(new { error = "Usuario no encontrado." })
        : Results.Ok(new
        {
            usuario.Id,
            usuario.Nombre,
            usuario.ApellidoPaterno,
            usuario.ApellidoMaterno,
            usuario.Email,
            usuario.FechaNacimiento,
            usuario.FechaRegistro,
            TieneCredito = usuario.PerfilCredito is not null,
            Direcciones = usuario.Direcciones
                .Where(d => d.Status == 1)
                .Select(d => new
                {
                    d.Id,
                    d.CalleNumero,
                    d.Colonia,
                    d.CodigoPostal,
                    d.EsPrincipal
                })
        });
})
.WithName("ObtenerUsuario")
.WithTags("Clientes")
.WithSummary("Obtiene perfil completo del usuario");

// POST /api/clientes/{id}/direcciones
app.MapPost("/api/clientes/{id:int}/direcciones", async (
    int id,
    DireccionDto dto,
    ClientesDbContext db) =>
{
    var usuarioExiste = await db.Usuarios
        .AnyAsync(u => u.Id == id && u.Status == 1);
    if (!usuarioExiste)
        return Results.NotFound(new { error = "Usuario no encontrado." });

    // Si es principal quitar el flag a las demás
    if (dto.EsPrincipal)
    {
        var anteriores = await db.Direcciones
            .Where(d => d.IdUsuario == id && d.EsPrincipal)
            .ToListAsync();
        anteriores.ForEach(d => d.EsPrincipal = false);
    }

    var direccion = new CLI_Direccion
    {
        IdUsuario = id,
        IdCiudad = dto.IdCiudad,
        CalleNumero = dto.CalleNumero,
        Colonia = dto.Colonia,
        CodigoPostal = dto.CodigoPostal,
        EsPrincipal = dto.EsPrincipal,
        Status = 1
    };

    db.Direcciones.Add(direccion);
    await db.SaveChangesAsync();

    return Results.Created(
        $"/api/clientes/{id}/direcciones/{direccion.Id}",
        new { direccion.Id, direccion.EsPrincipal });
})
.WithName("AgregarDireccion")
.WithTags("Clientes")
.WithSummary("Agrega una dirección al usuario");

// POST /api/clientes/{id}/credito
app.MapPost("/api/clientes/{id:int}/credito", async (
    int id,
    CreditoDto dto,
    ClientesDbContext db) =>
{
    var usuario = await db.Usuarios
        .Include(u => u.PerfilCredito)
        .FirstOrDefaultAsync(u => u.Id == id && u.Status == 1);

    if (usuario is null)
        return Results.NotFound(new { error = "Usuario no encontrado." });

    if (usuario.PerfilCredito is not null)
        return Results.Conflict(new
        { error = "El usuario ya tiene un perfil de crédito." });

    if (!ValidadorUsuario.EsElegibleParaCredito(usuario.FechaRegistro))
        return Results.BadRequest(new
        { error = "El usuario necesita al menos 6 meses de antigüedad." });

    var perfil = new CLI_PerfilCredito
    {
        IdUsuario = id,
        LimiteCredito = dto.LimiteCredito,
        SaldoUsado = 0m,
        TasaInteresAnual = 0.12m,
        FechaApertura = DateOnly.FromDateTime(DateTime.UtcNow),
        Status = 1
    };

    db.PerfilesCredito.Add(perfil);
    await db.SaveChangesAsync();

    return Results.Created(
        $"/api/clientes/{id}/credito",
        new
        {
            perfil.Id,
            perfil.LimiteCredito,
            perfil.TasaInteresAnual,
            perfil.FechaApertura
        });
})
.WithName("AbrirCredito")
.WithTags("Credito")
.WithSummary("Abre línea de crédito")
.WithDescription("Requiere 6 meses de antigüedad. Tasa inicial 12%.");

app.Run();