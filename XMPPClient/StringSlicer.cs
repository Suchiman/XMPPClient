
namespace XMPPClient;

public struct StringSlicer
{
    string input;
    int currentIndex;

    public StringSlicer(string str)
    {
        input = str;
        currentIndex = 0;
    }

    private int IndexOf(int from, char c)
    {
        for (int index = from; index < input.Length; index++)
        {
            if (input[index] == c)
            {
                return index;
            }
        }

        return -1;
    }

    public string PeekToChar(char c)
    {
        int index = IndexOf(currentIndex, c);
        if (index != -1)
        {
            return input.Substring(currentIndex, index - currentIndex);
        }

        return null;
    }

    public string SliceToChar(char c)
    {
        int index = IndexOf(currentIndex, c);
        if (index != -1)
        {
            return input.Substring(currentIndex, index - currentIndex);
        }
        currentIndex = index;
        return null;
    }

    public void SkipChar()
    {
        currentIndex++;
    }
}