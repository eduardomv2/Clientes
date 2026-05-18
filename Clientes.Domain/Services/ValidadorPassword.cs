using System;
using System.Collections.Generic;
using System.Text;

namespace Clientes.Domain.Services;

public static class ValidadorPassword
{
    private const int LongitudMinima = 8;
    private const int LongitudMaxima = 16;

    public static bool Validar(string password)
        => ObtenerErrores(password).Count == 0;

    public static IReadOnlyList<string> ObtenerErrores(string password)
    {
        var errores = new List<string>();

        if (string.IsNullOrEmpty(password))
        {
            errores.Add("El password no puede estar vacío.");
            return errores;
        }

        if (password.Contains(' '))
            errores.Add("El password no debe contener espacios.");

        if (password.Length < LongitudMinima || password.Length > LongitudMaxima)
            errores.Add($"El password debe tener entre {LongitudMinima} y {LongitudMaxima} caracteres.");

        if (!password.Any(char.IsUpper))
            errores.Add("El password debe incluir al menos una mayúscula.");

        if (!password.Any(char.IsLower))
            errores.Add("El password debe incluir al menos una minúscula.");

        if (!password.Any(char.IsDigit))
            errores.Add("El password debe incluir al menos un número.");

        if (!password.Any(c => !char.IsLetterOrDigit(c) && c != ' '))
            errores.Add("El password debe incluir al menos un carácter especial.");

        return errores;
    }
}