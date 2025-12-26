using Microsoft.AspNetCore.Mvc;
using System.Data;
using RestauranteApp.Models;
using System.Collections.Generic;
using System;
using Microsoft.AspNetCore.Http;

namespace RestauranteApp.Controllers
{
    public class VentasController : Controller
    {
        private readonly IDbConnection _db;

        public VentasController(IDbConnection db)
        {
            _db = db;
        }

        // --- VISTA PARA EL CAJERO ---
        public IActionResult Index()
        {
            // Solo Caja puede entrar aquí
            if (HttpContext.Session.GetString("Rol") != "CAJA") return RedirectToAction("Index", "Login");

            var lista = new List<dynamic>();

            if (_db.State == ConnectionState.Closed) _db.Open();
            using (var cmd = _db.CreateCommand())
            {
                // Traemos las mesas ocupadas con su total actual
                cmd.CommandText = "SELECT IdComanda, IdMesa, Fecha, Total FROM comanda WHERE Estado = 'ABIERTA'";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lista.Add(new {
                            IdComanda = Convert.ToInt32(reader["IdComanda"]),
                            IdMesa = Convert.ToInt32(reader["IdMesa"]),
                            Fecha = Convert.ToDateTime(reader["Fecha"]),
                            Total = Convert.ToDecimal(reader["Total"])
                        });
                    }
                }
            }
            _db.Close();
            return View(lista);
        }

        // --- VISTA PARA EL MOZO (Tu código original mejorado) ---
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

                using (var cmd = _db.CreateCommand())
                {
                    cmd.CommandText = "SELECT IdMesa, Numero FROM Mesa WHERE IdMesa = @id";
                    var p = cmd.CreateParameter(); p.ParameterName = "@id"; p.Value = idMesa;
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

                using (var cmd = _db.CreateCommand())
                {
                    cmd.CommandText = "SELECT IdComanda FROM comanda WHERE IdMesa = @idMesa AND Estado = 'ABIERTA' LIMIT 1";
                    var p = cmd.CreateParameter(); p.ParameterName = "@idMesa"; p.Value = idMesa;
                    cmd.Parameters.Add(p);
                    idComandaActiva = (int?)cmd.ExecuteScalar();
                }

                if (idComandaActiva.HasValue)
                {
                    using (var cmd = _db.CreateCommand())
                    {
                        cmd.CommandText = @"SELECT d.IdProducto, p.Nombre, d.Cantidad, d.PrecioUnitario 
                                            FROM detalle_comanda d 
                                            JOIN producto p ON d.IdProducto = p.IdProducto 
                                            WHERE d.IdComanda = @idComanda";
                        var p = cmd.CreateParameter(); p.ParameterName = "@idComanda"; p.Value = idComandaActiva;
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

                using (var cmd = _db.CreateCommand())
                {
                    cmd.CommandText = "SELECT IdCategoria, Nombre FROM categoria WHERE Activa = 1";
                    using (var reader = cmd.ExecuteReader()) {
                        while (reader.Read()) categorias.Add(new Categoria { IdCategoria = Convert.ToInt32(reader["IdCategoria"]), Nombre = reader["Nombre"].ToString() });
                    }
                }

                using (var cmd = _db.CreateCommand())
                {
                    cmd.CommandText = "SELECT IdProducto, Nombre, Precio, Imagen, IdCategoria FROM producto WHERE Activo = 1";
                    using (var reader = cmd.ExecuteReader()) {
                        while (reader.Read()) {
                            productos.Add(new Producto {
                                IdProducto = Convert.ToInt32(reader[0]), 
                                Nombre = reader[1].ToString(), 
                                Precio = Convert.ToDecimal(reader[2]),
                                Imagen = reader.GetValue(3)?.ToString() ?? "/imagenes/productos/default.png", 
                                IdCategoria = Convert.ToInt32(reader[4])
                            });
                        }
                    }
                }
            }
            catch (Exception ex) { ViewBag.Error = ex.Message; }
            finally { _db.Close(); }

            return View(new ComandaViewModel {
                IdMesa = mesaSeleccionada?.IdMesa ?? 0,
                NumeroMesa = mesaSeleccionada?.Numero ?? 0,
                Productos = productos,
                Categorias = categorias,
                DetalleActual = detalleActual,
                IdComandaActiva = idComandaActiva
            });
        }

        [HttpPost]
        public IActionResult AgregarProductoAjax(int idMesa, int idProducto, decimal precio)
        {
            try
            {
                if (_db.State == ConnectionState.Closed) _db.Open();

                int idComanda;
                // 1. Buscar o Crear Comanda
                using (var cmd = _db.CreateCommand())
                {
                    cmd.CommandText = "SELECT IdComanda FROM comanda WHERE IdMesa = @idMesa AND Estado = 'ABIERTA' LIMIT 1";
                    var p = cmd.CreateParameter(); p.ParameterName = "@idMesa"; p.Value = idMesa;
                    cmd.Parameters.Add(p);
                    var resultado = cmd.ExecuteScalar();

                    if (resultado == null) {
                        using (var cmdInsert = _db.CreateCommand()) {
                            // Guardamos el ID del Mozo que abre la mesa
                            cmdInsert.CommandText = "INSERT INTO comanda (IdMesa, Estado, Fecha, Total) VALUES (@idMesa, 'ABIERTA', NOW(), 0); SELECT LAST_INSERT_ID();";
                            var p2 = cmdInsert.CreateParameter(); p2.ParameterName = "@idMesa"; p2.Value = idMesa;
                            cmdInsert.Parameters.Add(p2);
                            idComanda = Convert.ToInt32(cmdInsert.ExecuteScalar());
                        }
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

                // 2. Insertar o Actualizar Detalle
                using (var cmd = _db.CreateCommand())
                {
                    cmd.CommandText = "SELECT IdDetalle FROM detalle_comanda WHERE IdComanda = @idC AND IdProducto = @idP AND EstadoItem = 'PEDIDO'";
                    var p1 = cmd.CreateParameter(); p1.ParameterName = "@idC"; p1.Value = idComanda;
                    var p2 = cmd.CreateParameter(); p2.ParameterName = "@idP"; p2.Value = idProducto;
                    cmd.Parameters.Add(p1); cmd.Parameters.Add(p2);
                    var idDetalleExistente = cmd.ExecuteScalar();

                    if (idDetalleExistente != null) {
                        using (var cmdUpd = _db.CreateCommand()) {
                            cmdUpd.CommandText = "UPDATE detalle_comanda SET Cantidad = Cantidad + 1, Subtotal = Cantidad * PrecioUnitario WHERE IdDetalle = @idD";
                            var pD = cmdUpd.CreateParameter(); pD.ParameterName = "@idD"; pD.Value = idDetalleExistente;
                            cmdUpd.Parameters.Add(pD);
                            cmdUpd.ExecuteNonQuery();
                        }
                    } else {
                        using (var cmdIns = _db.CreateCommand()) {
                            cmdIns.CommandText = "INSERT INTO detalle_comanda (IdComanda, IdProducto, Cantidad, PrecioUnitario, Subtotal, EstadoItem) VALUES (@idC, @idP, 1, @pre, @pre, 'PEDIDO')";
                            var pi1 = cmdIns.CreateParameter(); pi1.ParameterName = "@idC"; pi1.Value = idComanda;
                            var pi2 = cmdIns.CreateParameter(); pi2.ParameterName = "@idP"; pi2.Value = idProducto;
                            var pi3 = cmdIns.CreateParameter(); pi3.ParameterName = "@pre"; pi3.Value = precio;
                            cmdIns.Parameters.Add(pi1); cmdIns.Parameters.Add(pi2); cmdIns.Parameters.Add(pi3);
                            cmdIns.ExecuteNonQuery();
                        }
                    }
                }

                // 3. ACTUALIZAR EL TOTAL DE LA COMANDA (Muy importante para el cajero)
                using (var cmdTotal = _db.CreateCommand()) {
                    cmdTotal.CommandText = "UPDATE comanda SET Total = (SELECT SUM(Subtotal) FROM detalle_comanda WHERE IdComanda = @idC) WHERE IdComanda = @idC";
                    var pt = cmdTotal.CreateParameter(); pt.ParameterName = "@idC"; pt.Value = idComanda;
                    cmdTotal.Parameters.Add(pt);
                    cmdTotal.ExecuteNonQuery();
                }

                return Json(new { success = true });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
            finally { _db.Close(); }
        }

        [HttpPost]
        public IActionResult CerrarMesa(int idComanda, int idMesa)
        {
            try {
                if (_db.State == ConnectionState.Closed) _db.Open();
                
                // 1. Cerrar Comanda
                using (var cmd = _db.CreateCommand()) {
                    cmd.CommandText = "UPDATE comanda SET Estado = 'CERRADA' WHERE IdComanda = @idC";
                    var p = cmd.CreateParameter(); p.ParameterName = "@idC"; p.Value = idComanda;
                    cmd.Parameters.Add(p);
                    cmd.ExecuteNonQuery();
                }

                // 2. Liberar Mesa
                using (var cmdMesa = _db.CreateCommand()) {
                    cmdMesa.CommandText = "UPDATE Mesa SET Estado = 'LIBRE' WHERE IdMesa = @idM";
                    var pM = cmdMesa.CreateParameter(); pM.ParameterName = "@idM"; pM.Value = idMesa;
                    cmdMesa.Parameters.Add(pM);
                    cmdMesa.ExecuteNonQuery();
                }
                return Json(new { success = true });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
            finally { _db.Close(); }
        }

        // --- TUS OTROS MÉTODOS (Eliminar y Despachar se mantienen igual) ---
        [HttpPost]
[HttpPost]
public IActionResult EliminarProductoAjax(int idMesa, int idProducto)
{
    try
    {
        if (_db.State == ConnectionState.Closed) _db.Open();

        // 1. Buscamos la comanda y el estado del producto
        int idComanda = 0;
        string estadoItem = "";

        using (var cmdCheck = _db.CreateCommand())
        {
            cmdCheck.CommandText = @"SELECT d.IdComanda, d.EstadoItem 
                                     FROM detalle_comanda d
                                     JOIN comanda c ON d.IdComanda = c.IdComanda
                                     WHERE c.IdMesa = @idM AND c.Estado = 'ABIERTA' 
                                     AND d.IdProducto = @idP LIMIT 1";
            
            var p1 = cmdCheck.CreateParameter(); p1.ParameterName = "@idM"; p1.Value = idMesa;
            var p2 = cmdCheck.CreateParameter(); p2.ParameterName = "@idP"; p2.Value = idProducto;
            cmdCheck.Parameters.Add(p1); cmdCheck.Parameters.Add(p2);

            using (var reader = cmdCheck.ExecuteReader())
            {
                if (reader.Read())
                {
                    idComanda = Convert.ToInt32(reader["IdComanda"]);
                    estadoItem = reader["EstadoItem"].ToString();
                }
            }
        }

        // 2. Validaciones de negocio
        if (idComanda == 0) 
            return Json(new { success = false, message = "No se encontró el producto en una comanda abierta." });

        if (estadoItem != "PEDIDO")
            return Json(new { success = false, message = $"No se puede eliminar: El producto ya está en estado '{estadoItem}'." });

        // 3. Eliminación física (solo si es 'PEDIDO')
        using (var cmdDel = _db.CreateCommand())
        {
            cmdDel.CommandText = "DELETE FROM detalle_comanda WHERE IdComanda = @idC AND IdProducto = @idP AND EstadoItem = 'PEDIDO'";
            var pc = cmdDel.CreateParameter(); pc.ParameterName = "@idC"; pc.Value = idComanda;
            var pp = cmdDel.CreateParameter(); pp.ParameterName = "@idP"; pp.Value = idProducto;
            cmdDel.Parameters.Add(pc); cmdDel.Parameters.Add(pp);
            cmdDel.ExecuteNonQuery();
        }

        // 4. Recalcular el Total de la Comanda
        using (var cmdTotal = _db.CreateCommand())
        {
            cmdTotal.CommandText = @"UPDATE comanda 
                                    SET Total = IFNULL((SELECT SUM(Subtotal) FROM detalle_comanda WHERE IdComanda = @idC), 0) 
                                    WHERE IdComanda = @idC";
            var pt = cmdTotal.CreateParameter(); pt.ParameterName = "@idC"; pt.Value = idComanda;
            cmdTotal.Parameters.Add(pt);
            cmdTotal.ExecuteNonQuery();
        }

        return Json(new { success = true });
    }
    catch (Exception ex)
    {
        return Json(new { success = false, message = ex.Message });
    }
    finally { _db.Close(); }
}
       
       [HttpPost]
public IActionResult DespacharComanda(int idMesa)
{
    try
    {
        if (_db.State == ConnectionState.Closed) _db.Open();

        // Buscamos la comanda abierta de esa mesa
        int idComanda = 0;
        using (var cmd = _db.CreateCommand()) {
            cmd.CommandText = "SELECT IdComanda FROM comanda WHERE IdMesa = @idMesa AND Estado = 'ABIERTA'";
            var p = cmd.CreateParameter(); p.ParameterName = "@idMesa"; p.Value = idMesa;
            cmd.Parameters.Add(p);
            var res = cmd.ExecuteScalar();
            if (res != null) idComanda = Convert.ToInt32(res);
        }

        if (idComanda > 0) {
            // CAMBIAMOS LOS ITEMS DE 'PEDIDO' A 'PARA_PREPARAR'
            using (var cmd = _db.CreateCommand()) {
                cmd.CommandText = "UPDATE detalle_comanda SET EstadoItem = 'PARA_PREPARAR' WHERE IdComanda = @idC AND EstadoItem = 'PEDIDO'";
                var p = cmd.CreateParameter(); p.ParameterName = "@idC"; p.Value = idComanda;
                cmd.Parameters.Add(p);
                cmd.ExecuteNonQuery();
            }
        }
        return Json(new { success = true });
    }
    catch { return Json(new { success = false }); }
    finally { _db.Close(); }
}
public IActionResult DetalleCuenta(int idMesa)
{
    var items = new List<DetalleCajaViewModel>();
    decimal total = 0;
    int idComanda = 0;

    try 
    {
        if (_db.State == ConnectionState.Closed) _db.Open();
        
        using (var cmd = _db.CreateCommand())
        {
            // Usamos los campos exactos de tu tabla detalle_comanda
            cmd.CommandText = @"SELECT c.IdComanda, p.Nombre, d.Cantidad, d.PrecioUnitario, d.Subtotal 
                                FROM comanda c 
                                INNER JOIN detalle_comanda d ON c.IdComanda = d.IdComanda
                                INNER JOIN producto p ON d.IdProducto = p.IdProducto
                                WHERE c.IdMesa = @idM AND c.Estado = 'ABIERTA'";
            
            var pMesa = cmd.CreateParameter(); 
            pMesa.ParameterName = "@idM"; 
            pMesa.Value = idMesa;
            cmd.Parameters.Add(pMesa);

            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    idComanda = Convert.ToInt32(reader["IdComanda"]);
                    decimal sub = reader["Subtotal"] != DBNull.Value ? Convert.ToDecimal(reader["Subtotal"]) : 0;
                    total += sub;
                    
                    items.Add(new DetalleCajaViewModel {
                        Nombre = reader["Nombre"].ToString(),
                        Cant = reader["Cantidad"].ToString(),
                        Precio = Convert.ToDecimal(reader["PrecioUnitario"]).ToString("N2"),
                        Sub = sub.ToString("N2")
                    });
                }
            }
        }

        // Si no hay productos, mostramos un aviso
        if (idComanda == 0) return Content("<div class='alert alert-warning'>Mesa sin consumos.</div>", "text/html");

        ViewBag.Total = total;
        ViewBag.IdMesa = idMesa;
        ViewBag.IdComanda = idComanda;

        // IMPORTANTE: Asegúrate de que el archivo se llame DetalleCuenta.cshtml
        return PartialView("DetalleCuenta", items);
    }
    catch (Exception ex) 
    {
        return Content("<div class='alert alert-danger'>Error: " + ex.Message + "</div>", "text/html");
    }
    finally { _db.Close(); }
}

    }
}