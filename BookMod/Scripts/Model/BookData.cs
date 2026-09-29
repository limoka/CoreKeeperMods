using System;
using System.Collections.Generic;

namespace BookMod.Model
{
    public class BookData
    {
        public string title;
        public string description;
        public UIManager.CraftingUIThemeType backgroundTheme;

        public List<PageData> pages = new List<PageData>();

        private static readonly char[] separators = { '\r', '\n' };

        public static BookData parseFromText(string allData)
        {
            BookData data = new BookData();
            string[] lines = allData.Split(separators);

            int lastLineIndex = 0;
            string lastLine = "";

            try
            {
                foreach (string line in lines)
                {
                    if (string.IsNullOrEmpty(line)) continue;
                    if (line.StartsWith("##")) continue;
                    
                    lastLineIndex++;
                    lastLine = line;

                    if (line.StartsWith('!'))
                    {
                        ParseControlLine(line, data);
                        continue;
                    }
                    
                    int lastPage = data.pages.Count - 1;
                    if (lastPage < 0)
                        throw new Exception("Paragraph line cannot be defined before a page has been declared!");

                    data.pages[lastPage].paragraphs.Add(line);
                }
            }
            catch (Exception e)
            {
                BookMod.Log.LogWarning($"Failed to parse book data!\n Line {lastLineIndex}: {lastLine}\n Error: {e.Message}\n\n{e.StackTrace}");
                return null;
            }

            return data;
        }

        private static void ParseControlLine(string line, BookData data)
        {
            string conrolLine = line[1..];
            string[] parts = conrolLine.Split(' ');

            string command = parts[0];
            string[] args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

            // !title Test Book
            // !background 0
            // !page
            // !image top test.png

            switch (command)
            {
                case "title":
                {
                    if (args.Length <= 0)
                        throw new Exception("title control requires at least 1 argument!");
                    
                    string title = string.Join(' ', args);
                    data.title = title;
                    break;
                }
                case "description":
                {
                    if (args.Length <= 0)
                        throw new Exception("description control requires at least 1 argument!");
                    
                    string description = string.Join(' ', args);
                    data.description = description;
                    
                    break;
                }
                case "background":
                {
                    if (args.Length <= 0)
                        throw new Exception("background control requires 1 argument!");

                    if (!Enum.TryParse(args[0], true, out UIManager.CraftingUIThemeType background))
                        throw new Exception($"{args[0]} is not a valid CraftingUIThemeType!");
                    
                    data.backgroundTheme = background;
                    break;

                }
                case "page":
                {
                    data.pages.Add(new PageData());

                    break;
                }
                case "image":
                {
                    if (args.Length < 2)
                        throw new Exception("image control requires 2 arguments!");

                    if (!Enum.TryParse(args[0], true, out ImagePosition position))
                        throw new Exception($"{args[0]} is not a valid ImagePosition!");
                    
                    
                    int lastPage = data.pages.Count - 1;
                    if (lastPage < 0)
                        throw new Exception("image control used before any pages were defined!");

                    data.pages[lastPage].imagePosition = position;
                    data.pages[lastPage].imageResource = args[1];
                    break;
                }
            }
        }
    }
}