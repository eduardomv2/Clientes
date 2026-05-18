using System;
using System.Collections.Generic;
using System.Text;

namespace Clientes.Domain.Entities
{
    public class CLI_Cat_Estado
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int Status { get; set; } = 1;
    }
}
