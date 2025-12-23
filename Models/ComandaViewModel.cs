using System.Collections.Generic;

namespace RestauranteApp.Models
{
    public class ComandaViewModel
{
    public int IdMesa { get; set; }
    public int NumeroMesa { get; set; }
    public List<Producto> Productos { get; set; } = new();
    public List<Categoria> Categorias { get; set; } = new();
    // Agregamos esto para cargar lo que ya estaba pedido:
    public List<DetalleComanda> DetalleActual { get; set; } = new(); 
    public int? IdComandaActiva { get; set; }
}
}