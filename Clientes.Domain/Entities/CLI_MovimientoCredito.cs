namespace Clientes.Domain.Entities
{
    public class CLI_MovimientoCredito
    {
        public int Id { get; set; }
        public int IdUsuario { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public DateTime Fecha { get; set; } = DateTime.UtcNow;
        public int Status { get; set; } = 1;
        public CLI_Usuario Usuario { get; set; } = null!;
    }
}