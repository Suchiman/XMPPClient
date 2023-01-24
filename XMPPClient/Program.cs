using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace XMPPClient;

public class Program
{
    static void Main(string[] args)
    {
        XmlTcpConnection c = new XmlTcpConnection("servername", 5222);
        c.Login("username", "password", "client_id");
    }
}

public class XmlTcpConnection
{
    private static Encoding encoding = new UTF8Encoding(false);

    private const int ReadBufferSize = 4096;

    private string Hostname;
    private int Port;

    private string Realm;
    private long lastIQID;
    private string NextID => (++lastIQID).ToString();

    private TcpClient client;
    private Stream stream;

    private XMPPXmlWriter writer;
    private XMPPXmlReader reader;

    private IQManager IqManager;

    public XmlTcpConnection(string hostname, int port)
    {
        Hostname = hostname;
        Port = port;

        Realm = Hostname;
    }

    private void ResetWriter()
    {
        writer = new XMPPXmlWriter(stream, encoding);
    }

    private void ResetReader()
    {
        reader = new XMPPXmlReader(stream);
    }

    private void OpenStream()
    {
        ResetWriter();
        writer.WriteStartElement("stream", "stream", "http://etherx.jabber.org/streams");
        writer.WriteAttributeString("xmlns", "jabber:client");
        writer.WriteAttributeString("to", Hostname);
        writer.WriteAttributeString("version", "1.0");
        writer.WriteEndStartTag();
        writer.Flush();
        ResetReader();
    }

