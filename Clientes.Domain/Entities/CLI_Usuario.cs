using System;
using System.Collections.Generic;
using System.Text;

namespace Clientes.Domain.Entities
{
    public class CLI_Usuario
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string ApellidoPaterno { get; set; } = string.Empty;
        public string? ApellidoMaterno { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public DateOnly FechaNacimiento { get; set; }
        public DateOnly FechaRegistro { get; set; }
        public int Status { get; set; } = 1;

        public ICollection<CLI_Direccion> Direcciones { get; set; } = [];
        public CLI_PerfilCredito? PerfilCredito { get; set; }
    }
}
