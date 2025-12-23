using Microsoft.AspNetCore.Mvc;
using System.Data;
using RestauranteApp.Models;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http; // Necesario para Session

namespace RestauranteApp.Controllers
{
    public class ProductosController : Controller
    {
        private readonly IDbConnection _db;

        public ProductosController(IDbConnection db)
        {
            _db = db;
        }

        // LISTADO DE PRODUCTOS
        public IActionResult Index()
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (rol != "CAJA") return RedirectToAction("Index", "Login");

            var lista = new List<Producto>();

            if (_db.State == ConnectionState.Closed) _db.Open();
            using (var cmd = _db.CreateCommand())
            {
                cmd.CommandText = "SELECT IdProducto, Nombre, Precio, Imagen, IdCategoria, Activo FROM producto";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lista.Add(new Producto
                        {
                            IdProducto = Convert.ToInt32(reader["IdProducto"]),
                            Nombre = reader["Nombre"]?.ToString() ?? "",
                            Precio = Convert.ToDecimal(reader["Precio"]),
                            Imagen = reader["Imagen"]?.ToString() ?? "/imagenes/productos/default.png",
                            IdCategoria = Convert.ToInt32(reader["IdCategoria"]),
                            Activo = Convert.ToInt32(reader["Activo"])
                        });
                    }
                }
            }
            _db.Close();

            return View(lista);
        }

        // VISTA PARA CREAR NUEVO
        public IActionResult Crear()
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (rol != "CAJA") return RedirectToAction("Index", "Login");

            // Pasamos un objeto vacío con valores por defecto
            return View("Formulario", new Producto { Activo = 1 });
        }

        // VISTA PARA EDITAR EXISTENTE
        public IActionResult Editar(int id)
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (rol != "CAJA") return RedirectToAction("Index", "Login");

            Producto producto = null;

            if (_db.State == ConnectionState.Closed) _db.Open();
            using (var cmd = _db.CreateCommand())
            {
                cmd.CommandText = "SELECT IdProducto, Nombre, Precio, Imagen, IdCategoria, Activo FROM producto WHERE IdProducto = @id";
                var pId = cmd.CreateParameter();
                pId.ParameterName = "@id";
                pId.Value = id;
                cmd.Parameters.Add(pId);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        producto = new Producto
                        {
                            IdProducto = Convert.ToInt32(reader["IdProducto"]),
                            Nombre = reader["Nombre"]?.ToString() ?? "",
                            Precio = Convert.ToDecimal(reader["Precio"]),
                            Imagen = reader["Imagen"]?.ToString() ?? "",
                            IdCategoria = Convert.ToInt32(reader["IdCategoria"]),
                            Activo = Convert.ToInt32(reader["Activo"])
                        };
                    }
                }
            }
            _db.Close();

            if (producto == null) return NotFound();

            return View("Formulario", producto);
        }

        // ACCIÓN PARA GUARDAR (CREAR O EDITAR)
        [HttpPost]
        public IActionResult Guardar(Producto p)
        {
            if (_db.State == ConnectionState.Closed) _db.Open();
            using (var cmd = _db.CreateCommand())
            {
                if (p.IdProducto == 0)
                {
                    // INSERTAR NUEVO
                    cmd.CommandText = "INSERT INTO producto (Nombre, Precio, Imagen, IdCategoria, Activo) VALUES (@nom, @pre, @img, @cat, @act)";
                }
                else
                {
                    // ACTUALIZAR EXISTENTE
                    cmd.CommandText = "UPDATE producto SET Nombre=@nom, Precio=@pre, Imagen=@img, IdCategoria=@cat, Activo=@act WHERE IdProducto=@id";
                    var pId = cmd.CreateParameter();
                    pId.ParameterName = "@id";
                    pId.Value = p.IdProducto;
                    cmd.Parameters.Add(pId);
                }

                // Parámetros comunes
                var pNom = cmd.CreateParameter(); pNom.ParameterName = "@nom"; pNom.Value = p.Nombre ?? "";
                var pPre = cmd.CreateParameter(); pPre.ParameterName = "@pre"; pPre.Value = p.Precio;
                var pImg = cmd.CreateParameter(); pImg.ParameterName = "@img"; pImg.Value = p.Imagen ?? "/imagenes/productos/default.png";
                var pCat = cmd.CreateParameter(); pCat.ParameterName = "@cat"; pCat.Value = p.IdCategoria;
                var pAct = cmd.CreateParameter(); pAct.ParameterName = "@act"; pAct.Value = p.Activo;

                cmd.Parameters.Add(pNom);
                cmd.Parameters.Add(pPre);
                cmd.Parameters.Add(pImg);
                cmd.Parameters.Add(pCat);
                cmd.Parameters.Add(pAct);

                cmd.ExecuteNonQuery();
            }
            _db.Close();

            return RedirectToAction("Index");
        }

        // ACCIÓN PARA ELIMINAR (O DESACTIVAR)
        public IActionResult Eliminar(int id)
        {
            if (_db.State == ConnectionState.Closed) _db.Open();
            using (var cmd = _db.CreateCommand())
            {
                // Aquí podrías elegir entre un DELETE físico o un UPDATE Activo = 0
                cmd.CommandText = "DELETE FROM producto WHERE IdProducto = @id";
                var pId = cmd.CreateParameter();
                pId.ParameterName = "@id";
                pId.Value = id;
                cmd.Parameters.Add(pId);
                cmd.ExecuteNonQuery();
            }
            _db.Close();

            return RedirectToAction("Index");
        }
    }
}