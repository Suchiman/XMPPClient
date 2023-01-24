using System.Xml.Linq;

namespace XMPPClient;

public class IQ
{
    public string Id { get; set; }
    public JID From { get; set; }
    public JID To { get; set; }
    public IqType Type { get; set; }
    public XElement Content { get; set; }
}