using Microsoft.AspNetCore.Mvc;
using System.Data;
using RestauranteApp.Models;
using System.Collections.Generic;
using System;

namespace RestauranteApp.Controllers
{
    public class VentasController : Controller
    {
        private readonly IDbConnection _db;

        public VentasController(IDbConnection db)
        {
            _db = db;
        }

        public IActionResult NuevaComanda(int idMesa)
{
    var productos = new List<Producto>();
    var categorias = new List<Categoria>();
    var detalleActual = new List<DetalleComanda>();
    int? idComandaActiva = null;
    Mesa? mesaSeleccionada = null;

    try
    {
        if (_db.State == ConnectionState.Closed) _db.Open();

        // 1. Obtener Mesa
        using (var cmd = _db.CreateCommand())
        {
            cmd.CommandText = "SELECT IdMesa, Numero, Estado FROM Mesa WHERE IdMesa = @id";
            var p = cmd.CreateParameter();
            p.ParameterName = "@id"; p.Value = idMesa;
            cmd.Parameters.Add(p);
            using (var reader = cmd.ExecuteReader()) {
                if (reader.Read()) {
                    mesaSeleccionada = new Mesa {
                        IdMesa = Convert.ToInt32(reader["IdMesa"]),
                        Numero = Convert.ToInt32(reader["Numero"])
                    };
                }
            }
        }

        // 2. BUSCAR COMANDA ABIERTA PARA ESTA MESA
        using (var cmd = _db.CreateCommand())
        {
            cmd.CommandText = "SELECT IdComanda FROM comanda WHERE IdMesa = @idMesa AND Estado = 'ABIERTA' LIMIT 1";
            var p = cmd.CreateParameter();
            p.ParameterName = "@idMesa"; p.Value = idMesa;
            cmd.Parameters.Add(p);
            idComandaActiva = (int?)cmd.ExecuteScalar();
        }

        // 3. SI HAY COMANDA, CARGAR SUS DETALLES
        if (idComandaActiva.HasValue)
        {
            using (var cmd = _db.CreateCommand())
            {
                cmd.CommandText = @"SELECT d.IdProducto, p.Nombre, d.Cantidad, d.PrecioUnitario 
                                    FROM detalle_comanda d 
                                    JOIN producto p ON d.IdProducto = p.IdProducto 
                                    WHERE d.IdComanda = @idComanda";
                var p = cmd.CreateParameter();
                p.ParameterName = "@idComanda"; p.Value = idComandaActiva;
                cmd.Parameters.Add(p);
                using (var reader = cmd.ExecuteReader()) {
                    while (reader.Read()) {
                        detalleActual.Add(new DetalleComanda {
                            IdProducto = Convert.ToInt32(reader["IdProducto"]),
                            NombreProducto = reader["Nombre"].ToString(),
                            Cantidad = Convert.ToInt32(reader["Cantidad"]),
                            PrecioUnitario = Convert.ToDecimal(reader["PrecioUnitario"])
                        });
                    }
                }
            }
        }

        // 4. CARGAR CATEGORÍAS (Igual que antes)
        using (var cmd = _db.CreateCommand())
        {
            cmd.CommandText = "SELECT IdCategoria, Nombre FROM categoria WHERE Activa = 1";
            using (var reader = cmd.ExecuteReader()) {
                while (reader.Read()) categorias.Add(new Categoria { IdCategoria = Convert.ToInt32(reader["IdCategoria"]), Nombre = reader["Nombre"].ToString() });
            }
        }

        // 5. CARGAR PRODUCTOS (Igual que antes con tu lógica de posiciones)
        using (var cmd = _db.CreateCommand())
        {
            cmd.CommandText = "SELECT IdProducto, Nombre, Precio, Imagen, IdCategoria, Activo FROM producto WHERE Activo = 1";
            using (var reader = cmd.ExecuteReader()) {
                while (reader.Read()) {
                    string ruta = reader.GetValue(3)?.ToString()?.Trim() ?? "";
                    productos.Add(new Producto {
                        IdProducto = Convert.ToInt32(reader[0]), Nombre = reader[1].ToString(), Precio = Convert.ToDecimal(reader[2]),
                        Imagen = string.IsNullOrEmpty(ruta) ? "/imagenes/productos/default.png" : ruta, IdCategoria = Convert.ToInt32(reader[4])
                    });
                }
            }
        }
    }
    catch (Exception ex) { ViewBag.Error = ex.Message; }
    finally { _db.Close(); }

    var viewModel = new ComandaViewModel {
        IdMesa = mesaSeleccionada?.IdMesa ?? 0,
        NumeroMesa = mesaSeleccionada?.Numero ?? 0,
        Productos = productos,
        Categorias = categorias,
        DetalleActual = detalleActual, // Pasamos lo que ya estaba pedido
        IdComandaActiva = idComandaActiva
    };

    return View(viewModel);
}

[HttpPost]
public IActionResult AgregarProductoAjax(int idMesa, int idProducto, decimal precio)
{
    try
    {
        if (_db.State == ConnectionState.Closed) _db.Open();

        // 1. Buscar si hay comanda abierta, si no, crearla
        int idComanda;
        using (var cmd = _db.CreateCommand())
        {
            cmd.CommandText = "SELECT IdComanda FROM comanda WHERE IdMesa = @idMesa AND Estado = 'ABIERTA' LIMIT 1";
            var p = cmd.CreateParameter(); p.ParameterName = "@idMesa"; p.Value = idMesa;
            cmd.Parameters.Add(p);
            var resultado = cmd.ExecuteScalar();

            if (resultado == null) {
                // Crear nueva comanda
                using (var cmdInsert = _db.CreateCommand()) {
                    cmdInsert.CommandText = "INSERT INTO comanda (IdMesa, Estado, Fecha) VALUES (@idMesa, 'ABIERTA', NOW()); SELECT LAST_INSERT_ID();";
                    var p2 = cmdInsert.CreateParameter(); p2.ParameterName = "@idMesa"; p2.Value = idMesa;
                    cmdInsert.Parameters.Add(p2);
                    idComanda = Convert.ToInt32(cmdInsert.ExecuteScalar());
                }
                
                // OPCIONAL: Cambiar estado de la mesa a 'OCUPADA'
                using (var cmdMesa = _db.CreateCommand()) {
                    cmdMesa.CommandText = "UPDATE Mesa SET Estado = 'OCUPADA' WHERE IdMesa = @idMesa";
                    var p3 = cmdMesa.CreateParameter(); p3.ParameterName = "@idMesa"; p3.Value = idMesa;
                    cmdMesa.Parameters.Add(p3);
                    cmdMesa.ExecuteNonQuery();
                }
            } else {
                idComanda = Convert.ToInt32(resultado);
            }
        }

        // 2. Agregar o actualizar el detalle
        using (var cmd = _db.CreateCommand())
        {
            // Verificamos si el producto ya está en el detalle para sumar cantidad
            cmd.CommandText = @"INSERT INTO detalle_comanda (IdComanda, IdProducto, Cantidad, PrecioUnitario, Subtotal) 
                                VALUES (@idC, @idP, 1, @pre, @pre) 
                                ON DUPLICATE KEY UPDATE Cantidad = Cantidad + 1, Subtotal = Cantidad * PrecioUnitario";
            
            // Si tu MySQL no soporta ON DUPLICATE KEY (algunas versiones viejas), 
            // puedes hacer un UPDATE simple si ya existe o un INSERT si no.
            // Aquí lo simplificamos con un INSERT nuevo por ahora:
            cmd.CommandText = "INSERT INTO detalle_comanda (IdComanda, IdProducto, Cantidad, PrecioUnitario, Subtotal) VALUES (@idC, @idP, 1, @pre, @pre)";
            
            var p1 = cmd.CreateParameter(); p1.ParameterName = "@idC"; p1.Value = idComanda;
            var p2 = cmd.CreateParameter(); p2.ParameterName = "@idP"; p2.Value = idProducto;
            var p3 = cmd.CreateParameter(); p3.ParameterName = "@pre"; p3.Value = precio;
            cmd.Parameters.Add(p1); cmd.Parameters.Add(p2); cmd.Parameters.Add(p3);
            cmd.ExecuteNonQuery();
        }

        return Json(new { success = true });
    }
    catch (Exception ex) {
        return Json(new { success = false, message = ex.Message });
    }
    finally { _db.Close(); }
}

[HttpPost]
public IActionResult EliminarProductoAjax(int idMesa, int idProducto)
{
    try
    {
        if (_db.State == ConnectionState.Closed) _db.Open();

        using (var cmd = _db.CreateCommand())
        {
            // Buscamos la comanda abierta
            cmd.CommandText = @"DELETE FROM detalle_comanda 
                                WHERE IdComanda = (SELECT IdComanda FROM comanda WHERE IdMesa = @idMesa AND Estado = 'ABIERTA') 
                                AND IdProducto = @idProd";
            
            var p1 = cmd.CreateParameter(); p1.ParameterName = "@idMesa"; p1.Value = idMesa;
            var p2 = cmd.CreateParameter(); p2.ParameterName = "@idProd"; p2.Value = idProducto;
            cmd.Parameters.Add(p1); cmd.Parameters.Add(p2);
            
            cmd.ExecuteNonQuery();
        }
        return Json(new { success = true });
    }
    catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
    finally { _db.Close(); }
}

    }
}