using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatVisual
{
    /// <summary>
    /// Defines the different intelligence levels for the AI Orchestrator.
    /// </summary>
    public enum ResponseMode
    {
        // 1. Fast response using lightweight models (GPT-5.4-mini)
        Quick,

        // 2. Medium response using parallel agents and synthesis
        Thinking,

        // 3. High-quality response using reasoning models and multi-step logic
        DeepThinking
    }
}
