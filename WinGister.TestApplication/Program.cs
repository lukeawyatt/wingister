using System;
using WinGister.Shell;

namespace WinGister.TestApplication
{
    public class Program
    {

        public static void Main(string[] args)
        {
            DateTime _LastRefresh = DateTime.MinValue;
            var x = DateTime.Compare(DateTime.Now.AddMinutes(-15), _LastRefresh);

            WinGisterExtension client = new WinGisterExtension();
            var gistResponse = client.GetGistList("lukeawyatt");

            Console.ReadLine();
        }

    }
}
