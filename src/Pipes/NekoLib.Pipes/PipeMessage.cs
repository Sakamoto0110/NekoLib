#if NET9
using System.Text.Json;
#else
using Newtonsoft.Json.Linq;
#endif
using System;
 
namespace NekoLib.Pipes
{


    public sealed class PipeMessage
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = "";
        public string Name { get; set; } = "";
        public bool Ok { get; set; } = true;

#if NET9
    public JsonElement? Data { get; set; }
#else
        public Newtonsoft.Json.Linq.JToken? Data { get; set; }
#endif

        public PipeError? Error { get; set; }
    }

  




}


