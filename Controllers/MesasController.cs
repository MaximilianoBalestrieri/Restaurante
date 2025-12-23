using Microsoft.AspNetCore.Mvc;
using System.Data;
using RestauranteApp.Models;
using System.Collections.Generic;
using System;

namespace RestauranteApp.Controllers
{
    public class MesasController : Controller
    {
        private readonly IDbConnection _db;

        public MesasController(IDbConnection db)
        {
            _db = db;
        }

        public IActionResult Index()
        {
            var mesas = new List<Mesa>();
            _db.Open();
            using (var cmd = _db.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM Mesa ORDER BY Numero";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        mesas.Add(new Mesa {
                            IdMesa = Convert.ToInt32(reader["IdMesa"]),
                            Numero = Convert.ToInt32(reader["Numero"]),
                            Estado = reader["Estado"].ToString() ?? ""
                        });
                    }
                }
            }
            _db.Close();
            return View(mesas);
        }

        public IActionResult NuevaComanda(int idMesa)
        {
            var productos = new List<Producto>();
            Mesa? mesaSeleccionada = null; // Cambiado a Mesa?

            _db.Open();
            using (var cmdMesa = _db.CreateCommand())
            {
                cmdMesa.CommandText = "SELECT * FROM Mesa WHERE IdMesa = @id";
                var param = cmdMesa.CreateParameter();
                param.ParameterName = "@id";
                param.Value = idMesa;
                cmdMesa.Parameters.Add(param);

                using (var reader = cmdMesa.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        mesaSeleccionada = new Mesa {
                            IdMesa = Convert.ToInt32(reader["IdMesa"]),
                            Numero = Convert.ToInt32(reader["Numero"]),
                            Estado = reader["Estado"].ToString() ?? ""
                        };
                    }
                }
            }

            using (var cmdProd = _db.CreateCommand())
            {
                cmdProd.CommandText = "SELECT * FROM producto"; 
                using (var reader = cmdProd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        productos.Add(new Producto {
                            IdProducto = Convert.ToInt32(reader["IdProducto"]),
                            Nombre = reader["Nombre"].ToString() ?? "",
                            Precio = Convert.ToDecimal(reader["Precio"])
                        });
                    }
                }
            }
            _db.Close();

            if (mesaSeleccionada == null) return NotFound();

            var viewModel = new ComandaViewModel {
                IdMesa = mesaSeleccionada.IdMesa,
                NumeroMesa = mesaSeleccionada.Numero,
                Productos = productos
            };

            return View(viewModel);
        }
    }
}