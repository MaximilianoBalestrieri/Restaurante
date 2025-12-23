namespace RestauranteApp.Models
{
    public class Producto
    {
        public int IdProducto { get; set; }
        public string Nombre { get; set; } = "";
        public decimal Precio { get; set; }
        public int IdCategoria { get; set; } 
        public string Imagen { get; set; } = ""; // Asegúrate de que no sea 'null'
        public int Activo { get; set; } // Cambiado de bool a int
    }
}