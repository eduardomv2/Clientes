namespace Clientes.Api.DTOs;

public record RegistroUsuarioDto(
    string Nombre,
    string ApellidoPaterno,
    string? ApellidoMaterno,
    string Email,
    string Password,
    DateOnly FechaNacimiento
);

public record LoginDto(
    string Email,
    string Password
);

public record DireccionDto(
    int IdCiudad,
    string CalleNumero,
    string Colonia,
    string CodigoPostal,
    bool EsPrincipal
);

public record CreditoDto(
    decimal LimiteCredito
);