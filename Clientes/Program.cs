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

    // Validar dirección
    if (dto.Direccion is null)
        return Results.BadRequest(new
        { error = "La dirección de entrega es obligatoria." });

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

    // Guardar dirección
    var direccion = new CLI_Direccion
    {
        IdUsuario = usuario.Id,
        IdCiudad = dto.Direccion.IdCiudad,
        CalleNumero = dto.Direccion.CalleNumero,
        Colonia = dto.Direccion.Colonia,
        CodigoPostal = dto.Direccion.CodigoPostal,
        EsPrincipal = true,
        Status = 1
    };

    db.Direcciones.Add(direccion);
    await db.SaveChangesAsync();


    return Results.Created(
        $"/api/clientes/{usuario.Id}",
        new { usuario.Id, usuario.Email, usuario.FechaRegistro });
})
.WithName("RegistrarUsuario")
.WithTags("Clientes")
.WithSummary("Registra un nuevo usuario");

// POST /api/clientes/{id}/credito/solicitar
app.MapPost("/api/clientes/{id:int}/credito/solicitar", async (
    int id,
    ClientesDbContext db) =>
{
    var usuario = await db.Usuarios
        .FirstOrDefaultAsync(u => u.Id == id && u.Status == 1);

    // Verificar que existe el usuario
    if (usuario is null)
        return Results.NotFound(new { error = "Usuario no encontrado." });

    // Verificar 6 meses de antigüedad
    var fechaMinima = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-6));
    if (usuario.FechaRegistro > fechaMinima)
        return Results.BadRequest(new
        {
            error = "Necesitas al menos 6 meses como cliente para solicitar crédito.",
            fechaElegible = usuario.FechaRegistro.AddMonths(6).ToString("dd/MM/yyyy")
        });

    // Verificar $2,500 en compras con débito
    if (usuario.TotalComprasDebito < 2500m)
        return Results.BadRequest(new
        {
            error = $"Necesitas al menos $2,500 en compras para solicitar crédito. Llevas: ${usuario.TotalComprasDebito:N2}"
        });

    // Verificar que no tenga ya un crédito
    var creditoExistente = await db.PerfilesCredito
        .AnyAsync(p => p.IdUsuario == id && p.Status == 1);

    if (creditoExistente)
        return Results.Conflict(new { error = "Ya tienes un crédito activo." });

    var perfil = new CLI_PerfilCredito
    {
        IdUsuario = id,
        LimiteCredito = 5000m,
        SaldoUsado = 0m,
        TasaInteresAnual = 0.12m,
        FechaApertura = DateOnly.FromDateTime(DateTime.UtcNow),
        TotalCompras = 0,
        InteresesAcumulados = 0m,
        Status = 1
    };

    db.PerfilesCredito.Add(perfil);

    var movimiento = new CLI_MovimientoCredito
    {
        IdUsuario = id,
        Tipo = "apertura",
        Monto = 5000m,
        Descripcion = "Apertura de crédito $5,000",
        Fecha = DateTime.UtcNow,
        Status = 1
    };

    db.MovimientosCredito.Add(movimiento);
    await db.SaveChangesAsync();

    return Results.Created(
        $"/api/clientes/{id}/credito",
        new
        {
            perfil.LimiteCredito,
            perfil.TasaInteresAnual,
            mensaje = "¡Crédito aprobado! Límite inicial: $5,000"
        });
})
.WithName("SolicitarCredito")
.WithTags("Credito")
.WithSummary("Solicita crédito (requiere 6 meses de antigüedad y $2,500 en compras)");

