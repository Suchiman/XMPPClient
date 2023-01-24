
namespace XMPPClient;

public class JID
{
    public string Username { get; }
    public string Domain { get; }
    public string Resource { get; }

    public JID(string jid)
    {
        //StringSlicer slicer = new StringSlicer(jid);
        //Username = slicer.SliceToChar('@');
        //slicer.SkipChar();
        string[] split = jid.Split('@');
        Username = split[0];
        split = split[1].Split('/');
        Domain = split[0];
        if (split.Length > 1)
        {
            Resource = split[1];
        }
    }

    public JID(string username, string domain, string resource)
    {
        Username = username;
        Domain = domain;
        Resource = resource;
    }

    public override string ToString() => $"{Username}@{Domain}{(Resource != null ? "/" + Resource : null)}";
}