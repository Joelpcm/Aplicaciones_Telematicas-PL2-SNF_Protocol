using System;
using System.Net.Sockets;
using System.Net;
using SNF_definition;
using System.Runtime;
using System.IO;

namespace Servidor
{
    public class Program
    {
        public static void Main()
        {
            var servidor = new Program();
            servidor.Run(1000);
        }

        public void Run(int serverPort)
        {
            // creacion del socket UDP0;
            UdpClient server;
            try
            {
                // enlazar el socket en un puerto
                server = new UdpClient(serverPort);
            }
            catch (SocketException se)
            {
                Console.WriteLine(se.ErrorCode + ": " + se.Message);
                return;
            }

            IPEndPoint remoteIPEndPoint = new IPEndPoint(IPAddress.Any, 0); // dirección desde donde recibir. Se modificará tras la recepción
            BinaryCodec codec = new BinaryCodec();  // creamos una instancia del codec
            sbyte expectedSeq = 0;                    // el primer número de secuencia es 0
            const int LOST_PROB = 20;               // establecemos la probabilidad de pérdida en 20 %
            Random random = new Random();           // número random para la probabilidad de pérdida

            try
            {
                Console.WriteLine("Server initiated succesfully. Awaiting initial message...");

                // recibir el mensaje inicial
                SNF_message rcvMessage = ReceiveMessage(server, codec, ref remoteIPEndPoint); // llamada a metodo para recibir el mensaje

                if (rcvMessage.sequence == 0 && rcvMessage.sequence == expectedSeq) // si el mensaje recibido es el esperado
                {
                    // enviamos el ACK
                    SendMessage(server, rcvMessage, codec, remoteIPEndPoint); // llamada a metodo para enviar el mensaje
                    expectedSeq++; // aumentamos número de secuencia
                    Console.WriteLine("Transmission iniciated from IP " + remoteIPEndPoint.Address + ". Ready to receive.\n");

                    //Preguntamos si queremos que el servidor reciba los numeros y los escriba en un archivo
                    //o si queremos que el servidor lo muestre por consola
                    Console.WriteLine("Do you want to receive numbers and write them in a file? (yes/no)");
                    string sendFile = Console.ReadLine().ToLower();

                    // El usuario ha introducido un valor no valido y se le vuelve a preguntar
                    while (sendFile != "yes" && sendFile != "no")
                    {
                        Console.WriteLine("Please, introduce a valid option (yes/no)");
                        sendFile = Console.ReadLine().ToLower();
                    }

                    // Si el usuario quiere que se escriban los numeros en un archivo
                    if (sendFile == "yes")
                    {
                        Console.WriteLine("--Numbers will be written in a file\n");

                        // Obtener la ruta del escritorio y definir el nombre del archivo
                        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                        string filePath = Path.Combine(desktopPath, "received_file.txt");

                        // Abrir el archivo para escribir
                        using (StreamWriter writer = new StreamWriter(filePath, true))
                        {
                            // el servidor se ejecuta infinitamente
                            for (; ; )
                            {
                                try
                                {
                                    // recibir el mensaje
                                    rcvMessage = ReceiveMessage(server, codec, ref remoteIPEndPoint); // llamada a metodo para recibir el mensaje

                                    // caso 1: el mensaje recibido es el esperado
                                    if (rcvMessage.sequence == expectedSeq)
                                    {
                                        // enviamos el ACK, teniendo en cuenta la posible pérdida en su transmisión
                                        if (random.Next(0, 100) > LOST_PROB) // se envia el ACK y llega con éxito (80% de las veces)
                                        {
                                            SendMessage(server, rcvMessage, codec, remoteIPEndPoint); // llamada a metodo para enviar el mensaje
                                        }
                                        else // se envia el ACK pero se pierde por el camino (20% de las veces)
                                        {
                                            // No enviamos nada para simular la perdida
                                            Console.WriteLine("ACK sent with sequence number " + rcvMessage.sequence + " ,but was lost (simulation of losses)");
                                        }

                                        // Escribir el número recibido en el archivo
                                        writer.WriteLine(rcvMessage.number);

                                        // aumentamos número de secuencia en ambos casos, ya que en ambos casos se envió el ACK
                                        expectedSeq++;
                                        if (expectedSeq == 127) // si el número de secuencia llega a 127, se reinicia a 0
                                        {
                                            expectedSeq = 0;
                                        }
                                    }
                                    // caso 2: el mensaje recibido ya se habia recibido antes, asi que suponemos perdida en el ACK anterior y se reenvia
                                    else if (rcvMessage.sequence == expectedSeq - 1)
                                    {
                                        Console.WriteLine("Duplicated message received: ACK of last message could be lost. Resending...");
                                        // Reenviar
                                        SendMessage(server, rcvMessage, codec, remoteIPEndPoint);
                                    }
                                    // caso 3: recibido -1, fin de conexion
                                    else if (rcvMessage.sequence == -1)
                                    {
                                        Console.WriteLine("Received -1 sequence number, closing connection");
                                        // enviamos el ACK, teniendo en cuenta la posible pérdida en su transmisión
                                        if (random.Next(0, 100) > LOST_PROB) // se envia el ACK y llega con éxito (80% de las veces)
                                        {
                                            SendMessage(server, rcvMessage, codec, remoteIPEndPoint); // llamada a metodo para enviar el mensaje
                                            break;
                                        }
                                        else // se envia el ACK pero se pierde por el camino (20% de las veces)
                                        {
                                            // No enviamos nada para simular la perdida
                                            Console.WriteLine("ACK of end of transmission sent, but was lost (simulation of losses)");
                                        }
                                    }
                                    // caso 3: el mensaje recibido no es el esperado, descartamos el mensaje
                                    else
                                    {
                                        Console.WriteLine("Received message doesn't match the expected one");
                                    }
                                }
                                catch (SocketException se)
                                {
                                    Console.WriteLine(se.ErrorCode + ": " + se.Message);
                                    return;
                                }
                            }
                        }
                    }
                    // Si el usuario no quiere que se escriban los numeros en un archivo, se muestran por consola
                    else if (sendFile == "no")
                    {
                        Console.WriteLine("--Numbers will only be shown in console\n");

                        // el servidor se ejecuta infinitamente
                        for (; ; )
                        {
                            try
                            {
                                // recibir el mensaje
                                rcvMessage = ReceiveMessage(server, codec, ref remoteIPEndPoint); // llamada a metodo para recibir el mensaje

                                // caso 1: el mensaje recibido es el esperado
                                if (rcvMessage.sequence == expectedSeq)
                                {
                                    // enviamos el ACK, teniendo en cuenta la posible pérdida en su transmisión
                                    if (random.Next(0, 100) > LOST_PROB) // se envia el ACK y llega con éxito (80% de las veces)
                                    {
                                        SendMessage(server, rcvMessage, codec, remoteIPEndPoint); // llamada a metodo para enviar el mensaje
                                    }
                                    else // se envia el ACK pero se pierde por el camino (20% de las veces)
                                    {
                                        // No enviamos nada para simular la perdida
                                        Console.WriteLine("ACK sent with sequence number " + rcvMessage.sequence + " ,but was lost (simulation of losses)");
                                    }

                                    // aumentamos número de secuencia en ambos casos, ya que en ambos casos se envió el ACK
                                    expectedSeq++;
                                    if (expectedSeq == 127) // si el número de secuencia llega a 127, se reinicia a 0
                                    {
                                        expectedSeq = 0;
                                    }
                                }
                                // caso 2: el mensaje recibido ya se habia recibido antes, asi que suponemos perdida en el ACK anterior y se reenvia
                                else if (rcvMessage.sequence == expectedSeq - 1)
                                {
                                    Console.WriteLine("Duplicated message received: ACK of last message could be lost. Resending...");

                                    // Reenviar
                                    SendMessage(server, rcvMessage, codec, remoteIPEndPoint);
                                }
                                // caso 3: recibido -1, fin de conexion
                                else if (rcvMessage.sequence == -1)
                                {
                                    Console.WriteLine("Received -1 sequence number, closing connection");
                                    // enviamos el ACK, teniendo en cuenta la posible pérdida en su transmisión
                                    if (random.Next(0, 100) > LOST_PROB) // se envia el ACK y llega con éxito (80% de las veces)
                                    {
                                        SendMessage(server, rcvMessage, codec, remoteIPEndPoint); // llamada a metodo para enviar el mensaje
                                        break;
                                    }
                                    else // se envia el ACK pero se pierde por el camino (20% de las veces)
                                    {
                                        // No enviamos nada para simular la perdida
                                        Console.WriteLine("ACK of end of transmission sent, but was lost (simulation of losses)");
                                    }
                                }
                                // caso 3: el mensaje recibido no es el esperado, descartamos el mensaje
                                else
                                {
                                    Console.WriteLine("Received message doesn't match the expected one");
                                }
                            }
                            catch (SocketException se)
                            {
                                Console.WriteLine(se.ErrorCode + ": " + se.Message);
                                return;
                            }
                        }
                    }
                }
            }
            finally
            {
                server.Close();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(); // Mantener la consola abierta hasta que el usuario presione una tecla
            }
        }

