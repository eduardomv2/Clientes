using System;
using System.Collections.Generic;
using System.Text;

namespace Clientes.Domain.Entities
{
    public class CLI_Direccion
    {
        public int Id { get; set; }
        public int IdUsuario { get; set; }
        public int IdCiudad { get; set; }
        public string CalleNumero { get; set; } = string.Empty;
        public string Colonia { get; set; } = string.Empty;
        public string CodigoPostal { get; set; } = string.Empty;
        public bool EsPrincipal { get; set; } = false;
        public int Status { get; set; } = 1;

        public CLI_Usuario Usuario { get; set; } = null!;
        public CLI_Cat_Ciudad Ciudad { get; set; } = null!;
    }
}
