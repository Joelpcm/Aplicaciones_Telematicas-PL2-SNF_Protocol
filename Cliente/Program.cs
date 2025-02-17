using System;
using System.ComponentModel.Design;
using System.Net;
using System.Net.Sockets;
using Microsoft.VisualBasic;
using SNF_definition;
using System.IO;

namespace Cliente
{
    public class Program
    {
        public static void Main()
        {
            var cliente = new Program();
            cliente.Run(1000); // puerto al que enviaremos el mensaje
        }

        public void Run(int serverPort)
        {
            IPAddress server = IPAddress.Parse("127.0.0.1"); // direccion ip a la que enviaremos el mensaje (ip local)
            IPEndPoint endPoint = new IPEndPoint(server, serverPort);

            UdpClient client = new UdpClient();             // creamos el cliente UDP
            const int RECV_TIMEOUT = 5000;                  // establecemos el temporizador en 5000 ms
            client.Client.ReceiveTimeout = RECV_TIMEOUT;    // cualquier llamada a client.Receive() espera como máximo RECV_TIMEOUT
            BinaryCodec codec = new BinaryCodec();          // creamos una instancia del codec
            sbyte sequence = 0;                             // el primer número de secuencia es 0
            const int LOST_PROB = 20;                       // establecemos la probabilidad de pérdida en 20 %
            Random random = new Random();                   // número random para la probabilidad de pérdida

            try
            {
                bool firstACK = false; // booleano para controlar si se ha recibido el primer ACK
                while (!firstACK) // hasta que no se reciba el primer ACK, no se puede enviar información
                {
                    // recibir primer ACK
                    try
                    {
                        // Enviar mensaje de inicio
                        SNF_message startMessage = new SNF_message(sequence);
                        SendMessage(client, startMessage, codec, endPoint);

                        // Recibir mensaje
                        SNF_message rcvMessage = ReceiveMessage(client, codec, endPoint); // llamada a método para recibir el mensaje, se inicia el temporizador
                        firstACK = true; // si se recibe el ACK hemos iniciado una comunicacion
                        sequence++; // si se ha recibido el ACK con éxito, aumentamos la secuencia en 1 para el siguiente envio
                        Console.WriteLine("Connection established. You can start sending numbers to the server.");
                    }
                    catch (SocketException se)
                    {
                        if (se.SocketErrorCode == SocketError.TimedOut) // ha habido un timeout
                            Console.WriteLine("ACK not received. Resending message..."); // nos quedamos en el bucle para reenviar el mensaje
                        else // tratar otro tipo de error
                        {
                            Console.WriteLine(se.ErrorCode + ": " + se.Message);
                        }
                    }
                }

                //Para comprobar si el usuario quiere introducir por consola o leer archivos
                Console.WriteLine("Do you want to send numbers from a file? (yes/no)");
                string sendFile = Console.ReadLine().ToLower();

                while (sendFile != "yes" && sendFile != "no") // El usuario ha introducido un valor no valido y se le vuelve a preguntar
                {
                    Console.WriteLine("Please, introduce a valid option (yes/no)");
                    sendFile = Console.ReadLine().ToLower();
                }

                // Queremos leer fichero
                if (sendFile == "yes")
                {
                    // Pedimos al usuario que indique el fichero a leer
                    string filePath = null;
                    while (filePath == null || !File.Exists(filePath))
                    {
                        Console.WriteLine("Enter the file path:");
                        filePath = Console.ReadLine();
                    }

                    string[] lines = File.ReadAllLines(filePath);
                    foreach (string line in lines)
                    {
                        if (int.TryParse(line, out int num))
                        {
                            sbyte data = (sbyte)num; // convertimos el entero a byte
                            SNF_message message = new SNF_message(sequence, data); // creamos el mensaje SNF

                            try
                            {
                                bool ACKreceived = false; // establecemos el booleano de ACK en falso para proceder al reenvío en caso de pérdida
                                while (!ACKreceived) // hasta que no se reciba el ACK, se reenvía el mensaje
                                {
                                    if (random.Next(0, 100) > LOST_PROB)  // caso en el que se envia el paquete con exito (80% de las veces)
                                        SendMessage(client, message, codec, endPoint); // llamada a método para enviar el mensaje, se inicia el temporizador
                                    else // caso en el que se pierde el paquete (20% de las veces)
                                        Console.WriteLine("Number " + message.number + " sent with sequence number " + message.sequence + ", but was lost (simulation of losses)");

                                    try
                                    {
                                        // recepción del ACK
                                        SNF_message rcvMessage = ReceiveMessage(client, codec, endPoint); // llamada a método para recibir el mensaje, se inicia el temporizador
                                        ACKreceived = true; // si se recibe el ACK antes de que salte el timeout, establecemos ek ACK en true para salir del bucle y pasar al envío del siguiente mensaje

                                        sequence++; // si se ha recibido el ACK con éxito, aumentamos la secuencia en 1 para el siguiente envio
                                        if (sequence == 127) // si el numero de secuencia llega a 127, se reinicia a 0
                                            sequence = 0;
                                    }
                                    catch (SocketException se)
                                    {
                                        if (se.SocketErrorCode == SocketError.TimedOut) // ha habido un timeout
                                            Console.WriteLine("ACK not received. Resending message..."); // nos quedamos en el bucle para reenviar el mensaje
                                        else // tratar otro tipo de error
                                            Console.WriteLine(se.ErrorCode + ": " + se.Message);
                                    }
                                }
                            }
                            catch (SocketException se)
                            {
                                Console.WriteLine(se.ErrorCode + ": " + se.Message);
                            }
                        }
                    }
                    // Se ha llegado al final del fichero, se procede a cerrar la conexion
                    Console.WriteLine("End of File. Closing connection...");

                    // enviamos mensaje de cierre
                    sequence = -1; // establecemos la secuencia en -1 para indicar que se cierra la conexión
                    SNF_message closingMessage = new SNF_message(sequence);
                    try
                    {
                        bool ACKreceived = false; // establecemos el booleano de ACK en falso para proceder al reenvío en caso de pérdida
                        while (!ACKreceived)
                        {
                            if (random.Next(0, 100) > LOST_PROB) // caso en el que se envia el paquete con exito (80% de las veces)
                                SendMessage(client, closingMessage, codec, endPoint); // llamada a método para enviar el mensaje
                            else // caso en el que se pierde el paquete (20% de las veces)
                                Console.WriteLine("Message with sequence number " + closingMessage.sequence + " sent, but was lost (simulation of losses)");

                            try
                            {
                                SNF_message rcvMessage = ReceiveMessage(client, codec, endPoint); // llamada a método para recibir el mensaje, inicio de temporizador
                                ACKreceived = true; // si se recibe el ACK antes de que salte el timeout, establecemos el ACK en true para salir del bucle y cerrar la conexion

                                Console.WriteLine("Connection closed successfully.");
                                break; // salimos del bucle
                            }
                            catch (SocketException se)
                            {
                                if (se.SocketErrorCode == SocketError.TimedOut) // ha habido un timeout
                                    Console.WriteLine("ACK not received. Resending message..."); // nos quedamos en el bucle para reenviar el mensaje
                                else // tratar otro tipo de error
                                    Console.WriteLine(se.ErrorCode + ": " + se.Message);
                            }
                        }
                    }
                    catch (SocketException se)
                    {
                        Console.WriteLine(se.ErrorCode + ": " + se.Message);
                    }
                }
                // Queremos introducirlos por consola
                else if (sendFile == "no")
                {
                    while (true)
                    {
                        // Pedimos por consola el número a enviar
                        Console.WriteLine("\nIntroduce a number to send to the server: (introducing 'exit' will close the connection)");
                        string input = Console.ReadLine();

                        if (input != null)
                        {
                            input = input.ToLower(); // convertimos la entrada a minúsculas
                        } 

                        if (input == "exit")
                        {
                            Console.WriteLine("Closing connection...");

                            // enviamos mensaje de cierre
                            sequence = -1; // establecemos la secuencia en -1 para indicar que se cierra la conexión
                            SNF_message closingMessage = new SNF_message(sequence);
                            try
                            {
                                bool ACKreceived = false; // establecemos el booleano de ACK en falso para proceder al reenvío en caso de pérdida
                                while (!ACKreceived)
                                {
                                    if (random.Next(0, 100) > LOST_PROB) // caso en el que se envia el paquete con exito (80% de las veces)
                                        SendMessage(client, closingMessage, codec, endPoint); // llamada a método para enviar el mensaje
                                    else // caso en el que se pierde el paquete (20% de las veces)
                                        Console.WriteLine("Message with sequence number " + closingMessage.sequence + " sent, but was lost (simulation of losses)");

                                    try
                                    {
                                        SNF_message rcvMessage = ReceiveMessage(client, codec, endPoint); // llamada a método para recibir el mensaje, inicio de temporizador
                                        ACKreceived = true; // si se recibe el ACK antes de que salte el timeout, establecemos el ACK en true para salir del bucle y cerrar la conexion

                                        Console.WriteLine("Connection closed successfully.");
                                        break; // salimos del bucle
                                    }
                                    catch (SocketException se)
                                    {
                                        if (se.SocketErrorCode == SocketError.TimedOut) // ha habido un timeout
                                            Console.WriteLine("ACK not received. Resending message..."); // nos quedamos en el bucle para reenviar el mensaje
                                        else // tratar otro tipo de error
                                            Console.WriteLine(se.ErrorCode + ": " + se.Message);
                                    }
                                }
                            }
                            catch (SocketException se)
                            {
                                Console.WriteLine(se.ErrorCode + ": " + se.Message);
                            }

                            break; // salimos del bucle de entrada de numeros por teclado
                        }

                        // si la conversion de la entrada a entero es exitosa, se entiende que la entrada es válida
                        if (int.TryParse(input, out int num))
                        {
                            sbyte data = (sbyte)num; // convertimos el entero a byte
                            SNF_message message = new SNF_message(sequence, data); // creamos el mensaje SNF

                            try
                            {
                                bool ACKreceived = false; // establecemos el booleano de ACK en falso para proceder al reenvío en caso de pérdida
                                while (!ACKreceived) // hasta que no se reciba el ACK, se reenvía el mensaje
                                {
                                    if (random.Next(0, 100) > LOST_PROB)  // caso en el que se envia el paquete con exito (80% de las veces)
                                        SendMessage(client, message, codec, endPoint); // llamada a método para enviar el mensaje, se inicia el temporizador
                                    else // caso en el que se pierde el paquete (20% de las veces)
                                        Console.WriteLine("Number " + message.number + " sent with sequence number " + message.sequence + ", but was lost (simulation of losses)");

                                    try
                                    {
                                        // recepción del ACK
                                        SNF_message rcvMessage = ReceiveMessage(client, codec, endPoint); // llamada a método para recibir el mensaje, se inicia el temporizador
                                        ACKreceived = true; // si se recibe el ACK antes de que salte el timeout, establecemos ek ACK en true para salir del bucle y pasar al envío del siguiente mensaje

                                        sequence++; // si se ha recibido el ACK con éxito, aumentamos la secuencia en 1 para el siguiente envio
                                        if (sequence == 127) // si el numero de secuencia llega a 127, se reinicia a 0
                                            sequence = 0;
                                    }
                                    catch (SocketException se)
                                    {
                                        if (se.SocketErrorCode == SocketError.TimedOut) // ha habido un timeout
                                            Console.WriteLine("ACK not received. Resending message..."); // nos quedamos en el bucle para reenviar el mensaje
                                        else // tratar otro tipo de error
                                            Console.WriteLine(se.ErrorCode + ": " + se.Message);
                                    }
                                }
                            }
                            catch (SocketException se)
                            {
                                Console.WriteLine(se.ErrorCode + ": " + se.Message);
                            }
                        }

                        // El usuario ha introducido un valor no valido
                        else
                        {
                            Console.WriteLine("Error! Introduce a number:");
                        }
                    }
                }
            }
            finally
            {
                client.Close(); // si se dejan de enviar mensajes, se cierra el socket
            }
        }


        // Metodo para enviar el paquete
        public static void SendMessage(UdpClient client, SNF_message message, BinaryCodec codec, IPEndPoint endPoint)
        {
            byte[] packet = codec.Encode(message); // antes que nada, codificamos el mensaje
            client.Send(packet, packet.Length, endPoint); // enviamos
            Console.WriteLine("Number " + message.number + " sent with sequence number: " + message.sequence);
        }

        // Metodo para recibir el paquete
        public static SNF_message ReceiveMessage(UdpClient client, BinaryCodec codec, IPEndPoint endPoint)
        {
            byte[] rcvPacket = client.Receive(ref endPoint); // recibimos
            SNF_message rcvMessage = codec.Decode(rcvPacket); // decodificamos
            Console.WriteLine("ACK received with sequence number: " + rcvMessage.sequence);
            return rcvMessage;
        }

    }
}
