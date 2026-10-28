namespace OnionSquire.Models;

public sealed class RetainerInfo
{
    public ulong RetainerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Index { get; set; }
    public byte Level { get; set; }
    public byte ClassJob { get; set; }
    public byte ItemCount { get; set; }
    public byte MarketItemCount { get; set; }
    public bool Available { get; set; } = true;
    public string Source { get; set; } = "Memory";
}
