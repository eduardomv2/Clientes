using System;
using System.Collections.Generic;
using System.Text;

namespace Clientes.Domain.Entities
{
    public class CLI_PerfilCredito
    {
        public int Id { get; set; }
        public int IdUsuario { get; set; }
        public decimal LimiteCredito { get; set; }
        public decimal SaldoUsado { get; set; } = 0m;
        public decimal TasaInteresAnual { get; set; } = 0.12m;
        public DateOnly FechaApertura { get; set; }
        public DateOnly? FechaUltimaRevision { get; set; }
        public int TotalCompras { get; set; } = 0;
        public decimal InteresesAcumulados { get; set; } = 0m;
        public int Status { get; set; } = 1;
        public CLI_Usuario Usuario { get; set; } = null!;

        // Propiedades calculadas
        public decimal CreditoDisponible => LimiteCredito - SaldoUsado;
        public decimal InteresesMensuales => SaldoUsado * (TasaInteresAnual / 12);

        public void RegistrarCompra(decimal monto)
        {
            SaldoUsado += monto;
            TotalCompras++;
            FechaUltimaRevision = DateOnly.FromDateTime(DateTime.UtcNow);

            // Aumentar límite cada 3 compras, máximo $20,000
            if (TotalCompras % 3 == 0 && LimiteCredito < 20000m)
            {
                LimiteCredito = Math.Min(LimiteCredito + 1000m, 20000m);
            }
        }

        public void RealizarPago(decimal monto)
        {
            SaldoUsado = Math.Max(0m, SaldoUsado - monto);
            FechaUltimaRevision = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        public void AcumularIntereses()
        {
            if (SaldoUsado > 0)
                InteresesAcumulados += InteresesMensuales;
        }
    }
}