    public void Login(string Username, string Passwort, string Resource)
    {
        client = new TcpClient(Hostname, Port);
        stream = client.GetStream();

        OpenStream();

        XElement features = reader.ReadElement();

        if (features.DescendantNodes().OfType<XElement>().Any(x => x.Name == "{urn:ietf:params:xml:ns:xmpp-tls}starttls"))
        {
            writer.WriteStartElement("starttls", "urn:ietf:params:xml:ns:xmpp-tls");
            writer.WriteEndElement();
            writer.Flush();

            XElement starttlsResp = reader.ReadElement();
            if (starttlsResp.Name == "{urn:ietf:params:xml:ns:xmpp-tls}proceed")
            {
                SslStream sslStream = new SslStream(stream, true, (a, b, c, d) => true);
                sslStream.AuthenticateAsClient(Hostname);
                stream = sslStream;
                OpenStream();
                features = reader.ReadElement();
            }
        }

        IqManager = new IQManager(writer);

        //writer.WriteStartElement("iq");
        //writer.WriteAttributeString("type", "get");
        //writer.WriteAttributeString("id", NextID);
        //writer.WriteStartElement("query", "jabber:iq:register");
        //writer.WriteEndElement();
        //writer.WriteEndElement();
        //writer.Flush();

        //XElement getRegisterInfo = ReadElement();

        if (false)
        {
            string some = Convert.ToBase64String(Encoding.UTF8.GetBytes("\0" + Username + "\0" + Passwort));
            writer.WriteStartElement("auth", "urn:ietf:params:xml:ns:xmpp-sasl");
            writer.WriteAttributeString("mechanism", "PLAIN");
            writer.WriteString(some);
            writer.WriteEndElement();
            writer.Flush();

            XElement plainAuthResponse = reader.ReadElement();
            if (plainAuthResponse.Name != "{urn:ietf:params:xml:ns:xmpp-sasl}success")
            {
                Console.WriteLine("Fehler bei Authentifizierung");
                return;
            }
        }

        if (!features.DescendantNodes().OfType<XElement>().Any(x => x.Name == "{urn:ietf:params:xml:ns:xmpp-sasl}mechanism" && string.Equals(x.Value, "DIGEST-MD5", StringComparison.Ordinal)))
        {
            throw new NotImplementedException("Authentifizierungsmechanismus nicht unterstützt");
        }

        writer.WriteStartElement("auth", "urn:ietf:params:xml:ns:xmpp-sasl");
        writer.WriteAttributeString("mechanism", "DIGEST-MD5");
        writer.WriteEndElement();
        writer.Flush();

        XElement challenge = reader.ReadElement();
        if (challenge.Name == "{urn:ietf:params:xml:ns:xmpp-sasl}failure")
        {
            throw new Exception("Authentication failed");
        }

        byte[] result = Convert.FromBase64String(challenge.Value);
        string decoded = Encoding.UTF8.GetString(result);
        Dictionary<string, string> values = decoded.Split(',').Select(x => x.Split('=')).ToDictionary(k => k[0], v => v[1].Trim('"'));

        string cnonce = Guid.NewGuid().ToString();
        string digestUri = "xmpp/" + Realm;
        string nc = "00000001";

        //http://deusty.blogspot.de/2007/09/example-please.html
        byte[] ha1 = MD5.HashData(Encoding.UTF8.GetBytes($"{Username}:{Realm}:{Passwort}"));
        List<byte> combined = new List<byte>();
        combined.AddRange(ha1);
        combined.AddRange(Encoding.UTF8.GetBytes($":{values["nonce"]}:{cnonce}"));
        ha1 = MD5.HashData(combined.ToArray());

        byte[] ha2 = MD5.HashData(Encoding.UTF8.GetBytes("AUTHENTICATE:" + digestUri));

        byte[] hash = Encoding.UTF8.GetBytes($"{Convert.ToHexString(ha1).ToLower()}:{values["nonce"]}:{nc}:{cnonce}:{values["qop"]}:{Convert.ToHexString(ha2).ToLower()}");
        hash = MD5.HashData(hash);
        string response = Convert.ToHexString(hash).ToLower();

        string shit = $"""username="\{Username}",realm="{Realm}",nonce="{values["nonce"]}",cnonce="{cnonce}",nc={nc},qop={values["qop"]},digest-uri="{digestUri}",response={response},charset=utf-8""";
        string realResponse = Convert.ToBase64String(Encoding.UTF8.GetBytes(shit));

        writer.WriteElementString("response", "urn:ietf:params:xml:ns:xmpp-sasl", realResponse);
        writer.Flush();

        XElement responseElement = reader.ReadElement();

        if (responseElement.Name == "{urn:ietf:params:xml:ns:xmpp-sasl}challenge")
        {
            writer.WriteStartElement("response", "urn:ietf:params:xml:ns:xmpp-sasl");
            writer.WriteEndElement();
            writer.Flush();

            responseElement = reader.ReadElement();
        }

        if (responseElement.Name != "{urn:ietf:params:xml:ns:xmpp-sasl}success")
        {
            Console.WriteLine("Fehler bei Authentifizierung");
            return;
        }

        OpenStream();

        features = reader.ReadElement();

        writer.WriteStartElement("iq");
        writer.WriteAttributeString("type", "set");
        writer.WriteAttributeString("id", NextID);
        writer.WriteStartElement("bind", "urn:ietf:params:xml:ns:xmpp-bind");
        if (!String.IsNullOrWhiteSpace(Resource))
        {
            writer.WriteElementString("resource", Resource);
        }
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.Flush();

        XElement bindResponse = reader.ReadElement();

        writer.WriteStartElement("iq");
        writer.WriteAttributeString("type", "set");
        writer.WriteAttributeString("id", NextID);
        writer.WriteStartElement("session", "urn:ietf:params:xml:ns:xmpp-session");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.Flush();

        XElement sessionResp = reader.ReadElement();

        //<presence><priority>1</priority><c xmlns='http://jabber.org/protocol/caps' node='http://pidgin.im/' hash='sha-1' ver='I22W7CegORwdbnu0ZiQwGpxr0Go='/><x xmlns='vcard-temp:x:update'/></presence>
        writer.WriteStartElement("presence");
        writer.WriteElementString("priority", "1");
        writer.WriteStartElement("show");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.Flush();

        XElement presenceResp = reader.ReadElement();

        while (true)
        {
            XElement loop = reader.ReadElement();
            if (loop.Name == "{jabber:client}message")
            {
                JID from = new JID(loop.Attribute("from").Value);
                IEnumerable<XElement> descendants = loop.DescendantNodes().OfType<XElement>();

                if (descendants.Any(x => x.Name == ChatStates.Composing))
                {
                    Console.WriteLine($"{from.Username} hat angefangen zu schreiben");
                }
                if (descendants.Any(x => x.Name == ChatStates.Paused))
                {
                    Console.WriteLine($"{from.Username} hat aufgehört zu schreiben");
                }
                if (descendants.Any(x => x.Name == ChatStates.Active))
                {
                    Console.WriteLine($"{from.Username} beobachtet dich");
                }
                if (descendants.Any(x => x.Name == ChatStates.Inactive))
                {
                    Console.WriteLine($"{from.Username} ist jetzt inaktiv");
                }
                if (descendants.Any(x => x.Name == ChatStates.Gone))
                {
                    Console.WriteLine($"{from.Username} hat den chat geschlossen");
                }
                if (descendants.Any(x => x.Name == "{jabber:client}body"))
                {
                    string body = descendants.FirstOrDefault(x => x.Name == "{jabber:client}body").Value;
                    Console.WriteLine("{0} {1}: {2}", DateTime.Now.ToShortTimeString(), from.Username, body);
                }
            }
            else if (loop.Name == "{jabber:client}presence")
            {
                JID from = new JID(loop.Attribute("from").Value);

                Console.WriteLine($"Benutzer {from.Username} ist anwesend");
            }
            else if (loop.Name == "{jabber:client}iq")
            {
                JID from = new JID(loop.Attribute("from").Value);
                IqManager.OnReceive(loop);
                Console.WriteLine($"Benutzer {from.Username} ist anwesend");
            }
            else
            {
                throw new Exception("Unexpected root element");
            }
        }
    }

}

