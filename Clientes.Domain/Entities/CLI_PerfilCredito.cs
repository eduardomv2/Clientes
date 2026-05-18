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
        public int Status { get; set; } = 1;

        public CLI_Usuario Usuario { get; set; } = null!;
    }
}
