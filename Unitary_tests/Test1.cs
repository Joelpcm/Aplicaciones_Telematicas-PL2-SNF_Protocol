using System.Net.Sockets;
using System.Net;
using SNF_definition;
using Cliente;
using Servidor;

namespace Unitary_tests
{
    // Tests para la clase SNF_message
    [TestClass]
    public class SNFTests
    {
        // Este test verifica que la codificacion y decodificacion funcione.
        [TestMethod]
        public void BinaryCodec_EncodeDecode()
        {
            BinaryCodec codec = new BinaryCodec();

            // Test para mensaje DATA
            SNF_message originalDataMessage = new SNF_message(1, 2); // sequence = 1, number = 2
            byte[] encodedDataMessage = codec.Encode(originalDataMessage); // codificar mensaje
            SNF_message decodedDataMessage = codec.Decode(encodedDataMessage); // decodificar mensaje

            Assert.AreEqual(originalDataMessage.sequence, decodedDataMessage.sequence); // verificar que el sequence number sea igual
            Assert.AreEqual(originalDataMessage.number, decodedDataMessage.number); // verificar que los datos sean iguales

            // Test para mensaje ACK
            SNF_message originalAckMessage = new SNF_message(1); // sequence = 1
            byte[] encodedAckMessage = codec.Encode(originalAckMessage); // codificar mensaje
            SNF_message decodedAckMessage = codec.Decode(encodedAckMessage); // decodificar mensaje

            Assert.AreEqual(originalAckMessage.sequence, decodedAckMessage.sequence); // verificar que el sequence number sea igual
            Assert.AreEqual(originalAckMessage.number, decodedAckMessage.number); // verificar que los datos sean iguales
        }
    }

    // Tests para la conexión entre cliente y servidor
    [TestClass]
    public class ClientServerConnectionTests
    {
        [TestMethod]

        // Este test verifica que el cliente(creado dentro del test) pueda establecer una conexion con un servidor. (SOLO TESTEA EL PROGRAMA SERVIDOR)
        public void Client_EstablishConnection()
        {
            // Configuración del servidor
            int serverPort = 999; // puerto del servidor
            UdpClient server = new UdpClient(serverPort); // creamos el servidor UDP
            IPEndPoint remoteIPEndPoint = new IPEndPoint(IPAddress.Any, 0); // direccion desde donde recibir(modificada tras la recepción)
            BinaryCodec codec = new BinaryCodec(); // creamos una instancia del codec

            // Configuración del cliente
            Task.Run(() =>
            {
                var cliente = new Cliente.Program(); // creamos el cliente
                cliente.Run(serverPort); // ejecutamos el cliente
            });

            // Recibir mensaje de inicio en el servidor
            byte[] receivedStartPacket = server.Receive(ref remoteIPEndPoint); // recibir mensaje
            SNF_message receivedStartMessage = codec.Decode(receivedStartPacket); // decodificar mensaje
            Assert.AreEqual(Convert.ToSByte(0), receivedStartMessage.sequence); // verificar que el sequence number sea 0

            // Enviar ACK de inicio
            SNF_message ackMessage = new SNF_message(0); // crear mensaje ACK
            byte[] ackPacket = codec.Encode(ackMessage); // codificar mensaje
            server.Send(ackPacket, ackPacket.Length, remoteIPEndPoint); // enviar mensaje

            // Cerrar conexiones
            server.Close();
        }

        // Este test verifica que el servidor(creado dentro del test) pueda establecer una conexion con un cliente. (SOLO TESTEA EL PROGRAMA CLIENTE)
        [TestMethod]
        public void Server_EstablishConnection()
        {
            // Configuración del servidor
            int serverPort = 1001; // puerto del servidor
            Task.Run(() =>
            {
                var servidor = new Servidor.Program(); // creamos el servidor
                servidor.Run(serverPort); // ejecutamos el servidor
            });
            Thread.Sleep(1000); // Para asegurarnos de que el servidor se inicie antes de que el cliente intente conectarse

            // Configuración del cliente
            UdpClient client = new UdpClient(); // creamos el cliente UDP
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), serverPort); // direccion del servidor
            BinaryCodec codec = new BinaryCodec(); // usamos el codec definido

            // Enviar mensaje de inicio
            SNF_message startMessage = new SNF_message(Convert.ToSByte(0)); // crear mensaje de inicio
            byte[] startPacket = codec.Encode(startMessage); // codificar mensaje
            client.Send(startPacket, startPacket.Length, endPoint); // enviar mensaje

            // Recibir ACK de inicio
            client.Client.ReceiveTimeout = 5000; // Ponemos timeout de 5 seconds
            try
            {
                byte[] receivedAckPacket = client.Receive(ref endPoint); // recibir mensaje
                SNF_message receivedAckMessage = codec.Decode(receivedAckPacket); // decodificar mensaje
                Assert.AreEqual(Convert.ToSByte(0), receivedAckMessage.sequence); // verificar que el sequence number sea 0
            }
            catch (SocketException)
            {
                Assert.Fail("Failed to receive ACK from server (First message always works).");
            }

            // Cerrar conexiones
            client.Close();
        }
    } 
}