// POST /api/clientes/{id}/credito/revisar-tasa
app.MapPost("/api/clientes/{id:int}/credito/revisar-tasa", async (
    int id,
    ClientesDbContext db) =>
{
    var perfil = await db.PerfilesCredito
        .FirstOrDefaultAsync(p => p.IdUsuario == id && p.Status == 1);

    if (perfil is null)
        return Results.NotFound(new { error = "Perfil de crédito no encontrado." });

    // Verificar 12 meses con crédito
    var fechaMinima = perfil.FechaApertura.AddMonths(12);
    if (DateOnly.FromDateTime(DateTime.UtcNow) < fechaMinima)
        return Results.BadRequest(new
        {
            error = "Necesitas 12 meses con crédito para reducir la tasa.",
            fechaElegible = fechaMinima.ToString("dd/MM/yyyy")
        });

    if (perfil.TasaInteresAnual <= 0.10m)
        return Results.BadRequest(new
        { error = "Tu tasa ya está en el mínimo (10%)." });

    perfil.TasaInteresAnual = 0.10m;
    perfil.LimiteCredito = Math.Min(perfil.LimiteCredito + 5000m, 10000m);
    perfil.FechaUltimaRevision = DateOnly.FromDateTime(DateTime.UtcNow);
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        perfil.TasaInteresAnual,
        perfil.LimiteCredito,
        mensaje = "¡Felicidades! Tu tasa bajó al 10% y tu límite aumentó a $10,000"
    });
})
.WithName("RevisarTasaCredito")
.WithTags("Credito")
.WithSummary("Reduce la tasa al 10% y aumenta límite después de 12 meses");

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

// GET /api/clientes/{id}/direccion-principal
app.MapGet("/api/clientes/{id:int}/direccion-principal", async (
    int id,
    ClientesDbContext db) =>
{
    var direccion = await db.Direcciones
        .Include(d => d.Ciudad)
        .FirstOrDefaultAsync(d => d.IdUsuario == id && d.EsPrincipal && d.Status == 1);

    if (direccion is null)
        return Results.NotFound(new { error = "No se encontró dirección principal." });

    return Results.Ok(new
    {
        direccion.Id,
        direccion.CalleNumero,
        direccion.Colonia,
        direccion.CodigoPostal,
        Ciudad = direccion.Ciudad.Nombre
    });
})
.WithName("ObtenerDireccionPrincipal")
.WithTags("Clientes")
.WithSummary("Obtiene la dirección principal del usuario");

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

// GET /api/clientes/{id}/credito
app.MapGet("/api/clientes/{id:int}/credito", async (
    int id,
    ClientesDbContext db) =>
{
    var perfil = await db.PerfilesCredito
        .FirstOrDefaultAsync(p => p.IdUsuario == id && p.Status == 1);

    if (perfil is null)
        return Results.NotFound(new { error = "Perfil de crédito no encontrado." });

    return Results.Ok(new
    {
        perfil.Id,
        perfil.LimiteCredito,
        perfil.SaldoUsado,
        CreditoDisponible = perfil.CreditoDisponible,
        perfil.TasaInteresAnual,
        perfil.InteresesAcumulados,
        InteresesMensuales = perfil.InteresesMensuales,
        perfil.TotalCompras,
        perfil.FechaApertura,
        perfil.FechaUltimaRevision
    });
})
.WithName("ObtenerCredito")
.WithTags("Credito")
.WithSummary("Obtiene el perfil de crédito del usuario");


// PATCH /api/clientes/{id}/compras/registrar
app.MapMethods("/api/clientes/{id:int}/compras/registrar", ["PATCH"], async (
    int id,
    RegistrarCompraDto dto,
    ClientesDbContext db) =>
{
    var usuario = await db.Usuarios
        .FirstOrDefaultAsync(u => u.Id == id && u.Status == 1);

    if (usuario is null)
        return Results.NotFound(new { error = "Usuario no encontrado." });

    usuario.TotalComprasDebito += dto.Monto;
    await db.SaveChangesAsync();

    return Results.Ok(new { usuario.TotalComprasDebito });
})
.WithName("RegistrarCompraDebito")
.WithTags("Clientes")
.WithSummary("Registra el monto de una compra con débito");



