using System;
using System.Collections.Generic;
using System.Text;

namespace Clientes.Domain.Services
{
    public static class ValidadorUsuario
    {
        // Valida que el usuario sea mayor de edad
        public static bool EsMayorDeEdad(DateOnly fechaNacimiento)
        {
            var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
            var edad = hoy.Year - fechaNacimiento.Year;
            if (fechaNacimiento > hoy.AddYears(-edad)) edad--;
            return edad >= 18;
        }

        // Valida que el usuario tenga al menos 6 meses de antigüedad para crédito
        public static bool EsElegibleParaCredito(DateOnly fechaRegistro)
        {
            var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
            var meses = ((hoy.Year - fechaRegistro.Year) * 12)
                      + (hoy.Month - fechaRegistro.Month);
            return meses >= 6;
        }

        // Valida si aplica reducción de tasa tras 12 meses de buen historial
        public static bool AplicaReduccionTasa(DateOnly fechaApertura)
        {
            var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
            var meses = ((hoy.Year - fechaApertura.Year) * 12)
                      + (hoy.Month - fechaApertura.Month);
            return meses >= 12;
        }
    }
}