        // Metodo para recibir el paquete
        public static SNF_message ReceiveMessage(UdpClient client, BinaryCodec codec, ref IPEndPoint remoteIPEndPoint)
        {
            // Recibir el paquete
            byte[] rcvPacket = client.Receive(ref remoteIPEndPoint);

            // Decodificar el paquete
            SNF_message rcvMessage = codec.Decode(rcvPacket);

            if (rcvMessage._number == null)
                // Se trata de un mensaje especial (0 o -1). Se muestra en pantalla
                Console.WriteLine("\nReceived message with sequence number " + rcvMessage.sequence);
            else
                // Mostrar el mensaje recibido  asi como su numero de secuencia
                Console.WriteLine("\nReceived message is " + rcvMessage.number + ", with sequence number " + rcvMessage.sequence);

            return rcvMessage;
        }

        // Metodo para enviar el paquete
        public static void SendMessage(UdpClient client, SNF_message rcvMessage, BinaryCodec codec, IPEndPoint remoteIPEndPoint)
        {
            // Crear el mensaje ACK
            SNF_message message = new SNF_message(rcvMessage.sequence); // creamos mensaje ACK con el número de secuencia del mensaje recibido

            // Codificar el mensaje
            byte[] packet = codec.Encode(message);

            // Enviar el mensaje
            client.Send(packet, packet.Length, remoteIPEndPoint);

            // Se muestra que se ha enviado el mensaje y su numero de secuencia
            Console.WriteLine("ACK sent, with sequence number " + message.sequence);
        }
    }
}
