namespace piringku.Models
{
    public class Recipe
    {
        public string NamaResep { get; set; }
        public string ImageURL { get; set; }
        public string Instructions { get; set; }

        public Recipe()
        {
            NamaResep = string.Empty;
            ImageURL = string.Empty;
            Instructions = string.Empty;
        }
    }
}