public class IQManager
{
    private XMPPXmlWriter Writer;
    private long lastIQID;
    private string NextID => (++lastIQID).ToString();
    private List<WaitingRequests> Requests = new List<WaitingRequests>();

    public IQManager(XMPPXmlWriter writer)
    {
        Writer = writer;
    }

    public void OnReceive(IQ received)
    {
        if (received.Type == IqType.Result)
        {
            WaitingRequests find = Requests.Find(x => string.Equals(x.Id, received.Id, StringComparison.Ordinal));
            Requests.Remove(find);
            find.Task.SetResult(received);
        }
    }

    internal void OnReceive(XElement loop)
    {
        IQ iq = new IQ();
        iq.Content = loop;
    }

    public Task<IQ> SendIQAsync(IQ request)
    {
        WaitingRequests result = new WaitingRequests { Id = NextID, Task = new TaskCompletionSource<IQ>() };

        Writer.WriteStartElement("iq");
        Writer.WriteAttributeString("type", request.Type.ToString().ToLowerInvariant());
        Writer.WriteAttributeString("id", result.Id);
        foreach (XElement children in request.Content.Descendants())
        {
            children.WriteTo(Writer);
        }
        Writer.WriteEndElement();
        Writer.Flush();

        Requests.Add(result);
        return result.Task.Task;
    }

    public Task<IQ> SendIQAsync(XElement content, IqType type)
    {
        WaitingRequests result = new WaitingRequests { Id = NextID, Task = new TaskCompletionSource<IQ>() };

        Writer.WriteStartElement("iq");
        Writer.WriteAttributeString("type", type.ToString().ToLowerInvariant());
        Writer.WriteAttributeString("id", result.Id);
        content.WriteTo(Writer);
        Writer.WriteEndElement();
        Writer.Flush();

        Requests.Add(result);
        return result.Task.Task;
    }

    class WaitingRequests
    {
        public string Id { get; set; }
        public TaskCompletionSource<IQ> Task { get; set; }
    }
}