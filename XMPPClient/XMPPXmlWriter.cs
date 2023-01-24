using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;

namespace XMPPClient;

public class XMPPXmlWriter : XmlTextWriter
{
    private Action<bool> Private_WriteEndStartTag;

    public XMPPXmlWriter(Stream w, Encoding encoding)
        : base(w, encoding)
    {
        Type baseClass = typeof(XmlTextWriter);

        MethodInfo WEST = baseClass.GetMethod("WriteEndStartTag", BindingFlags.Instance | BindingFlags.NonPublic);
        Private_WriteEndStartTag = (Action<bool>)WEST.CreateDelegate(typeof(Action<bool>), this);
    }

    public void WriteEndStartTag()
    {
        Private_WriteEndStartTag(false);
    }
}