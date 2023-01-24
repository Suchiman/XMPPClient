using System.Xml.Linq;

namespace XMPPClient;

public static class ChatStates
{
    public static readonly XNamespace NS = "http://jabber.org/protocol/chatstates";

    public static readonly XName Gone = NS + "gone";
    public static readonly XName Inactive = NS + "inactive";
    public static readonly XName Active = NS + "active";
    public static readonly XName Paused = NS + "paused";
    public static readonly XName Composing = NS + "composing";
}