using System;
using System.Collections.Generic;
using System.Text;

namespace Clientes.Domain.Entities
{
    public class CLI_Cat_Ciudad
    {
        public int Id { get; set; }
        public int IdEstado { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int Status { get; set; } = 1;

        public CLI_Cat_Estado Estado { get; set; } = null!;
    }
}
