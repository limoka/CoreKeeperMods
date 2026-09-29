using System.Collections.Generic;

namespace BookMod.Model
{
    public class PageData
    {
        public List<string> paragraphs = new List<string>();
        
        public string imageResource;
        public ImagePosition imagePosition;
    }

    public enum ImagePosition
    {
        Top,
        Middle,
        Bottom
    }
}