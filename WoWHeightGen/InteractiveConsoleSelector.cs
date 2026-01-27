namespace WoWHeightGen
{
    /// <summary>
    /// Provides an interactive console UI for selecting items using arrow keys.
    /// </summary>
    public static class InteractiveConsoleSelector
    {
        /// <summary>
        /// Displays a list of items and allows selection using arrow keys.
        /// </summary>
        /// <typeparam name="T">The type of items to select from</typeparam>
        /// <param name="items">The list of items to display</param>
        /// <param name="displayFunc">Function to convert item to display string</param>
        /// <param name="prompt">Prompt message to display</param>
        /// <returns>Selected item or default if user cancels (Escape)</returns>
        public static T? SelectFromList<T>(
            List<T> items,
            Func<T, string> displayFunc,
            string prompt = "Use arrow keys to select, Enter to confirm, Esc for manual input:")
        {
            if (items == null || items.Count == 0)
                return default;

            int selectedIndex = 0;
            ConsoleKey key;

            do
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(prompt);
                Console.WriteLine();
                Console.ResetColor();

                // Display all items with highlight for selected
                for (int i = 0; i < items.Count; i++)
                {
                    if (i == selectedIndex)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write(" > ");
                    }
                    else
                    {
                        Console.Write("   ");
                    }

                    Console.WriteLine(displayFunc(items[i]));
                    Console.ResetColor();
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("Press Esc to enter path manually instead.");
                Console.ResetColor();

                key = Console.ReadKey(intercept: true).Key;

                switch (key)
                {
                    case ConsoleKey.UpArrow:
                        selectedIndex = selectedIndex > 0
                            ? selectedIndex - 1
                            : items.Count - 1;
                        break;
                    case ConsoleKey.DownArrow:
                        selectedIndex = (selectedIndex + 1) % items.Count;
                        break;
                    case ConsoleKey.Enter:
                        return items[selectedIndex];
                    case ConsoleKey.Escape:
                        return default;
                }

            } while (key != ConsoleKey.Enter && key != ConsoleKey.Escape);

            return default;
        }
    }
}
