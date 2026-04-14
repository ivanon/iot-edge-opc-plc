using Newtonsoft.Json;

namespace OpcPlc.PluginNodes;

public class SimulationConfig
{
    public string Type { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public double Min { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public double Max { get; set; }
}
