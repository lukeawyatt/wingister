using System;
using WinGister.Shell;

namespace WinGister.TestApplication
{
    public class Program
    {

        public static void Main(string[] args)
        {
            WinGisterExtension client = new WinGisterExtension();
            var gistResponse = client.GetGistList("lukeawyatt");

            Console.ReadLine();
        }

    }
}
