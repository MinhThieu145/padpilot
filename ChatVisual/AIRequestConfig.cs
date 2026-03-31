using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatVisual
{
    /// <summary>
    /// Config for the AI models parameters. Has 4 main properties: Model, SystemPrompt, Temperature and MaxOutputToken. 
    /// </summary>
    internal class AIRequestConfig
    {
        public string Model { get; set;  }
        public string SystemPrompt { get; set; }
        public float Temperature { get; set; }
        public int MaxOutputToken { get; set; }

    }
}
