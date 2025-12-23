namespace RestauranteApp.Models
{
    public class Mesa
    {
        public int IdMesa { get; set; }
        public int Numero { get; set; }
        public string Estado { get; set; } = "LIBRE";
    }
}
