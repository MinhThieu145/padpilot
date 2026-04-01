using System;
using System.Collections.Generic;

namespace ChatVisual
{
    /// <summary>
    /// Represents a single message within the conversation history, 
    /// acting as a neutral data container shared between different AI clients.
    /// </summary>
    internal class SharedMessage
    {
        public ChatMessageRole Role { get; set; }
        public string Message { get; set; }
        public List<byte[]> Screenshots { get; set; } = new List<byte[]>();
    }
}
