using System;
using System.IO;

namespace SNF_definition
{
    public class SNF_message
    {
        public sbyte _sequence; // _sequence ahora puede ser un byte que contemple valores negativos
        public  sbyte? _number;  //_number puede ser null si se trata de un ACK

        public sbyte sequence
        {
            get { return _sequence; }
            set { _sequence = value; }
        }

        public sbyte? number
        {
            get { return _number; }
            set { _number = value; }
        }

        // Constructor para mensaje DATA
        public SNF_message(sbyte seq, sbyte num)
        {
            _sequence = seq;
            _number = num;
        }

        // Constructor para mensaje ACK
        public SNF_message(sbyte seq)
        {
            _sequence = seq;
            _number = null;
        }
    }

    //Interfaz de codificacion/decodificacion
    public interface ICodec
    {
        byte[] Encode(SNF_message message);
        SNF_message Decode(byte[] info);
    }

    public class BinaryCodec : ICodec
    {
        public byte[] Encode(SNF_message message)
        {
            byte[] byteBuffer;
            using MemoryStream ms = new MemoryStream(); //Se usa using para asegurar que los streams se cierren correctamente.
            using BinaryWriter writer = new BinaryWriter(ms);

            // Si el mensaje es un ACK solo esto
            writer.Write(message.sequence);

            // Si el mensaje es un DATA se escribe el numero
            if (message.number.HasValue)
                writer.Write((sbyte)message.number);

            // Se limpia el buffer y se escribe en el stream
            writer.Flush();
            // Se convierte el stream a un array de bytes
            byteBuffer = ms.ToArray();

            return byteBuffer;
        }

        public SNF_message Decode(byte[] info)
        {
            using MemoryStream ms = new MemoryStream(info);
            using BinaryReader reader = new BinaryReader(ms);

            if (info.Length == 2) // El mensaje es un DATA
            {
                // Se leen el numero de secuencia
                sbyte seq = reader.ReadSByte();

                // Se lee el numero
                sbyte num = reader.ReadSByte();

                // Se crea el mensaje
                SNF_message message = new SNF_message(seq, num);

                return message;
            }
            else // El mensaje es un ACK
            {
                // Simplemente se lee el primer byte
                sbyte seq = reader.ReadSByte();

                // Se crea el mensaje
                SNF_message message = new SNF_message(seq);

                return message;
            }

        }
    }

}
