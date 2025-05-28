using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace WindowsFormsApp2
{
    public partial class SERVER : Form
    {
        TcpListener servidor;
        Dictionary<TcpClient, (string nombre, string ip)> clientes = new Dictionary<TcpClient, (string, string)>();
        object lockClientes = new object();

        public SERVER()
        {
            InitializeComponent();
            StartServer();
        }

        void StartServer()
        {
            Thread hiloServidor = new Thread(() =>
            {
                try
                {
                    servidor = new TcpListener(IPAddress.Any, 5000);
                    servidor.Start();
                    AgregarLog("Servidor iniciado en puerto 5000...");

                    while (true)
                    {
                        TcpClient cliente = servidor.AcceptTcpClient();
                        Thread hiloCliente = new Thread(() => EscucharCliente(cliente));
                        hiloCliente.IsBackground = true;
                        hiloCliente.Start();
                    }
                }
                catch (Exception ex)
                {
                    AgregarLog("Error en el servidor: " + ex.Message);
                }
            });

            hiloServidor.IsBackground = true;
            hiloServidor.Start();
        }

        void EscucharCliente(TcpClient cliente)
        {
            string nombreUsuario = "";
            try
            {
                var endpoint = cliente.Client.RemoteEndPoint as IPEndPoint;
                string ipCliente = endpoint.Address.ToString();

                using (NetworkStream stream = cliente.GetStream())
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
                {
                    // Primer mensaje = nombre de usuario
                    nombreUsuario = reader.ReadLine()?.Trim();
                    if (string.IsNullOrEmpty(nombreUsuario)) return;

                    lock (lockClientes)
                    {
                        clientes[cliente] = (nombreUsuario, ipCliente);
                    }

                    AgregarLog($"Cliente conectado: {nombreUsuario} ({ipCliente})");
                    EnviarListaUsuarios();

                    string mensaje;
                    while ((mensaje = reader.ReadLine()) != null)
                    {
                        // El mensaje debe tener el formato: /para:DESTINATARIO mensaje
                        if (mensaje.StartsWith("/para:"))
                        {
                            int idx = mensaje.IndexOf(' ');
                            if (idx > 6)
                            {
                                string destinatario = mensaje.Substring(6, idx - 6).Trim();
                                string mensajeReal = mensaje.Substring(idx + 1);
                                EnviarMensajePrivado(nombreUsuario, destinatario, mensajeReal);
                            }
                        }
                    }
                }
            }
            catch { }
            finally
            {
                lock (lockClientes)
                {
                    if (clientes.ContainsKey(cliente))
                    {
                        AgregarLog($"Cliente desconectado: {clientes[cliente].nombre}");
                        clientes.Remove(cliente);
                    }
                }

                cliente.Close();
                EnviarListaUsuarios();
            }
        }

        void EnviarMensajePrivado(string remitente, string destinatario, string mensaje)
        {
            TcpClient clienteDestino = null;
            lock (lockClientes)
            {
                foreach (var kv in clientes)
                {
                    if (kv.Value.nombre.Equals(destinatario, StringComparison.OrdinalIgnoreCase))
                    {
                        clienteDestino = kv.Key;
                        break;
                    }
                }
            }

            if (clienteDestino != null)
            {
                try
                {
                    StreamWriter writer = new StreamWriter(clienteDestino.GetStream(), Encoding.UTF8) { AutoFlush = true };
                    writer.WriteLine($"[Privado de {remitente}]: {mensaje}");
                }
                catch { }
            }
        }

        void EnviarListaUsuarios()
        {
            lock (lockClientes)
            {
                StringBuilder lista = new StringBuilder();
                lista.Append("#usuarios:");

                foreach (var cliente in clientes)
                {
                    string nombre = cliente.Value.nombre;
                    string ip = cliente.Value.ip;
                    lista.Append($"{nombre}|{ip},");
                }

                if (lista.Length > 10)
                    lista.Length--;

                foreach (var cliente in clientes.Keys)
                {
                    try
                    {
                        StreamWriter writer = new StreamWriter(cliente.GetStream(), Encoding.UTF8) { AutoFlush = true };
                        writer.WriteLine(lista.ToString());
                    }
                    catch { }
                }
            }
        }

        void AgregarLog(string texto)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => AgregarLog(texto)));
            }
            else
            {
                richTextBox1.AppendText(texto + Environment.NewLine);
                richTextBox1.SelectionStart = richTextBox1.Text.Length;
                richTextBox1.ScrollToCaret();
            }
        }
    }
}