// POST /api/clientes/{id}/credito/pago
app.MapPost("/api/clientes/{id:int}/credito/pago", async (
    int id,
    PagoCreditoDto dto,
    ClientesDbContext db) =>
{
    var perfil = await db.PerfilesCredito
        .FirstOrDefaultAsync(p => p.IdUsuario == id && p.Status == 1);



    if (perfil is null)
        return Results.NotFound(new { error = "Perfil de crédito no encontrado." });

    if (dto.Monto <= 0)
        return Results.BadRequest(new { error = "El monto debe ser mayor a cero." });

    if (dto.Monto > perfil.SaldoUsado + perfil.InteresesAcumulados)
        return Results.BadRequest(new
        { error = $"El monto excede el saldo total. Debes: ${perfil.SaldoUsado + perfil.InteresesAcumulados:N2}" });

    perfil.RealizarPago(dto.Monto);

    var movimiento = new CLI_MovimientoCredito
    {
        IdUsuario = id,
        Tipo = "pago",
        Monto = dto.Monto,
        Descripcion = $"Pago de crédito por ${dto.Monto:N2}",
        Fecha = DateTime.UtcNow,
        Status = 1
    };

    db.MovimientosCredito.Add(movimiento);
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        perfil.SaldoUsado,
        perfil.CreditoDisponible,
        mensaje = "Pago realizado correctamente."
    });
})
.WithName("PagarCredito")
.WithTags("Credito")
.WithSummary("Realiza un pago al crédito");

// GET /api/clientes/{id}/credito/movimientos
app.MapGet("/api/clientes/{id:int}/credito/movimientos", async (
    int id,
    ClientesDbContext db) =>
{
    var movimientos = await db.MovimientosCredito
        .Where(m => m.IdUsuario == id && m.Status == 1)
        .OrderByDescending(m => m.Fecha)
        .Select(m => new
        {
            m.Id,
            m.Tipo,
            m.Monto,
            m.Descripcion,
            m.Fecha
        })
        .ToListAsync();

    return Results.Ok(movimientos);
})
.WithName("ObtenerMovimientos")
.WithTags("Credito")
.WithSummary("Obtiene el historial de movimientos de crédito");

// POST /api/clientes/{id}/credito/compra
app.MapPost("/api/clientes/{id:int}/credito/compra", async (
    int id,
    CompraCredito dto,
    ClientesDbContext db) =>
{
    var perfil = await db.PerfilesCredito
        .FirstOrDefaultAsync(p => p.IdUsuario == id && p.Status == 1);

    if (perfil is null)
        return Results.NotFound(new { error = "Perfil de crédito no encontrado." });

    if (dto.Monto > perfil.CreditoDisponible)
        return Results.BadRequest(new
        {
            error = "Crédito insuficiente.",
            disponible = perfil.CreditoDisponible
        });

    perfil.RegistrarCompra(dto.Monto);

    var movimiento = new CLI_MovimientoCredito
    {
        IdUsuario = id,
        Tipo = "compra",
        Monto = dto.Monto,
        Descripcion = dto.Descripcion,
        Fecha = DateTime.UtcNow,
        Status = 1
    };

    db.MovimientosCredito.Add(movimiento);
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        perfil.SaldoUsado,
        perfil.CreditoDisponible,
        perfil.LimiteCredito,
        perfil.TotalCompras,
        mensaje = perfil.TotalCompras % 3 == 0
            ? $"¡Límite aumentado a ${perfil.LimiteCredito:N0}!"
            : "Compra registrada en crédito."
    });
})
.WithName("RegistrarCompraCredito")
.WithTags("Credito")
.WithSummary("Registra una compra en el crédito del usuario");

app.Run();

record PagoCreditoDto(decimal Monto);
record CompraCredito(decimal Monto, string Descripcion);
record RegistrarCompraDto(decimal Monto);