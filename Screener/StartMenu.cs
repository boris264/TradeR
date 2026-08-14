using Screener.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Screener
{
    internal class StartMenu
    {
        private IEnumerable<IScreener> screeners { get; set; }

        public StartMenu(IEnumerable<IScreener> screeners)
        {
            this.screeners = screeners;
        }

        public async Task RunAsync()
        {
            Console.WriteLine("Welcome to TradeR Screener!\n");
            PrintAvailableScreeners();

            Console.WriteLine();
            Console.Write("Choose an option: ");

            var selectedOption = Console.ReadLine();
        }

        private void PrintAvailableScreeners()
        {
            int i = 1;
            foreach (var screener in screeners)
            {
                Console.WriteLine($"{i}) {screener.Description}");
            }
        }
    }
}
