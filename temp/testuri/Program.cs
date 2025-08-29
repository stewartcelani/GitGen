using System;

class Program
{
    static void Main()
    {
        var url = "https://api.x.ai/v1/chat/completions";
        Console.WriteLine($"Testing URL: '{url}'");
        Console.WriteLine($"URL Length: {url.Length}");
        
        // Show hex dump
        Console.Write("Hex: ");
        foreach (char c in url)
        {
            Console.Write($"{(int)c:X2} ");
        }
        Console.WriteLine();
        
        try
        {
            var uri = new Uri(url, UriKind.Absolute);
            Console.WriteLine($"Success\! Uri: {uri}");
            Console.WriteLine($"Scheme: {uri.Scheme}");
            Console.WriteLine($"Host: {uri.Host}");
            Console.WriteLine($"PathAndQuery: {uri.PathAndQuery}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
