using System;
using System.Runtime.Serialization;

namespace DCI.SystemEvents.Handlers
{
    internal class TransientException : Exception
    {
        public TransientException() : base()
        {

        }
        public TransientException(string message) : base(message)
        {
        }

        public TransientException(string message, Exception innerException) : base(message, innerException)
        {

        }

        public TransientException(SerializationInfo info, StreamingContext context) : base(info, context)
        {

        }


    }
}
