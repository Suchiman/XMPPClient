using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace XMPPClient;

public class XMPPXmlReader : XmlTextReader
{
    public XMPPXmlReader(Stream input) : base(input)
    {
    }

    public XElement ReadNodeHacked()
    {
        XDocument doc = new XDocument();
        XmlWriter xtw = doc.CreateWriter();

        string startElement = Name;
        int startingDepth = (NodeType == XmlNodeType.None ? -1 : Depth);
        do
        {
            switch (NodeType)
            {
                case XmlNodeType.Element:
                    xtw.WriteStartElement(Prefix, LocalName, NamespaceURI);
                    xtw.WriteAttributes(this, false);
                    if (!IsEmptyElement)
                    {
                        continue;
                    }
                    xtw.WriteEndElement();
                    if (Depth == startingDepth && string.Equals(Name, startElement, StringComparison.Ordinal))
                    {
                        break;
                    }
                    continue;
                case XmlNodeType.Text:
                    xtw.WriteString(Value);
                    continue;
                case XmlNodeType.CDATA:
                    xtw.WriteCData(Value);
                    continue;
                case XmlNodeType.EntityReference:
                    xtw.WriteEntityRef(Name);
                    continue;
                case XmlNodeType.ProcessingInstruction:
                case XmlNodeType.XmlDeclaration:
                    xtw.WriteProcessingInstruction(Name, Value);
                    continue;
                case XmlNodeType.Comment:
                    xtw.WriteComment(Value);
                    continue;
                case XmlNodeType.DocumentType:
                    xtw.WriteDocType(Name, GetAttribute("PUBLIC"), GetAttribute("SYSTEM"), Value);
                    continue;
                case XmlNodeType.Whitespace:
                case XmlNodeType.SignificantWhitespace:
                    xtw.WriteWhitespace(Value);
                    continue;
                case XmlNodeType.EndElement:
                    xtw.WriteFullEndElement();
                    if (Depth == startingDepth && string.Equals(Name, startElement, StringComparison.Ordinal))
                    {
                        break;
                    }
                    continue;
                default:
                    continue;
            }
            xtw.Dispose();
            return doc.Root;
        }
        while (Read() && (startingDepth < Depth || startingDepth == Depth && NodeType == XmlNodeType.EndElement));
        throw new Exception("Das war nicht vorgesehen");
    }

    private bool ReadingInStream;
    public XElement ReadElement()
    {
        if (EOF)
        {
            return null;
        }

        if (!ReadingInStream)
        {
            while (Read() && !(string.Equals(LocalName, "stream", StringComparison.Ordinal) && string.Equals(NamespaceURI, "http://etherx.jabber.org/streams", StringComparison.Ordinal))) ;
            ReadingInStream = true;
        }
        Read();
        if (NodeType == XmlNodeType.Element)
        {
            return ReadNodeHacked();
        }
        throw new Exception("WTF maaan...");
    }
}